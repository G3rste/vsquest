using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using VSQuest.Server.Model;
using VSQuest.Templates;

namespace VSQuest.Server
{
	public class ServerSide : IDisposable
	{
		public TemplateRepositories Templates { get; } = new();

		readonly ConcurrentDictionary<string, PlayerState> _playerStates = [];

		public ServerSide(ICoreServerAPI api, Mod mod)
		{
			ApiModHelper.Api = api;
			ApiModHelper.Mod = mod;

			ApiModHelper.GetChannel()
				.SetMessageHandler<AcceptQuestCommand>(OnQuestAccepted);
			//.SetMessageHandler<CompleteQuestCommand>(OnQuestCompleted);

			api.RegisterEntityBehaviorClass("questgiver", typeof(EntityBehaviorQuestGiver));

			api.Event.GameWorldSave += OnSave;
			api.Event.PlayerJoin += OnConnect;
			api.Event.PlayerDisconnect += OnDisconnect;
		}

		public void Dispose()
		{
			ApiModHelper.Api.Event.GameWorldSave -= OnSave;
			ApiModHelper.Api.Event.PlayerJoin -= OnConnect;
			ApiModHelper.Api.Event.PlayerDisconnect -= OnDisconnect;

			GC.SuppressFinalize(this);
		}

		public void AddQuestTemplates(IQuestTemplate[] templates)
		{
			foreach (var template in templates)
			{
				AddTemplate<IQuestTemplate>(template.Id, template);
			}
		}

		public void AddTemplate<T>(string id, ITemplate template)
			where T : ITemplate =>
			Templates.Register<T>(id, template);

		public IQuestTemplate GetQuestTemplate(string id) => Templates.Get<IQuestTemplate>(id);

		void OnConnect(IServerPlayer player)
		{
			var uid = player.PlayerUID;
			_playerStates.TryAdd(uid, new(player));
		}

		void OnDisconnect(IServerPlayer player)
		{
			var uid = player.PlayerUID;
			if (_playerStates.TryGetValue(uid, out var state))
			{
				state.Save();
				_playerStates.Remove(uid);
			}
		}

		void OnSave()
		{
			foreach (var state in _playerStates.Values)
			{
				state.Save();
			}
		}

		public IQuest[] GetQuests(string playerUID, long giverId)
		{
			if (_playerStates.TryGetValue(playerUID, out var state))
			{
				return state.GetQuestsFor(giverId);
			}
			return [];
		}




		


		public void OnQuestAccepted(IServerPlayer player, AcceptQuestCommand message)
		{
			//TODO : sanitize and check if ok
			var id = message.Id;
			var template = GetQuestTemplate(id);
			var context = new QuestCreationContext(ApiModHelper.Api, template, player, message.GiverId);
			var activeQuest = new Quest(context);
			_playerStates[player.PlayerUID].AcceptQuest(activeQuest);
		}

		public void OnQuestCompleted(IServerPlayer player, CompleteQuestCommand message)
		{
			_playerStates[player.PlayerUID].CompleteQuest(message);
		}

		//void RewardPlayer(IServerPlayer fromPlayer, CompleteQuestCommand message, Entity questgiver)
		//{
		//	var template = _questTemplates[message.Id];
		//	foreach (var reward in template.ItemRewards)
		//	{
		//		CollectibleObject? item = ApiModHelper.GetItem(AssetLocation.CreateOrNull(reward.ItemCode));
		//		item ??= ApiModHelper.GetBlock(AssetLocation.CreateOrNull(reward.ItemCode));

		//		if (item == null) continue;

		//		var stack = new ItemStack(item, reward.Amount);
		//		if (!fromPlayer.InventoryManager.TryGiveItemstack(stack))
		//		{
		//			ApiModHelper.SpawnItem(stack, questgiver.Pos.XYZ);
		//		}
		//	}

		//	List<RandomItem> randomItems = template.RandomItemRewards.Items;
		//	for (int i = 0; i < template.RandomItemRewards.SelectAmount; i++)
		//	{
		//		if (randomItems.Count <= 0)
		//		{
		//			break;
		//		}

		//		var randomItem = randomItems[ApiModHelper.NextRand(0, randomItems.Count)];
		//		randomItems.Remove(randomItem);

		//		CollectibleObject? item = ApiModHelper.GetItem(AssetLocation.CreateOrNull(randomItem.ItemCode));
		//		item ??= ApiModHelper.GetBlock(AssetLocation.CreateOrNull(randomItem.ItemCode));

		//		var stack = new ItemStack(item, ApiModHelper.NextRand(randomItem.MinAmount, randomItem.MaxAmount + 1));
		//		if (!fromPlayer.InventoryManager.TryGiveItemstack(stack))
		//		{
		//			ApiModHelper.SpawnItem(stack, questgiver.Pos.XYZ);
		//		}
		//	}
		//}

		static void MarkQuestCompleted(IServerPlayer player, CompleteQuestCommand message, Entity questGiver)
		{
			var completedQuests = new HashSet<string>(questGiver.WatchedAttributes.GetStringArray($"playercompleted-{player.PlayerUID}", []))
			{
				message.Id
			};
			var completedQuestsArray = new string[completedQuests.Count];
			completedQuests.CopyTo(completedQuestsArray);
			questGiver.WatchedAttributes.SetStringArray($"playercompleted-{player.PlayerUID}", completedQuestsArray);
		}
	}

	public static partial class ApiModHelper
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

		public static Entity? GetEntity(long? id) => id.HasValue ? Api.World.GetEntityById(id.Value) : null;
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