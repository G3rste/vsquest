using Newtonsoft.Json.Linq;
using ProtoBuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VSQuest.Server
{
	public class Playsound : IQuestActionData
	{
		public string Path { get; set; } = "";
	}

	public class QuestActionTemplate(Type dataType, TestQuestAction action)
	{
		Type _dataType = dataType;
		TestQuestAction _action = action;

		public void Exec(IServerPlayer player, JObject json)
		{
			var data = json.ToObject(_dataType)!;
			_action(ApiModHelper.Api, player, (IQuestActionData)data);
		}
	}

	public interface IQuestActionData { }
	public delegate void TestQuestAction(ICoreServerAPI api, IServerPlayer player, IQuestActionData data);

	public class ServerSide
	{
		public Dictionary<string, Quest> QuestRegistry { get; private set; } = [];
		public Dictionary<string, QuestAction> ActionRegistry { get; private set; } = [];

		readonly ConcurrentDictionary<string, List<ActiveQuest>> _playerQuests = [];

		public ServerSide(ICoreServerAPI api, Mod mod)
		{
			ApiModHelper.Api = api;
			ApiModHelper.Mod = mod;

			api.RegisterEntityBehaviorClass("questgiver", typeof(EntityBehaviorQuestGiver));

			api.Network.GetChannel(mod.Info.ModID)
				.SetMessageHandler<QuestAcceptedMessage>(OnQuestAccepted)
				.SetMessageHandler<QuestCompletedMessage>(OnQuestCompleted);

			ActionRegistry.Add("despawnquestgiver", (api, message, byPlayer, args) => api.World.RegisterCallback(dt => api.World.GetEntityById(message.QuestGiverId).Die(EnumDespawnReason.Removed), int.Parse(args[0])));
			ActionRegistry.Add("playsound", (api, message, byPlayer, args) => api.World.PlaySoundFor(AssetLocation.CreateOrNull(args[0]), byPlayer));
			ActionRegistry.Add("spawnentities", ActionUtil.SpawnEntities);
			ActionRegistry.Add("spawnany", ActionUtil.SpawnAnyOfEntities);
			ActionRegistry.Add("spawnsmoke", ActionUtil.SpawnSmoke);
			ActionRegistry.Add("recruitentity", ActionUtil.RecruitEntity);
			ActionRegistry.Add("healplayer", (api, message, byPlayer, args) => byPlayer.Entity.ReceiveDamage(new() { Type = EnumDamageType.Heal }, 100));
			ActionRegistry.Add("addplayerattribute", (api, message, byPlayer, args) => byPlayer.Entity.WatchedAttributes.SetString(args[0], args[1]));
			ActionRegistry.Add("removeplayerattribute", (api, message, byPlayer, args) => byPlayer.Entity.WatchedAttributes.RemoveAttribute(args[0]));
			ActionRegistry.Add("completequest", ActionUtil.CompleteQuest);
			ActionRegistry.Add("acceptquest", (api, message, byPlayer, args) => OnQuestAccepted(byPlayer, new(long.Parse(args[0]), args[1])));
			ActionRegistry.Add("giveitem", ActionUtil.GiveItem);
			ActionRegistry.Add("addtraits", ActionUtil.AddTraits);
			ActionRegistry.Add("removetraits", ActionUtil.RemoveTraits);

			api.Event.GameWorldSave += () => OnSave();
			api.Event.PlayerDisconnect += player => OnDisconnect(player);
			api.Event.OnEntityDeath += (entity, dmgSource) => OnEntityDeath(entity, dmgSource);
			api.Event.DidBreakBlock += (byPlayer, blockId, blockSel) => GetPlayerQuests(byPlayer.PlayerUID).ForEach(quest => quest.OnBlockBroken(ApiModHelper.GetBlock(blockId).Code.Path));
			api.Event.DidPlaceBlock += (byPlayer, oldBlockId, blockSel, itemstack) => GetPlayerQuests(byPlayer.PlayerUID).ForEach(quest => quest.OnBlockPlaced(ApiModHelper.GetBlock(blockSel.Position).Code.Path));
		}

		public List<ActiveQuest> GetPlayerQuests(string playerUID)
		{
			return _playerQuests.GetOrAdd(playerUID, val => LoadPlayerQuests(val));
		}

		void OnEntityDeath(Entity entity, DamageSource damageSource)
		{
			if (damageSource?.SourceEntity is EntityPlayer player)
			{
				GetPlayerQuests(player.PlayerUID).ForEach(quest => quest.OnEntityKilled(entity.Code.Path));
			}
		}

		void OnDisconnect(IServerPlayer byPlayer)
		{
			if (_playerQuests.TryGetValue(byPlayer.PlayerUID, out var activeQuests))
			{
				SavePlayerQuests(byPlayer.PlayerUID, activeQuests);
				_playerQuests.Remove(byPlayer.PlayerUID);
			}
		}

		void OnSave()
		{
			foreach (var player in _playerQuests)
			{
				SavePlayerQuests(player.Key, player.Value);
			}
		}

		void OnQuestAccepted(IServerPlayer fromPlayer, QuestAcceptedMessage message)
		{
			var quest = QuestRegistry[message.QuestId];
			var killTrackers = new List<EventTracker>();

			foreach (var objective in quest.KillObjectives)
			{
				var tracker = new EventTracker()
				{
					Count = 0,
					RelevantCodes = [.. objective.ValidCodes]
				};
				killTrackers.Add(tracker);
			}

			var blockPlaceTrackers = new List<EventTracker>();

			foreach (var objective in quest.BlockPlaceObjectives)
			{
				var tracker = new EventTracker()
				{
					Count = 0,
					RelevantCodes = [.. objective.ValidCodes]
				};
				blockPlaceTrackers.Add(tracker);
			}

			var blockBreakTrackers = new List<EventTracker>();

			foreach (var objective in quest.BlockBreakObjectives)
			{
				var tracker = new EventTracker()
				{
					Count = 0,
					RelevantCodes = [.. objective.ValidCodes]
				};
				blockBreakTrackers.Add(tracker);
			}

			var activeQuest = new ActiveQuest()
			{
				QuestGiverId = message.QuestGiverId,
				QuestId = message.QuestId,
				KillTrackers = killTrackers,
				BlockPlaceTrackers = blockPlaceTrackers,
				BlockBreakTrackers = blockBreakTrackers
			};
			GetPlayerQuests(fromPlayer.PlayerUID).Add(activeQuest);

			var questgiver = ApiModHelper.GetEntity(message.QuestGiverId);
			var key = quest.PerPlayer ? $"lastaccepted-{quest.Id}-{fromPlayer.PlayerUID}" : $"lastaccepted-{quest.Id}";
			questgiver.WatchedAttributes.SetDouble(key, ApiModHelper.TotalDays);
			questgiver.WatchedAttributes.MarkPathDirty(key);

			foreach (var action in quest.OnAcceptedActions)
			{
				try
				{
					//ActionRegistry[action.Id].Invoke(ApiModHelper.Api, message, fromPlayer, action.Args);
				}
				catch (Exception ex)
				{
					ApiModHelper.Error($"Action {action.Id} caused an error in quest {quest.Id}.");
					ApiModHelper.Error(ex);
					ApiModHelper.SendLogMessage(fromPlayer, $"An error occurred during quest {quest.Id}, please check the server logs for more details.");
				}
			}
		}

		public void OnQuestCompleted(IServerPlayer fromPlayer, QuestCompletedMessage message)
		{
			var playerQuests = GetPlayerQuests(fromPlayer.PlayerUID);
			var activeQuest = playerQuests.Find(q => q.QuestId == message.QuestId && q.QuestGiverId == message.QuestGiverId);

			if (activeQuest is null)
			{
				ApiModHelper.Error($@"Completed quest not found in active quests : ""{message.QuestId}"" from ""{message.QuestGiverId}""");
				return;
			}

			if (activeQuest.IsCompletable(fromPlayer))
			{
				activeQuest.CompleteQuest(fromPlayer);
				playerQuests.Remove(activeQuest);
				var questgiver = ApiModHelper.GetEntity(message.QuestGiverId);
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
			var quest = QuestRegistry[message.QuestId];
			foreach (var reward in quest.ItemRewards)
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

			List<RandomItem> randomItems = quest.RandomItemRewards.Items;
			for (int i = 0; i < quest.RandomItemRewards.SelectAmount; i++)
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

			foreach (var action in quest.OnCompletedActions)
			{
				try
				{
					//ActionRegistry[action.Id].Invoke(ApiModHelper.Api, message, fromPlayer, action.Args);
				}
				catch (Exception ex)
				{
					ApiModHelper.Error($"Action {action.Id} caused an error in quest {quest.Id}.");
					ApiModHelper.Error(ex);
					ApiModHelper.SendLogMessage(fromPlayer, $"An error occurred during quest {quest.Id}, please check the server logs for more details.");
				}
			}
		}

		static void MarkQuestCompleted(IServerPlayer fromPlayer, QuestCompletedMessage message, Entity questgiver)
		{
			var completedQuests = new HashSet<string>(questgiver.WatchedAttributes.GetStringArray($"playercompleted-{fromPlayer.PlayerUID}", []))
			{
				message.QuestId
			};
			var completedQuestsArray = new string[completedQuests.Count];
			completedQuests.CopyTo(completedQuestsArray);
			questgiver.WatchedAttributes.SetStringArray($"playercompleted-{fromPlayer.PlayerUID}", completedQuestsArray);
		}

		static void SavePlayerQuests(string playerUID, List<ActiveQuest> activeQuests) => ApiModHelper.SaveData($"quests-{playerUID}", activeQuests);

		static List<ActiveQuest> LoadPlayerQuests(string playerUID)
		{
			try
			{
				return ApiModHelper.LoadData<List<ActiveQuest>>($"quests-{playerUID}") ?? [];
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

		public static double TotalDays => Api.World.Calendar.TotalDays;

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