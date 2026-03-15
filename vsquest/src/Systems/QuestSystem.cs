using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
        public Dictionary<string, Quest> QuestRegistry { get; private set; } = [];
        public Dictionary<string, QuestAction> ActionRegistry { get; private set; } = [];
        public Dictionary<string, ActiveActionObjective> ActionObjectiveRegistry { get; private set; } = [];

        private ConcurrentDictionary<string, List<ActiveQuest>> _playerQuests = [];
        
        private QuestConfig? _config;
        public QuestConfig Config {
            get
            {
                _config ??= new QuestConfig();
                return _config;
            }
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterEntityBehaviorClass("questgiver", typeof(EntityBehaviorQuestGiver));

            api.RegisterItemClass("ItemDebugTool", typeof(ItemDebugTool));

            ActionObjectiveRegistry.Add("plantflowers", new NearbyFlowersActionObjective());
            ActionObjectiveRegistry.Add("hasAttribute", new PlayerHasAttributeActionObjective());

            bool create = false;

            try
            {
                _config = api.LoadModConfig<QuestConfig>("questconfig.json");
                create = _config is null;

                if (create)
                {
                    Mod.Logger.Notification("Config file not found : falling back to default settings");
                }
                else
                {
                    Mod.Logger.Notification("Config file successfully loaded");
                }
            }
            catch (Exception ex)
            {
                Mod.Logger.Error($"Config file parsing failed due to : {ex.Message}");
                Mod.Logger.Warning("Falling back to default settings");
            }
            finally
            {
                if (create)
                {
                    api.StoreModConfig(Config, "questconfig.json");
                }
            }
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

            ActionRegistry.Add("despawnquestgiver", (api, message, byPlayer, args) => api.World.RegisterCallback(dt => api.World.GetEntityById(message.QuestGiverId).Die(EnumDespawnReason.Removed), int.Parse(args[0])));
            ActionRegistry.Add("playsound", (api, message, byPlayer, args) => api.World.PlaySoundFor(new AssetLocation(args[0]), byPlayer));
            ActionRegistry.Add("spawnentities", ActionUtil.SpawnEntities);
            ActionRegistry.Add("spawnany", ActionUtil.SpawnAnyOfEntities);
            ActionRegistry.Add("spawnsmoke", ActionUtil.SpawnSmoke);
            ActionRegistry.Add("recruitentity", ActionUtil.RecruitEntity);
            ActionRegistry.Add("healplayer", (api, message, byPlayer, args) => byPlayer.Entity.ReceiveDamage(new DamageSource() { Type = EnumDamageType.Heal }, 100));
            ActionRegistry.Add("addplayerattribute", (api, message, byPlayer, args) => byPlayer.Entity.WatchedAttributes.SetString(args[0], args[1]));
            ActionRegistry.Add("removeplayerattribute", (api, message, byPlayer, args) => byPlayer.Entity.WatchedAttributes.RemoveAttribute(args[0]));
            ActionRegistry.Add("completequest", ActionUtil.CompleteQuest);
            ActionRegistry.Add("acceptquest", (api, message, byPlayer, args) => OnQuestAccepted(byPlayer, new QuestAcceptedMessage() { QuestGiverId = long.Parse(args[0]), QuestId = args[1] }, api));
            ActionRegistry.Add("giveitem", ActionUtil.GiveItem);
            ActionRegistry.Add("addtraits", ActionUtil.AddTraits);
            ActionRegistry.Add("removetraits", ActionUtil.RemoveTraits);

            sapi.Event.GameWorldSave += () => OnSave(sapi);
            sapi.Event.PlayerDisconnect += player => OnDisconnect(player, sapi);
            sapi.Event.OnEntityDeath += (entity, dmgSource) => OnEntityDeath(entity, dmgSource, sapi);
            sapi.Event.DidBreakBlock += (byPlayer, blockId, blockSel) => getPlayerQuests(byPlayer.PlayerUID, sapi).ForEach(quest => quest.OnBlockBroken(sapi.World.GetBlock(blockId).Code.Path));
            sapi.Event.DidPlaceBlock += (byPlayer, oldBlockId, blockSel, itemstack) => getPlayerQuests(byPlayer.PlayerUID, sapi).ForEach(quest => quest.OnBlockPlaced(sapi.World.BlockAccessor.GetBlock(blockSel.Position).Code.Path));
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            base.AssetsLoaded(api);
            foreach (var mod in api.ModLoader.Mods)
            {
                api.Assets
                    .GetMany<List<Quest>>(api.Logger, "config/quests", mod.Info.ModID)
                    .SelectMany(pair => pair.Value)
                    .Foreach(quest => QuestRegistry.Add(quest.Id, quest));
            }
        }

        public List<ActiveQuest> getPlayerQuests(string playerUID, ICoreServerAPI sapi)
        {
            return _playerQuests.GetOrAdd(playerUID, val => loadPlayerQuests(sapi, val));
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
            if (_playerQuests.TryGetValue(byPlayer.PlayerUID, out var activeQuests))
            {
                savePlayerQuests(sapi, byPlayer.PlayerUID, activeQuests);
                _playerQuests.Remove(byPlayer.PlayerUID);
            }
        }

        private void OnSave(ICoreServerAPI sapi)
        {
            foreach (var player in _playerQuests)
            {
                savePlayerQuests(sapi, player.Key, player.Value);
            }
        }

        private void savePlayerQuests(ICoreServerAPI sapi, string playerUID, List<ActiveQuest> activeQuests)
        {
            sapi.WorldManager.SaveGame.StoreData($"quests-{playerUID}", activeQuests);
        }
        private List<ActiveQuest> loadPlayerQuests(ICoreServerAPI sapi, string playerUID)
        {
            try
            {
                return sapi.WorldManager.SaveGame.GetData<List<ActiveQuest>>($"quests-{playerUID}", []);
            }
            catch (ProtoException)
            {
                sapi.Logger.Error($"Could not load quests for player with id {playerUID}, corrupted quests will be deleted.");
                return new List<ActiveQuest>();
            }
        }

        private void OnQuestAccepted(IServerPlayer fromPlayer, QuestAcceptedMessage message, ICoreServerAPI sapi)
        {
            var quest = QuestRegistry[message.QuestId];
            var killTrackers = new List<EventTracker>();

            foreach (var objective in quest.KillObjectives)
            {
                var tracker = new EventTracker()
                {
                    Count = 0,
                    RelevantCodes = [..objective.ValidCodes]
                };
                killTrackers.Add(tracker);
            }

            var blockPlaceTrackers = new List<EventTracker>();

            foreach (var objective in quest.BlockPlaceObjectives)
            {
                var tracker = new EventTracker()
                {
                    Count = 0,
                    RelevantCodes = [..objective.ValidCodes]
                };
                blockPlaceTrackers.Add(tracker);
            }

            var blockBreakTrackers = new List<EventTracker>();

            foreach (var objective in quest.BlockBreakObjectives)
            {
                var tracker = new EventTracker()
                {
                    Count = 0,
                    RelevantCodes = [..objective.ValidCodes]
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
            getPlayerQuests(fromPlayer.PlayerUID, sapi).Add(activeQuest);

            var questgiver = sapi.World.GetEntityById(message.QuestGiverId);
            var key = quest.PerPlayer ? String.Format("lastaccepted-{0}-{1}", quest.Id, fromPlayer.PlayerUID) : String.Format("lastaccepted-{0}", quest.Id);
            questgiver.WatchedAttributes.SetDouble(key, sapi.World.Calendar.TotalDays);
            questgiver.WatchedAttributes.MarkPathDirty(key);

            foreach (var action in quest.OnAcceptedActions)
            {
                try
                {
                    ActionRegistry[action.Id].Invoke(sapi, message, fromPlayer, action.Args);
                }
                catch (Exception ex)
                {
                    sapi.Logger.Error(string.Format("Action {0} caused an Error in Quest {1}. The Error had the following message: {2}\n Stacktrace:", action.Id, quest.Id, ex.Message, ex.StackTrace));
                    sapi.SendMessage(fromPlayer, GlobalConstants.InfoLogChatGroup, string.Format("An error occurred during quest {0}, please check the server logs for more details.", quest.Id), EnumChatType.Notification);
                }
            }
        }

        public void OnQuestCompleted(IServerPlayer fromPlayer, QuestCompletedMessage message, ICoreServerAPI sapi)
        {
            var playerQuests = getPlayerQuests(fromPlayer.PlayerUID, sapi);
            var activeQuest = playerQuests.Find(q => q.QuestId == message.QuestId && q.QuestGiverId == message.QuestGiverId);

            if (activeQuest is null)
            {
                Mod.Logger.Error($@"Completed quest not found in active quests : ""{message.QuestId}"" from ""{message.QuestGiverId}""");
                return;
            }

            if (activeQuest.isCompletable(fromPlayer))
            {
                activeQuest.completeQuest(fromPlayer);
                playerQuests.Remove(activeQuest);
                var questgiver = sapi.World.GetEntityById(message.QuestGiverId);
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
            var quest = QuestRegistry[message.QuestId];
            foreach (var reward in quest.ItemRewards)
            {
                CollectibleObject? item = sapi.World.GetItem(new AssetLocation(reward.ItemCode));
                item ??= sapi.World.GetBlock(new AssetLocation(reward.ItemCode));

                if (item == null) continue;

                var stack = new ItemStack(item, reward.Amount);
                if (!fromPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    sapi.World.SpawnItemEntity(stack, questgiver.Pos.XYZ);
                }
            }
            List<RandomItem> randomItems = quest.RandomItemRewards.Items;
            for (int i = 0; i < quest.RandomItemRewards.SelectAmount; i++)
            {
                if (randomItems.Count <= 0) break;
                var randomItem = randomItems[sapi.World.Rand.Next(0, randomItems.Count)];
                randomItems.Remove(randomItem);
                CollectibleObject? item = sapi.World.GetItem(new AssetLocation(randomItem.ItemCode));
                if (item == null)
                {
                    item = sapi.World.GetBlock(new AssetLocation(randomItem.ItemCode));
                }
                var stack = new ItemStack(item, sapi.World.Rand.Next(randomItem.MinAmount, randomItem.MaxAmount + 1));
                if (!fromPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    sapi.World.SpawnItemEntity(stack, questgiver.Pos.XYZ);
                }
            }
            foreach (var action in quest.ActionRewards)
            {
                try
                {
                    ActionRegistry[action.Id].Invoke(sapi, message, fromPlayer, action.Args);
                }
                catch (Exception ex)
                {
                    sapi.Logger.Error(string.Format("Action {0} caused an Error in Quest {1}. The Error had the following message: {2}\n Stacktrace:", action.Id, quest.Id, ex.Message, ex.StackTrace));
                    sapi.SendMessage(fromPlayer, GlobalConstants.InfoLogChatGroup, string.Format("An error occurred during quest {0}, please check the server logs for more details.", quest.Id), EnumChatType.Notification);
                }
            }
        }

        private static void markQuestCompleted(IServerPlayer fromPlayer, QuestCompletedMessage message, Entity questgiver)
        {
            var completedQuests = new HashSet<string>(questgiver.WatchedAttributes.GetStringArray($"playercompleted-{fromPlayer.PlayerUID}", []))
            {
                message.QuestId
            };
            var completedQuestsArray = new string[completedQuests.Count];
            completedQuests.CopyTo(completedQuestsArray);
            questgiver.WatchedAttributes.SetStringArray($"playercompleted-{fromPlayer.PlayerUID}", completedQuestsArray);
        }

        private void OnQuestInfoMessage(QuestInfoMessage message, ICoreClientAPI capi)
        {
            new QuestSelectGui(capi, message.QuestGiverId, message.AvailableQestIds, message.ActiveQuests, Config).TryOpen();
        }
    }

    public class QuestConfig
    {
        public bool CloseGuiAfterAcceptingAndCompleting = true;
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
        public string QuestId { get; set; } = string.Empty;

        public long QuestGiverId { get; set; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class QuestInfoMessage
    {
        public long QuestGiverId { get; set; }
        public List<string> AvailableQestIds { get; set; } = [];
        public List<ActiveQuest> ActiveQuests { get; set; } = [];
    }
}