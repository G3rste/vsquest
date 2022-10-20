using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VsQuest
{
    public delegate void QuestAction(ICoreServerAPI sapi, QuestMessage message, IServerPlayer player, string[] args);
    public class QuestSystem : ModSystem
    {
        public Dictionary<string, Quest> questRegistry { get; private set; } = new Dictionary<string, Quest>();
        public Dictionary<string, QuestAction> actionRegistry { get; private set; } = new Dictionary<string, QuestAction>();
        public Dictionary<string, ActiveActionObjective> actionObjectiveRegistry { get; private set; } = new Dictionary<string, ActiveActionObjective>();
        private ConcurrentDictionary<string, List<ActiveQuest>> playerQuests = new ConcurrentDictionary<string, List<ActiveQuest>>();
        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterEntityBehaviorClass("questgiver", typeof(EntityBehaviorQuestGiver));

            api.RegisterItemClass("ItemDebugTool", typeof(ItemDebugTool));

            actionObjectiveRegistry.Add("plantflowers", new NearbyFlowersActionObjective());
            actionObjectiveRegistry.Add("hasAttribute", new PlayerHasAttributeActionObjective());
        }

        public override void StartClientSide(ICoreClientAPI capi)
        {
            base.StartClientSide(capi);

            capi.Network.RegisterChannel("vsquest")
                .RegisterMessageType<QuestAcceptedMessage>()
                .RegisterMessageType<QuestCompletedMessage>()
                .RegisterMessageType<QuestInfoMessage>().SetMessageHandler<QuestInfoMessage>(message => OnQuestInfoMessage(message, capi));
        }

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            base.StartServerSide(sapi);

            sapi.Network.RegisterChannel("vsquest")
                .RegisterMessageType<QuestAcceptedMessage>().SetMessageHandler<QuestAcceptedMessage>((player, message) => OnQuestAccepted(player, message, sapi))
                .RegisterMessageType<QuestCompletedMessage>().SetMessageHandler<QuestCompletedMessage>((player, message) => OnQuestCompleted(player, message, sapi))
                .RegisterMessageType<QuestInfoMessage>();

            actionRegistry.Add("despawnquestgiver", (api, message, byPlayer, args) => api.World.RegisterCallback(dt => api.World.GetEntityById(message.questGiverId).Die(EnumDespawnReason.Removed), int.Parse(args[0])));
            actionRegistry.Add("playsound", (api, message, byPlayer, args) => api.World.PlaySoundFor(new AssetLocation(args[0]), byPlayer));
            actionRegistry.Add("spawnentities", ActionUtil.SpawnEntities);
            actionRegistry.Add("spawnany", ActionUtil.SpawnAnyOfEntities);
            actionRegistry.Add("recruitentity", ActionUtil.RecruitEntity);
            actionRegistry.Add("addplayerattribute", (api, message, byPlayer, args) => byPlayer.Entity.WatchedAttributes.SetString(args[0], args[1]));
            actionRegistry.Add("removeplayerattribute", (api, message, byPlayer, args) => byPlayer.Entity.WatchedAttributes.RemoveAttribute(args[0]));
            actionRegistry.Add("completequest", (api, message, byPlayer, args) => OnQuestCompleted(byPlayer, new QuestCompletedMessage() { questGiverId = long.Parse(args[0]), questId = args[1] }, api));
            actionRegistry.Add("acceptquest", (api, message, byPlayer, args) => OnQuestAccepted(byPlayer, new QuestAcceptedMessage() { questGiverId = long.Parse(args[0]), questId = args[1] }, api));
            actionRegistry.Add("giveitem", ActionUtil.GiveItem);

            sapi.Event.GameWorldSave += () => OnSave(sapi);
            sapi.Event.PlayerDisconnect += player => OnDisconnect(player, sapi);
            sapi.Event.OnEntityDeath += (entity, dmgSource) => OnEntityDeath(entity, dmgSource, sapi);
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            base.AssetsLoaded(api);
            foreach (var mod in api.ModLoader.Mods)
            {
                List<Quest> quests = api.Assets.TryGet(new AssetLocation(mod.Info.ModID, "config/quests.json"))?.ToObject<List<Quest>>();
                if (quests == null) continue;

                foreach (var quest in quests)
                {
                    questRegistry.Add(quest.id, quest);
                }
            }
        }

        public List<ActiveQuest> getPlayerQuests(string playerUID, ICoreServerAPI sapi)
        {
            return playerQuests.GetOrAdd(playerUID, (val) => loadPlayerQuests(sapi, val));
        }

        private void OnEntityDeath(Entity entity, DamageSource damageSource, ICoreServerAPI sapi)
        {
            if (damageSource?.SourceEntity is EntityPlayer player)
            {
                getPlayerQuests(player.PlayerUID, sapi).ForEach(quest => quest.OnEntityKilled(entity.Code.Path));
            }
        }

        private void OnDisconnect(IServerPlayer byPlayer, ICoreServerAPI sapi)
        {
            if (playerQuests.TryGetValue(byPlayer.PlayerUID, out var activeQuests))
            {
                savePlayerQuests(sapi, byPlayer.PlayerUID, activeQuests);
                playerQuests.Remove(byPlayer.PlayerUID);
            }
        }

        private void OnSave(ICoreServerAPI sapi)
        {
            foreach (var player in playerQuests)
            {
                savePlayerQuests(sapi, player.Key, player.Value);
            }
        }

        private void savePlayerQuests(ICoreServerAPI sapi, string playerUID, List<ActiveQuest> activeQuests)
        {
            sapi.WorldManager.SaveGame.StoreData<List<ActiveQuest>>(String.Format("quests-{0}", playerUID), activeQuests);
        }
        private List<ActiveQuest> loadPlayerQuests(ICoreServerAPI sapi, string playerUID)
        {
            return sapi.WorldManager.SaveGame.GetData<List<ActiveQuest>>(String.Format("quests-{0}", playerUID), new List<ActiveQuest>());
        }

        private void OnQuestAccepted(IServerPlayer fromPlayer, QuestAcceptedMessage message, ICoreServerAPI sapi)
        {
            sapi.Logger.Error(message.questId);
            var quest = questRegistry[message.questId];
            var killTrackers = new List<EntityKillTracker>();
            foreach (var killObjective in quest.killObjectives)
            {
                var tracker = new EntityKillTracker()
                {
                    kills = 0,
                    relevantEntityCodes = new HashSet<string>(killObjective.validCodes)
                };
                killTrackers.Add(tracker);
            }
            foreach (var action in quest.onAcceptedActions)
            {
                try
                {
                    actionRegistry[action.id].Invoke(sapi, message, fromPlayer, action.args);
                }
                catch (Exception ex)
                {
                    sapi.Logger.Error(string.Format("Action {0} caused an Error in Quest {1}. The Error had the following message: {2}\n Stacktrace:", action.id, quest.id, ex.Message, ex.StackTrace));
                    sapi.SendMessage(fromPlayer, GlobalConstants.InfoLogChatGroup, string.Format("An error occurred during quest {0}, please check the server logs for more details.", quest.id), EnumChatType.Notification);
                }
            }
            var activeQuest = new ActiveQuest()
            {
                questGiverId = message.questGiverId,
                questId = message.questId,
                killTrackers = killTrackers
            };
            getPlayerQuests(fromPlayer.PlayerUID, sapi).Add(activeQuest);
            var questgiver = sapi.World.GetEntityById(message.questGiverId);
            var key = quest.perPlayer ? String.Format("lastaccepted-{0}-{1}", quest.id, fromPlayer.PlayerUID) : String.Format("lastaccepted-{0}", quest.id);
            questgiver.WatchedAttributes.SetDouble(key, sapi.World.Calendar.TotalDays);
            questgiver.WatchedAttributes.MarkPathDirty(key);
        }

        private void OnQuestCompleted(IServerPlayer fromPlayer, QuestCompletedMessage message, ICoreServerAPI sapi)
        {
            var playerQuests = getPlayerQuests(fromPlayer.PlayerUID, sapi);
            var activeQuest = playerQuests.Find(item => item.questId == message.questId && item.questGiverId == message.questGiverId);
            if (activeQuest.isCompletable(fromPlayer))
            {
                activeQuest.completeQuest(fromPlayer);
                playerQuests.Remove(activeQuest);
                var questgiver = sapi.World.GetEntityById(message.questGiverId);
                rewardPlayer(fromPlayer, message, sapi, questgiver);
                markQuestCompleted(fromPlayer, message, questgiver);
            }
            else
            {
                sapi.SendMessage(fromPlayer, GlobalConstants.InfoLogChatGroup, "Something went wrong, the quest could not be completed", EnumChatType.Notification);
            }
        }

        private void rewardPlayer(IServerPlayer fromPlayer, QuestCompletedMessage message, ICoreServerAPI sapi, Entity questgiver)
        {
            var quest = questRegistry[message.questId];
            foreach (var reward in quest.itemRewards)
            {
                CollectibleObject item = sapi.World.GetItem(new AssetLocation(reward.itemCode));
                if (item == null)
                {
                    item = sapi.World.GetBlock(new AssetLocation(reward.itemCode));
                }
                var stack = new ItemStack(item, reward.amount);
                if (!fromPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    sapi.World.SpawnItemEntity(stack, questgiver.ServerPos.XYZ);
                }
            }
            List<RandomItem> randomItems = quest.randomItemRewards.items;
            for (int i = 0; i < quest.randomItemRewards.selectAmount; i++)
            {
                if (randomItems.Count <= 0) break;
                var randomItem = randomItems[sapi.World.Rand.Next(0, randomItems.Count)];
                randomItems.Remove(randomItem);
                CollectibleObject item = sapi.World.GetItem(new AssetLocation(randomItem.itemCode));
                if (item == null)
                {
                    item = sapi.World.GetBlock(new AssetLocation(randomItem.itemCode));
                }
                var stack = new ItemStack(item, sapi.World.Rand.Next(randomItem.minAmount, randomItem.maxAmount + 1));
                if (!fromPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    sapi.World.SpawnItemEntity(stack, questgiver.ServerPos.XYZ);
                }
            }
            foreach (var action in quest.actionRewards)
            {
                try
                {
                    actionRegistry[action.id].Invoke(sapi, message, fromPlayer, action.args);
                }
                catch (Exception ex)
                {
                    sapi.Logger.Error(string.Format("Action {0} caused an Error in Quest {1}. The Error had the following message: {2}\n Stacktrace:", action.id, quest.id, ex.Message, ex.StackTrace));
                    sapi.SendMessage(fromPlayer, GlobalConstants.InfoLogChatGroup, string.Format("An error occurred during quest {0}, please check the server logs for more details.", quest.id), EnumChatType.Notification);
                }
            }
        }

        private static void markQuestCompleted(IServerPlayer fromPlayer, QuestCompletedMessage message, Entity questgiver)
        {
            var completedQuests = new HashSet<string>(questgiver.WatchedAttributes.GetStringArray(String.Format("playercompleted-{0}", fromPlayer.PlayerUID), new string[0]));
            completedQuests.Add(message.questId);
            var completedQuestsArray = new string[completedQuests.Count];
            completedQuests.CopyTo(completedQuestsArray);
            questgiver.WatchedAttributes.SetStringArray(String.Format("playercompleted-{0}", fromPlayer.PlayerUID), completedQuestsArray);
        }

        private void OnQuestInfoMessage(QuestInfoMessage message, ICoreClientAPI capi)
        {
            new QuestSelectGui(capi, message.questGiverId, message.availableQestIds, message.activeQuests).TryOpen();
        }
    }

    [ProtoContract]
    public class QuestAcceptedMessage : QuestMessage
    {
    }

    [ProtoContract]
    public class QuestCompletedMessage : QuestMessage
    {
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [ProtoInclude(10, typeof(QuestAcceptedMessage))]
    [ProtoInclude(11, typeof(QuestCompletedMessage))]
    public abstract class QuestMessage
    {
        public string questId { get; set; }

        public long questGiverId { get; set; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class QuestInfoMessage
    {
        public long questGiverId { get; set; }
        public List<string> availableQestIds { get; set; }
        public List<ActiveQuest> activeQuests { get; set; }
    }
}