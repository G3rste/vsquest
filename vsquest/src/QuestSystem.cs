using Newtonsoft.Json.Linq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using VSQuest.Client;
using VSQuest.Server;

namespace VSQuest
{
	public delegate void QuestAction(ICoreServerAPI sapi, QuestMessage message, IServerPlayer player, string[] args);
	public class QuestSystem : ModSystem
	{
		public Dictionary<string, IActiveActionObjective> ActionObjectiveRegistry { get; private set; } = [];

		public Dictionary<string, Quest> QuestRegistry => _server!.QuestRegistry;

		public void OnQuestCompleted(IServerPlayer player, QuestCompletedMessage message) =>
			_server?.OnQuestCompleted(player, message);

		public List<ActiveQuest> GetPlayerQuests(string playerUID) =>
			_server!.GetPlayerQuests(playerUID);

		ClientSide? _client;
		ServerSide? _server;

		public override double ExecuteOrder() => 0.15;

		public override void Start(ICoreAPI api)
		{
			api.Network.RegisterChannel(Mod.Info.ModID)
				.RegisterMessageType<QuestAcceptedMessage>()
				.RegisterMessageType<QuestCompletedMessage>()
				.RegisterMessageType<QuestInfoMessage>();


			api.RegisterEntityBehaviorClass("questgiver", typeof(EntityBehaviorQuestGiver));

			api.RegisterItemClass("ItemDebugTool", typeof(ItemDebugTool));

			ActionObjectiveRegistry.Add("plantflowers", new NearbyFlowersActionObjective());
			ActionObjectiveRegistry.Add("hasAttribute", new PlayerHasAttributeActionObjective());
		}

		public override void StartClientSide(ICoreClientAPI api)
		{
			_client = new(api, Mod);
		}

		public override void StartServerSide(ICoreServerAPI api)
		{
			_server = new(api, Mod);
			ActionTemplates.Add("playsound", new(typeof(Playsound), (api, player, data) => {  }));
		}

		public static Dictionary<string, QuestActionTemplate> ActionTemplates = [];

		public override void AssetsLoaded(ICoreAPI api)
		{
			if (api.Side != EnumAppSide.Server)
			{
				return;
			}

			foreach (var mod in api.ModLoader.Mods)
			{
				//var assets = api.Assets.GetMany("config/quests", mod.Info.ModID);

				//foreach(var asset in assets)
				//{
				//	var quest = asset.ToObject<List<Quest>>();
				//}
				
				var quests = api.Assets.GetMany<List<Quest>>(mod.Logger, "config/quests", mod.Info.ModID).SelectMany(entry => entry.Value);
				AddQuests(quests);
			}
		}

		public void AddQuests(IEnumerable<Quest> quests)
		{
			foreach (var quest in quests)
			{
				_server?.QuestRegistry.Add(quest.Id, quest);
			}
		}

		public void AddObjectives()
		{

		}

		public void AddQuestActions(IEnumerable<QuestAction> actions)
		{

		}
	}

	[ProtoContract]
	public class QuestAcceptedMessage(long questGiverId, string questId) :
		QuestMessage(questGiverId, questId)
	{ }

	[ProtoContract]
	public class QuestCompletedMessage(long questGiverId, string questId) :
		QuestMessage(questGiverId, questId)
	{ }

	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
	[ProtoInclude(10, typeof(QuestAcceptedMessage))]
	[ProtoInclude(11, typeof(QuestCompletedMessage))]
	public abstract class QuestMessage(long questGiverId, string questId)
	{
		public string QuestId => questId;
		public long QuestGiverId => questGiverId;
	}

	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
	public class QuestInfoMessage(long questGiverId, IEnumerable<string> availableQuestIds, IDictionary<string, bool> activeQuestData)
	{
		public long QuestGiverId => questGiverId;
		public IEnumerable<string> AvailableQuestIds => availableQuestIds;
		public IDictionary<string, bool> ActiveQuestData => activeQuestData;
	}
}