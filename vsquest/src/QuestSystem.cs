using Newtonsoft.Json.Linq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using VSQuest.Client;
using VSQuest.Containers;
using VSQuest.Model.Server.Data;
using VSQuest.Server;
using VSQuest.Server.Model.Action;
using VSQuest.Templates;

namespace VSQuest
{
	public class QuestContainer(string id, IQuestTemplate template) : Container<IQuestTemplate>(id, template);

	public class FileInflator(JToken content, AssetLocation location, QuestTemplateSystem templateSystem)
	{
		public List<QuestContainer> Inflate()
		{
			return content.Type switch
			{
				JTokenType.Object => Inflate((JObject)content),
				JTokenType.Array => Inflate((JArray)content),
				_ => throw new Exception($"Quest template files should contain else :\na single quest template object\nan array of template objects\nan object whose properties are template objects with their ids as property names\nan array of these")
			};
		}

		List<QuestContainer> Inflate(JArray arr)
		{
			if (!arr.HasValues)
			{
				throw new InflateException("Root array is empty");
			}

			var questTemplates = new List<IQuestTemplate>();
			for (var child = arr.First; child != null; child = child.Next)
			{
				context.QuestTemplateArrayIndex.Incr();
				try
				{
					if (child is JObject obj)
					{
						questTemplates.AddRange(Inflate(obj));
						continue;
					}
					throw new InflateException($"Quest templates should be objects");
				}
				catch (InflateException ex)
				{
					context.Logger.Error(ex);
				}
				catch (Exception ex)
				{
					context.Logger.Error("Implementation mistake : report to dev!");
					context.Logger.Error(ex);
				}
			}
			context.QuestTemplateArrayIndex.Reset();
			return questTemplates;
		}

		static List<QuestContainer> Inflate(JObject obj)
		{
			var id = obj.Value<string>("id");
			if (id != null)
			{
				return [Inflate(id, obj)];
			}

			var questTemplates = new List<IQuestTemplate>();
			foreach (var property in obj.Properties())
			{
				context.QuestTemplateRegistryIndex.Incr();
				try
				{
					if (property.Value is JObject val)
					{
						questTemplates.Add(Inflate(property.Name, val));
						continue;
					}
					throw new InflateException($"Quest templates should be objects");

				}
				catch (Exception ex)
				{
					context.Logger.Error(ex);
				}
			}
			context.QuestTemplateRegistryIndex.Reset();
			return questTemplates;
		}

		QuestContainer Inflate(string id, JObject obj)
		{
			if (id.IsWhiteSpace())
			{
				throw new Exception("Each quest template requires a valid id");
			}

			return new(id, new QuestTemplate(
				ObjectiveContainer.Inflate(obj.Property("objectives")),
				RewardContainer.Inflate(obj.Property("rewards")),
				LockContainer.Inflate(obj.Property("locks")),
				QuestActionContainer.Inflate(obj.Property("onAcceptActions")),
				QuestActionContainer.Inflate(obj.Property("onCompleteActions")),
				ObjectiveActionContainer.Inflate(obj.Property("onProgressActions")),
				ObjectiveActionContainer.Inflate(obj.Property("onFailActions"))
			));
		}


	}

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
				.RegisterMessageType<AcceptQuestCommand>()
				.RegisterMessageType<CompleteQuestCommand>()
				.RegisterMessageType<QuestGiverInfoResponse>();

			if (api is ICoreServerAPI sapi)
			{
				//We need this to exist before AssetsLoaded procs
				_server = new(sapi, Mod);

				Server.AddTemplate<IQuestActionTemplate>("playsound",			new QuestActionTemplate<SoundData>(ActionUtil.PlaySound));
				Server.AddTemplate<IQuestActionTemplate>("despawnquestgiver",	new QuestActionTemplate<DelayData>(ActionUtil.DespawnQuestGiver));
				Server.AddTemplate<IQuestActionTemplate>("spawnall",			new QuestActionTemplate<EntityData>(ActionUtil.SpawnEntities));
				Server.AddTemplate<IQuestActionTemplate>("spawnany",			new QuestActionTemplate<EntityData>(ActionUtil.SpawnAnyOfEntities));
				Server.AddTemplate<IQuestActionTemplate>("particles",			new QuestActionTemplate<GreySmokeData>(ActionUtil.SpawnParticles));
				Server.AddTemplate<IQuestActionTemplate>("recruit",				new QuestActionTemplate(ActionUtil.RecruitEntity));
				Server.AddTemplate<IQuestActionTemplate>("heal",				new QuestActionTemplate<HealData>(ActionUtil.Heal));
				Server.AddTemplate<IQuestActionTemplate>("addplayerattr",		new QuestActionTemplate<AttributeData<string>>(ActionUtil.AddPlayerAttribute));
				Server.AddTemplate<IQuestActionTemplate>("removeplayerattr",	new QuestActionTemplate<AttributeData>(ActionUtil.RemovePlayerAttribute));
				Server.AddTemplate<IQuestActionTemplate>("completequest",		new QuestActionTemplate<QuestData>(ActionUtil.CompleteQuest));
				Server.AddTemplate<IQuestActionTemplate>("acceptquest",			new QuestActionTemplate<QuestData>(ActionUtil.AcceptQuest));
				Server.AddTemplate<IQuestActionTemplate>("giveitem",			new QuestActionTemplate<ItemData>(ActionUtil.GiveItem));
				Server.AddTemplate<IQuestActionTemplate>("addtraits",			new QuestActionTemplate<TraitsData>(ActionUtil.AddTraits));
				Server.AddTemplate<IQuestActionTemplate>("removetraits",		new QuestActionTemplate<TraitsData>(ActionUtil.RemoveTraits));

				Server.AddTemplate<IObjectiveTemplate>("kill", new KillObjectiveTemplate());
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
				var assets = api.Assets
					.GetMany<JToken>(mod.Logger, "config/quests", mod.Info.ModID);

				var context = new InflationContext(mod.Logger, Server.Templates);
				foreach(var (location, json) in assets)
				{
					context.Location = location;
					Server.AddQuestTemplates(QuestTemplate.Inflate(json, context));
				}
			}
		}

		public override void Dispose()
		{
			_client?.Dispose();
			_server?.Dispose();
		}
	}

	//TODO rework messages and add a bunch
	#region Client-Server Messaging

	[ProtoContract]
	public record AcceptQuestCommand(string Id, long? GiverId) : QuestSummary(Id, GiverId);

	[ProtoContract]
	public record CompleteQuestCommand(string Id, long? GiverId) : QuestSummary(Id, GiverId);

	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
	[ProtoInclude(10, typeof(AcceptQuestCommand))]
	[ProtoInclude(11, typeof(CompleteQuestCommand))]
	public record QuestSummary(string Id, long? GiverId);

	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
	public record QuestGiverInfoResponse(long GiverId, IEnumerable<string> AvailableQuestIds, IDictionary<string, bool> ActiveQuestData);

	#endregion
}