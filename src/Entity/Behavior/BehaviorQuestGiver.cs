using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using System.Security.Cryptography;
using Vintagestory.API.Client;

namespace VsQuest
{
    public class EntityBehaviorQuestGiver : EntityBehavior
    {
        private string[] quests;
        private bool selectRandom;
        private int selectRandomCount;

        public EntityBehaviorQuestGiver(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            selectRandom = attributes["selectrandom"].AsBool();
            selectRandomCount = attributes["selectrandomcount"].AsInt(1);
            quests = attributes["quests"].AsArray<string>();

            // simple randomizer that will always select the same quests for each entityId
            if (selectRandom)
            {
                int seed = unchecked((int)entity.EntityId);
                var questList = new List<string>(quests);
                var resultList = new List<string>();
                for (int i = 0; i < Math.Min(selectRandomCount, quests.Length); i++)
                {
                    seed = (seed * 7 + 7) % questList.Count;
                    resultList.Add(questList[seed]);
                    questList.RemoveAt(seed);
                }
                quests = resultList.ToArray();
            }
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);
            if (entity.Alive && entity.Api is ICoreServerAPI sapi && byEntity is EntityPlayer player && mode == EnumInteractMode.Interact && player.Controls.Sneak)
            {
                var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
                var activeQuests = questSystem.getPlayerQuests(player.PlayerUID, sapi).FindAll(quest => quest.questGiverId == entity.EntityId);
                var availableQuestIds = new List<string>();
                foreach (var questId in quests)
                {
                    var quest = questSystem.questRegistry[questId];
                    var key = quest.perPlayer ? String.Format("lastaccepted-{0}-{1}", questId, (byEntity as EntityPlayer)?.PlayerUID) : String.Format("lastaccepted-{0}", questId);
                    if (entity.WatchedAttributes.GetDouble(key, -quest.cooldown) + quest.cooldown < sapi.World.Calendar.TotalDays
                            && activeQuests.Find(activeQuest => activeQuest.questId == questId && activeQuest.questGiverId == entity.EntityId) == null
                            && predecessorsCompleted(quest, player.PlayerUID))
                    {
                        availableQuestIds.Add(questId);
                    }
                }
                var message = new QuestInfoMessage()
                {
                    questGiverId = entity.EntityId,
                    availableQestIds = availableQuestIds,
                    activeQuests = activeQuests
                };

                sapi.Network.GetChannel("vsquest").SendPacket<QuestInfoMessage>(message, player.Player as IServerPlayer);
            }
        }

        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
        {
            if (entity.Alive)
            {
                return new WorldInteraction[] {
                    new WorldInteraction(){
                        ActionLangCode = "vsquest:access-quests",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "sneak"
                    }
                };
            }
            else { return base.GetInteractionHelp(world, es, player, ref handled); }
        }

        private bool predecessorsCompleted(Quest quest, string playerUID)
        {
            var completedQuests = new List<string>(entity.WatchedAttributes.GetStringArray(String.Format("playercompleted-{0}", playerUID), new string[0]));
            return String.IsNullOrEmpty(quest.predecessor)
                || completedQuests.Contains(quest.predecessor);
        }

        public override string PropertyName() => "questgiver";
    }
}