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
using VSQuest.Model.Server.Data;

namespace VSQuest.Server
{
	public class EntityBehaviorQuestGiver(Entity entity) : EntityBehavior(entity)
	{
		public override string PropertyName() => "questgiver";

		string[] _questIds = [];
		bool _selectRandom;
		int _selectRandomCount;

		public override void Initialize(EntityProperties properties, JsonObject attributes)
		{
			_selectRandom = attributes["selectrandom"].AsBool();
			_selectRandomCount = attributes["selectrandomcount"].AsInt(1);

			_questIds = [.. attributes["quests"].AsArray<string>([]).OfType<string>()];

			// simple randomizer that will always select the same quests for each entityId
			if (_selectRandom)
			{
				int seed = unchecked((int)entity.EntityId);
				var questList = new List<string>(_questIds);
				var resultList = new List<string>();

				for (int i = 0; i < Math.Min(_selectRandomCount, _questIds.Length); i++)
				{
					seed = (seed * 5 + 7) % questList.Count;
					resultList.Add(questList[seed]);
					questList.RemoveAt(seed);
				}
				_questIds = [.. resultList];
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

				SendQuestGiverDataTo((EntityPlayer)triggeringEntity);
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
				SendQuestGiverDataTo(player);
			}
		}

		public void SendQuestGiverDataTo(EntityPlayer player)
		{
			var questSystem = ApiModHelper.GetModSystem<QuestSystem>();
			var activeQuests = questSystem.Server.GetQuests(player.PlayerUID, entity.EntityId);

			var availableQuestIds = new List<string>();
			foreach (var questId in _questIds)
			{
				var template = questSystem.Server.GetQuestTemplate(questId);
				var data = new RuntimeData(ApiModHelper.Api);
				if (template.Locks.All(l => l.Check(data)))
				{
					availableQuestIds.Add(questId);
				}

				//if (template.Cooldown >= 0)
				//{
				//	var key = $"lastcompletion-{questId}";
				//	var target = template.Shared ? entity : player;
				//	var lastCompletion = target.WatchedAttributes.GetDouble(key, 0);

				//	if (template.Cooldown + lastCompletion > ApiModHelper.TotalDays)
				//	{
				//		continue;
				//	}
				//}

				//if (activeQuests.All(quest => quest.Id != questId || quest.GiverId != entity.EntityId) && template.PredecessorsCompletedBy(player))
				//{
				//	availableQuestIds.Add(questId);
				//}
			}

			var activeQuestData = activeQuests.Select(quest => (quest.Id, quest.Fullfilled)).ToDictionary();
			var message = new QuestGiverInfoResponse(entity.EntityId, availableQuestIds, activeQuestData);
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
	}
}