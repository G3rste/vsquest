using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.GameContent;
using System.Linq;
using Vintagestory.API.Util;
using VSQuest.Client;
using VSQuest.Model;

namespace VSQuest.Server
{
	public class EntityBehaviorQuestGiver(Entity entity) : EntityBehavior(entity)
	{
		public override string PropertyName() => "questgiver";

		string[] _quests = [];
		bool _selectRandom;
		int _selectRandomCount;

		public override void Initialize(EntityProperties properties, JsonObject attributes)
		{
			_selectRandom = attributes["selectrandom"].AsBool();
			_selectRandomCount = attributes["selectrandomcount"].AsInt(1);

			_quests = [.. attributes["quests"].AsArray<string>([]).OfType<string>()];

			// simple randomizer that will always select the same quests for each entityId
			if (_selectRandom)
			{
				int seed = unchecked((int)entity.EntityId);
				var questList = new List<string>(_quests);
				var resultList = new List<string>();

				for (int i = 0; i < Math.Min(_selectRandomCount, _quests.Length); i++)
				{
					seed = (seed * 5 + 7) % questList.Count;
					resultList.Add(questList[seed]);
					questList.RemoveAt(seed);
				}
				_quests = [.. resultList];
			}
		}

		public override void AfterInitialized(bool onFirstSpawn)
		{
			var bh = entity.GetBehavior<EntityBehaviorConversable>();
			bh?.OnControllerCreated += controller => controller.DialogTriggers += Dialog_DialogTriggers;
		}

		int Dialog_DialogTriggers(EntityAgent triggeringEntity, string value, JsonObject data)
		{
			if (value == "openquests")
			{
				var behaviorConversable = entity.GetBehavior<EntityBehaviorConversable>();
				behaviorConversable?.Dialog?.TryClose();

				SendQuestInfoMessageToClient((EntityPlayer)triggeringEntity);
				return 0;
			}

			return -1;
		}


		public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
		{
			if (entity.Alive
				&& byEntity is EntityPlayer player
				&& mode == EnumInteractMode.Interact
				&& player.Controls.Sneak
				&& !entity.HasBehavior<EntityBehaviorConversable>())
			{
				SendQuestInfoMessageToClient(player);
			}
		}

		public void SendQuestInfoMessageToClient(EntityPlayer player)
		{
			var questSystem = ApiModHelper.GetModSystem<QuestSystem>();
			var activeQuests = questSystem.Server.GetQuests(player.PlayerUID).FindAll(quest => quest.Info.GiverId == entity.EntityId);

			var availableQuestIds = new List<string>();
			foreach (var questId in _quests)
			{
				var quest = questSystem.QuestRegistry[questId];

				if (quest.Cooldown >= 0)
				{
					var key = $"lastcompletion-{questId}";
					var target = quest.PerPlayer ? player : entity;
					var lastCompletion = target.WatchedAttributes.GetDouble(key, 0);

					if (quest.Cooldown + lastCompletion > ApiModHelper.TotalDays)
					{
						continue;
					}
				}

				if (activeQuests.All(quest => quest.Info.Id != questId || quest.Info.GiverId != entity.EntityId) && PredecessorsCompleted(quest, player.PlayerUID))
				{
					availableQuestIds.Add(questId);
				}
			}

			var activeQuestData = activeQuests.Select(quest => (quest.Info.Id, quest.IsCompletable(player.Player))).ToDictionary();
			var message = new QuestGiverInfoMessage(entity.EntityId, availableQuestIds, activeQuestData);
			ApiModHelper.GetChannel().SendPacket(message, player.Player as IServerPlayer);
		}

		public override WorldInteraction[]? GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
		{
			if (entity.Alive && !entity.HasBehavior<EntityBehaviorConversable>())
			{
				return [
					new (){
						ActionLangCode = "vsquest:access-quests",
						MouseButton = EnumMouseButton.Right,
						HotKeyCode = "sneak"
					}
				];
			}
			else { return base.GetInteractionHelp(world, es, player, ref handled); }
		}

		bool PredecessorsCompleted(QuestTemplate quest, string playerUID)
		{
			var completedQuests = entity.WatchedAttributes.GetStringArray($"playercompleted-{playerUID}", []);
			return quest.Predecessors.Count == 0 || quest.Predecessors.All(questId => completedQuests.Contains(questId));
		}
	}
}