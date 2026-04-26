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

namespace VSQuest
{
	public class EntityBehaviorQuestGiver(Entity entity) : EntityBehavior(entity)
	{
		public override string PropertyName() => "questgiver";

		private string[] quests = [];
		private bool selectRandom;
		private int selectRandomCount;

		public override void Initialize(EntityProperties properties, JsonObject attributes)
		{
			base.Initialize(properties, attributes);
			selectRandom = attributes["selectrandom"].AsBool();
			selectRandomCount = attributes["selectrandomcount"].AsInt(1);

			quests = [.. attributes["quests"].AsArray<string>([]).OfType<string>()];

			// simple randomizer that will always select the same quests for each entityId
			if (selectRandom)
			{
				int seed = unchecked((int)entity.EntityId);
				var questList = new List<string>(quests);
				var resultList = new List<string>();
				for (int i = 0; i < Math.Min(selectRandomCount, quests.Length); i++)
				{
					seed = (seed * 5 + 7) % questList.Count;
					resultList.Add(questList[seed]);
					questList.RemoveAt(seed);
				}
				quests = [.. resultList];
			}
		}

		public override void AfterInitialized(bool onFirstSpawn)
		{
			base.AfterInitialized(onFirstSpawn);
			var bh = entity.GetBehavior<EntityBehaviorConversable>();
			bh?.OnControllerCreated += (controller) =>
			{
				controller.DialogTriggers += Dialog_DialogTriggers;
			};
		}

		private int Dialog_DialogTriggers(EntityAgent triggeringEntity, string value, JsonObject data)
		{
			var behaviorConversable = entity.GetBehavior<EntityBehaviorConversable>();
			behaviorConversable?.Dialog?.TryClose();

			if (value == "openquests" && triggeringEntity.Api is ICoreServerAPI sapi)
			{
				SendQuestInfoMessageToClient(sapi, (EntityPlayer)triggeringEntity);
				return 0;
			}

			return -1;
		}


		public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
		{
			if (entity.Alive
				&& entity.Api is ICoreServerAPI sapi
				&& byEntity is EntityPlayer player
				&& mode == EnumInteractMode.Interact
				&& player.Controls.Sneak
				&& !entity.HasBehavior<EntityBehaviorConversable>())
			{
				SendQuestInfoMessageToClient(sapi, player);
			}
		}

		public void SendQuestInfoMessageToClient(ICoreServerAPI sapi, EntityPlayer player)
		{
			var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
			var activeQuests = questSystem.GetPlayerQuests(player.PlayerUID).FindAll(quest => quest.QuestGiverId == entity.EntityId);
			var availableQuestIds = new List<string>();
			foreach (var questId in quests)
			{
				var quest = questSystem.QuestRegistry[questId];
				var key = quest.PerPlayer ? $"lastaccepted-{questId}-{player.PlayerUID}" : $"lastaccepted-{questId}";
				if (entity.WatchedAttributes.GetDouble(key, -quest.Cooldown) + quest.Cooldown < sapi.World.Calendar.TotalDays
						&& activeQuests.Find(activeQuest => activeQuest.QuestId == questId && activeQuest.QuestGiverId == entity.EntityId) == null
						&& PredecessorsCompleted(quest, player.PlayerUID))
				{
					availableQuestIds.Add(questId);
				}
			}
			var message = new QuestInfoMessage()
			{
				QuestGiverId = entity.EntityId,
				AvailableQuestIds = availableQuestIds,
				ActiveQuests = activeQuests
			};

			sapi.Network.GetChannel("vsquest").SendPacket(message, player.Player as IServerPlayer);
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

		private bool PredecessorsCompleted(Quest quest, string playerUID)
		{
			var completedQuests = new List<string>(entity.WatchedAttributes.GetStringArray($"playercompleted-{playerUID}", []));
			return string.IsNullOrEmpty(quest.Predecessor)
				|| completedQuests.Contains(quest.Predecessor);
		}
	}
}