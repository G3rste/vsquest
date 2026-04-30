using ProtoBuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using VSQuest.Model;
using VSQuest.Model.Action;

namespace VSQuest.Server
{
	public class TargetData : IObjectiveData
	{
		public required string[] Codes { get; set; }
		public required int Amount { get; set; }
	}

	public class BlockData : IObjectiveData
	{
		public required string[] Codes { get; set; }
		public required int Amount { get; set; }
	}

	public class ServerSide
	{
		readonly Dictionary<string, IActiveActionObjective> _objectiveRegistry = [];

		readonly Dictionary<string, QuestTemplate> _questTemplates = [];
		readonly Dictionary<string, IQuestActionTemplate> _actionTemplates = [];
		readonly Dictionary<string, IObjectiveTemplate> _objectiveTemplates = [];

		readonly ConcurrentDictionary<string, List<Quest>> _playerQuests = [];

		public ServerSide(ICoreServerAPI api, Mod mod)
		{
			ApiModHelper.Api = api;
			ApiModHelper.Mod = mod;

			ApiModHelper.GetChannel()
				.SetMessageHandler<QuestAcceptedMessage>(OnQuestAccepted)
				.SetMessageHandler<QuestCompletedMessage>(OnQuestCompleted);

			api.RegisterEntityBehaviorClass("questgiver", typeof(EntityBehaviorQuestGiver));

			_objectiveTemplates.Add("kill", new ObjectiveTemplate<TargetData>());

			_objectiveRegistry.Add("plantflowers", new NearbyFlowersActionObjective());
			_objectiveRegistry.Add("hasAttribute", new PlayerHasAttributeActionObjective());

			api.Event.GameWorldSave += OnSave;
			api.Event.PlayerJoin += OnConnect;
			api.Event.PlayerDisconnect += OnDisconnect;







			api.Event.OnEntityDeath += OnEntityDeath;
			api.Event.DidBreakBlock += DidBreakBlock;
			api.Event.DidPlaceBlock += DidPlaceBlock;
		}

		public void AddActionTemplate(string id, IQuestActionTemplate template) =>
			_actionTemplates.Add(id, template);

		public void AddObjectiveTemplate(string id, IObjectiveTemplate template) =>
			_objectiveTemplates.Add(id, template);

		public void AddQuestTemplates(IEnumerable<QuestTemplate> templates)
		{
			foreach (var template in templates)
			{
				_questTemplates.Add(template.Id, template);

				foreach (var actionContainer in template.OnAcceptActions)
				{
					var actionTemplate = _actionTemplates[actionContainer.Id];
					actionContainer.SetTemplate(actionTemplate);
				}

				foreach (var actionContainer in template.OnCompleteActions)
				{
					var actionTemplate = _actionTemplates[actionContainer.Id];
					actionContainer.SetTemplate(actionTemplate);
				}
			}
		}

		void OnConnect(IServerPlayer player)
		{
			var uid = player.PlayerUID;
			_playerQuests.TryAdd(uid, LoadPlayerQuests(uid));
		}

		void OnDisconnect(IServerPlayer player)
		{
			var uid = player.PlayerUID;
			if (_playerQuests.TryGetValue(uid, out var quests))
			{
				SavePlayerQuests(uid, quests);
				_playerQuests.Remove(uid);
			}
		}

		void OnSave()
		{
			foreach (var (uid, quests) in _playerQuests)
			{
				SavePlayerQuests(uid, quests);
			}
		}

		public List<Quest> GetQuests(string playerUID)
		{
			if (_playerQuests.TryGetValue(playerUID, out var quests))
			{
				return quests;
			}
			return [];
		}








		void OnEntityDeath(Entity entity, DamageSource? damageSource)
		{
			if (damageSource?.GetCauseEntity() is EntityPlayer player)
			{
				GetQuests(player.PlayerUID).ForEach(quest => quest.OnEntityKilled(entity.Code.Path));
			}
		}

		

		void DidPlaceBlock(IServerPlayer byPlayer, int _, BlockSelection blockSel, ItemStack __)
		{
			foreach (var quest in GetQuests(byPlayer.PlayerUID))
			{
				quest.OnBlockPlaced(ApiModHelper.GetBlock(blockSel.Position).Code.Path);
			}
		}

		void DidBreakBlock(IServerPlayer byPlayer, int oldblockId, BlockSelection _)
		{
			foreach (var quest in GetQuests(byPlayer.PlayerUID))
			{
				quest.OnBlockBroken(ApiModHelper.GetBlock(oldblockId).Code.Path);
			}
		}

		public void OnQuestAccepted(IServerPlayer player, QuestAcceptedMessage message)
		{
			var id = message.Id;
			var template = _questTemplates[id];



			var killTrackers = new List<EventTracker>();

			foreach (var objective in template.KillObjectives)
			{
				var tracker = new EventTracker()
				{
					Count = 0,
					RelevantCodes = [.. objective.ValidCodes]
				};
				killTrackers.Add(tracker);
			}

			var blockPlaceTrackers = new List<EventTracker>();

			foreach (var objective in template.BlockPlaceObjectives)
			{
				var tracker = new EventTracker()
				{
					Count = 0,
					RelevantCodes = [.. objective.ValidCodes]
				};
				blockPlaceTrackers.Add(tracker);
			}

			var blockBreakTrackers = new List<EventTracker>();

			foreach (var objective in template.BlockBreakObjectives)
			{
				var tracker = new EventTracker()
				{
					Count = 0,
					RelevantCodes = [.. objective.ValidCodes]
				};
				blockBreakTrackers.Add(tracker);
			}

			var activeQuest = new Quest(new(id, message.GiverId, player), template)
			{
				KillTrackers = killTrackers,
				BlockPlaceTrackers = blockPlaceTrackers,
				BlockBreakTrackers = blockBreakTrackers
			};
			GetQuests(player.PlayerUID).Add(activeQuest);
			
			foreach (var actionContainer in template.OnAcceptActions)
			{
				try
				{
					actionContainer?.Exec(new(id, message.GiverId, player));
				}
				catch (Exception ex)
				{
					ApiModHelper.Error($"Action {actionContainer.Id} caused an error in quest {id}.");
					ApiModHelper.Error(ex);
					ApiModHelper.SendLogMessage(player, $"An error occurred during quest {id}, please check the server logs for more details.");
				}
			}
		}

		public void OnQuestCompleted(IServerPlayer fromPlayer, QuestCompletedMessage message)
		{
			var playerQuests = GetQuests(fromPlayer.PlayerUID);
			var activeQuest = playerQuests.Find(q => q.Info.Id == message.Id && q.Info.GiverId == message.GiverId);

			if (activeQuest is null)
			{
				ApiModHelper.Error($@"Completed quest not found in active quests : ""{message.Id}"" from ""{message.GiverId}""");
				return;
			}

			if (activeQuest.IsCompletable(fromPlayer))
			{
				activeQuest.CompleteQuest(fromPlayer);
				playerQuests.Remove(activeQuest);

				var quest = _questTemplates[message.Id];


				var questgiver = ApiModHelper.GetEntity(message.GiverId);
				var key = quest.PerPlayer ? $"lastaccepted-{quest.Id}-{fromPlayer.PlayerUID}" : $"lastaccepted-{quest.Id}";
				questgiver.WatchedAttributes.SetDouble(key, ApiModHelper.TotalDays);
				questgiver.WatchedAttributes.MarkPathDirty(key);

				RewardPlayer(fromPlayer, message, questgiver);
				MarkQuestCompleted(fromPlayer, message, questgiver);
			}
			else
			{
				ApiModHelper.SendLogMessage(fromPlayer, "Something went wrong, the quest could not be completed");
			}
		}

		void RewardPlayer(IServerPlayer fromPlayer, QuestCompletedMessage message, Entity questgiver)
		{
			var template = _questTemplates[message.Id];
			foreach (var reward in template.ItemRewards)
			{
				CollectibleObject? item = ApiModHelper.GetItem(AssetLocation.CreateOrNull(reward.ItemCode));
				item ??= ApiModHelper.GetBlock(AssetLocation.CreateOrNull(reward.ItemCode));

				if (item == null) continue;

				var stack = new ItemStack(item, reward.Amount);
				if (!fromPlayer.InventoryManager.TryGiveItemstack(stack))
				{
					ApiModHelper.SpawnItem(stack, questgiver.Pos.XYZ);
				}
			}

			List<RandomItem> randomItems = template.RandomItemRewards.Items;
			for (int i = 0; i < template.RandomItemRewards.SelectAmount; i++)
			{
				if (randomItems.Count <= 0)
				{
					break;
				}

				var randomItem = randomItems[ApiModHelper.NextRand(0, randomItems.Count)];
				randomItems.Remove(randomItem);

				CollectibleObject? item = ApiModHelper.GetItem(AssetLocation.CreateOrNull(randomItem.ItemCode));
				item ??= ApiModHelper.GetBlock(AssetLocation.CreateOrNull(randomItem.ItemCode));

				var stack = new ItemStack(item, ApiModHelper.NextRand(randomItem.MinAmount, randomItem.MaxAmount + 1));
				if (!fromPlayer.InventoryManager.TryGiveItemstack(stack))
				{
					ApiModHelper.SpawnItem(stack, questgiver.Pos.XYZ);
				}
			}

			foreach (var actionContainer in template.OnCompleteActions)
			{
				try
				{
					actionContainer.Exec(new(template.Id, message.GiverId, fromPlayer));
				}
				catch (Exception ex)
				{
					ApiModHelper.Error($"Action {actionContainer.Id} caused an error in quest {template.Id}.");
					ApiModHelper.Error(ex);
					ApiModHelper.SendLogMessage(fromPlayer, $"An error occurred during quest {template.Id}, please check the server logs for more details.");
				}
			}
		}

		static void MarkQuestCompleted(IServerPlayer player, QuestCompletedMessage message, Entity questGiver)
		{
			var completedQuests = new HashSet<string>(questGiver.WatchedAttributes.GetStringArray($"playercompleted-{player.PlayerUID}", []))
			{
				message.Id
			};
			var completedQuestsArray = new string[completedQuests.Count];
			completedQuests.CopyTo(completedQuestsArray);
			questGiver.WatchedAttributes.SetStringArray($"playercompleted-{player.PlayerUID}", completedQuestsArray);
		}

		static void SavePlayerQuests(string playerUID, List<Quest> activeQuests) => ApiModHelper.SaveData($"quests-{playerUID}", activeQuests);

		static List<Quest> LoadPlayerQuests(string playerUID)
		{
			try
			{
				return ApiModHelper.LoadData<List<Quest>>($"quests-{playerUID}") ?? [];
			}
			catch (ProtoException)
			{
				ApiModHelper.Error($"Could not load quests for player with id {playerUID}, corrupted quests will be deleted.");
				return [];
			}
		}
	}

	public static class ApiModHelper
	{
		static ICoreServerAPI? _api;
		public static ICoreServerAPI Api
		{
			get => _api ?? throw new Exception("Api is null");
			set => _api = value;
		}

		static Mod? _mod;
		public static Mod Mod
		{
			get => _mod ?? throw new Exception("Mod is null");
			set => _mod = value;
		}

		public static string ModId => Mod.Info.ModID;
		public static double TotalDays => Api.World.Calendar.TotalDays;

		public static void SpawnEntity(Entity entity) => Api.World.SpawnEntity(entity);
		public static Entity CreateEntity(EntityProperties props) => Api.World.ClassRegistry.CreateEntity(props);
		public static EntityProperties? GetEntityProps(string code) => Api.World.GetEntityType(new(code));
		public static T GetModSystem<T>() where T: ModSystem => Api.ModLoader.GetModSystem<T>();
		public static void SendLogMessage(IServerPlayer player, string message) => Api.SendMessage(player, GlobalConstants.InfoLogChatGroup, message, EnumChatType.Notification);
		public static void SpawnItem(ItemStack stack, Vec3d position) => Api.World.SpawnItemEntity(stack, position);
		public static int NextRand(int min, int max) => Api.World.Rand.Next(min, max);
		public static IServerNetworkChannel GetChannel() => Api.Network.GetChannel(Mod.Info.ModID);
		public static CollectibleObject? GetItem(AssetLocation path) => Api.World.GetItem(path);
		public static Block GetBlock(int blockId) => Api.World.GetBlock(blockId);
		public static Block? GetBlock(AssetLocation path) => Api.World.GetBlock(path);
		public static Block GetBlock(BlockPos blockPosition) => Api.World.BlockAccessor.GetBlock(blockPosition);
		public static Entity GetEntity(long id) => Api.World.GetEntityById(id);
		public static void Notify(string message) => Mod.Logger.Notification(message);
		public static void Warn(string message) => Mod.Logger.Warning(message);
		public static void Error(string message) => Mod.Logger.Error(message);
		public static void Error(Exception ex) => Mod.Logger.Error(ex);

		public static void SaveData<T>(string key, T data) => Api.WorldManager.SaveGame.StoreData(key, data);
		public static T LoadData<T>(string key) => Api.WorldManager.SaveGame.GetData<T>(key);
	}
}