using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using VSQuest.Client;
using VSQuest.Model;
using VSQuest.Model.Action;
using VSQuest.Server;

namespace VSQuest
{
	public class QuestSystem : ModSystem
	{
		ServerSide? _server;
		public ServerSide Server => _server ?? throw new Exception($"Server is null : you might need to set an ExecuteOrder greater than {ExecuteOrder()}!");

		ClientSide? _client;
		public ClientSide Client => _client ?? throw new Exception($"Client is null : you might need to set an ExecuteOrder greater than {ExecuteOrder()}!");

		//Submods need to set a greater execOrder for the modSystem they set templates in
		public override double ExecuteOrder() => 0.15;

		//Submods need to register their templates in Start so they are available when AssetsLoaded procs
		public override void Start(ICoreAPI api)
		{
			//Channel registration for both sides
			api.Network.RegisterChannel(Mod.Info.ModID)
				.RegisterMessageType<QuestAcceptedMessage>()
				.RegisterMessageType<QuestCompletedMessage>()
				.RegisterMessageType<QuestGiverInfoMessage>();

			if (api is ICoreServerAPI sapi)
			{
				//We need this to exist before AssetsLoaded procs
				_server = new(sapi, Mod);

				Server.AddActionTemplate("playsound", new QuestActionTemplate<SoundData>(ActionUtil.PlaySound));
				Server.AddActionTemplate("despawnquestgiver", new QuestActionTemplate<DelayData>(ActionUtil.DespawnQuestGiver));
				Server.AddActionTemplate("spawnall", new QuestActionTemplate<EntityData>(ActionUtil.SpawnEntities));
				Server.AddActionTemplate("spawnany", new QuestActionTemplate<EntityData>(ActionUtil.SpawnAnyOfEntities));
				Server.AddActionTemplate("particles", new QuestActionTemplate<GreySmokeData>(ActionUtil.SpawnParticles));
				Server.AddActionTemplate("recruit", new QuestActionTemplate(ActionUtil.RecruitEntity));
				Server.AddActionTemplate("heal", new QuestActionTemplate<HealData>(ActionUtil.Heal));
				Server.AddActionTemplate("addplayerattr", new QuestActionTemplate<AttributeData<string>>(ActionUtil.AddPlayerAttribute));
				Server.AddActionTemplate("removeplayerattr", new QuestActionTemplate<AttributeData>(ActionUtil.RemovePlayerAttribute));
				Server.AddActionTemplate("completequest", new QuestActionTemplate<QuestData>(ActionUtil.CompleteQuest));
				Server.AddActionTemplate("acceptquest", new QuestActionTemplate<QuestData>(ActionUtil.AcceptQuest));
				Server.AddActionTemplate("giveitem", new QuestActionTemplate<ItemData>(ActionUtil.GiveItem));
				Server.AddActionTemplate("addtraits", new QuestActionTemplate<TraitsData>(ActionUtil.AddTraits));
				Server.AddActionTemplate("removetraits", new QuestActionTemplate<TraitsData>(ActionUtil.RemoveTraits));


			}
		}

		public override void StartClientSide(ICoreClientAPI api)
		{
			_client = new(api, Mod);
		}

		public override void AssetsLoaded(ICoreAPI api)
		{
			if (api.Side != EnumAppSide.Server)
			{
				return;
			}

			foreach (var mod in api.ModLoader.Mods)
			{
				var templates = api.Assets
					.GetMany<List<QuestTemplate>>(mod.Logger, "config/quests", mod.Info.ModID)
					.SelectMany(entry => entry.Value);

				Server.AddQuestTemplates(templates);
			}
		}
	}

	#region Client-Server Messaging

	[ProtoContract]
	public class QuestAcceptedMessage(string id, long giverId) :
		QuestMessage(id, giverId)
	{ }

	[ProtoContract]
	public class QuestCompletedMessage(string id, long giverId) :
		QuestMessage(id, giverId)
	{ }

	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
	[ProtoInclude(10, typeof(QuestAcceptedMessage))]
	[ProtoInclude(11, typeof(QuestCompletedMessage))]
	public abstract class QuestMessage(string id, long giverId)
	{
		public string Id => id;
		public long GiverId => giverId;
	}

	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
	public class QuestGiverInfoMessage(long giverId, IEnumerable<string> availableQuestIds, IDictionary<string, bool> activeQuestData)
	{
		public long GiverId => giverId;
		public IEnumerable<string> AvailableQuestIds => availableQuestIds;
		public IDictionary<string, bool> ActiveQuestData => activeQuestData;
	}

	#endregion
}