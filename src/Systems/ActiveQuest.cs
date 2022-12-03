using System;
using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VsQuest
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ActiveQuest
    {
        public long questGiverId { get; set; }
        public string questId { get; set; }
        public List<EventTracker> killTrackers { get; set; } = new List<EventTracker>();
        public List<EventTracker> blockPlaceTrackers { get; set; } = new List<EventTracker>();
        public List<EventTracker> blockBreakTrackers { get; set; } = new List<EventTracker>();
        public void OnEntityKilled(string entityCode)
        {
            checkEventTrackers(killTrackers, entityCode);
        }

        public void OnBlockPlaced(string blockCode)
        {
            checkEventTrackers(blockPlaceTrackers, blockCode);
        }

        public void OnBlockBroken(string blockCode)
        {
            checkEventTrackers(blockBreakTrackers, blockCode);
        }

        private static void checkEventTrackers(List<EventTracker> trackers, string code)
        {
            foreach (var tracker in trackers)
            {
                if (trackerMatches(tracker, code))
                {
                    tracker.count++;
                }
            }
        }

        private static bool trackerMatches(EventTracker tracker, string code)
        {
            if (tracker.relevantCodes.Contains(code))
            {
                return true;
            }
            foreach (var candidate in tracker.relevantCodes)
            {
                if (candidate.EndsWith("*") && code.StartsWith(candidate.Remove(candidate.Length - 1)))
                {
                    return true;
                }
            }
            return false;
        }

        public bool isCompletable(IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            var quest = questSystem.questRegistry[questId];
            var activeActionObjectives = quest.actionObjectives.ConvertAll<ActiveActionObjective>(objective => questSystem.actionObjectiveRegistry[objective.id]);
            bool completable = true;
            for (int i = 0; i < quest.blockPlaceObjectives.Count; i++)
            {
                completable &= quest.blockPlaceObjectives[i].demand <= blockPlaceTrackers[i].count;
            }
            for (int i = 0; i < quest.blockBreakObjectives.Count; i++)
            {
                completable &= quest.blockBreakObjectives[i].demand <= blockBreakTrackers[i].count;
            }
            for (int i = 0; i < quest.killObjectives.Count; i++)
            {
                completable &= quest.killObjectives[i].demand <= killTrackers[i].count;
            }
            foreach (var gatherObjective in quest.gatherObjectives)
            {
                int itemsFound = itemsGathered(byPlayer, gatherObjective);
                completable &= itemsFound >= gatherObjective.demand;
            }
            for (int i = 0; i < activeActionObjectives.Count; i++)
            {
                completable &= activeActionObjectives[i].isCompletable(byPlayer, quest.actionObjectives[i].args);
            }
            return completable;
        }

        public void completeQuest(IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            var quest = questSystem.questRegistry[questId];
            foreach (var gatherObjective in quest.gatherObjectives)
            {
                handOverItems(byPlayer, gatherObjective);
            }
        }

        public List<int> trackerProgress()
        {
            var result = new List<int>();
            foreach (var trackerList in new List<EventTracker>[] { killTrackers, blockPlaceTrackers, blockBreakTrackers })
            {
                if (trackerList != null)
                {
                    result.AddRange(trackerList.ConvertAll<int>(tracker => tracker.count));
                }
            }
            return result;
        }

        public List<int> gatherProgress(IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            var quest = questSystem.questRegistry[questId];
            return quest.gatherObjectives.ConvertAll<int>(gatherObjective => itemsGathered(byPlayer, gatherObjective));
        }

        public List<int> actionProgress(IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            var quest = questSystem.questRegistry[questId];
            var activeActionObjectives = quest.actionObjectives.ConvertAll<ActiveActionObjective>(objective => questSystem.actionObjectiveRegistry[objective.id]);
            List<int> result = new List<int>();
            for (int i = 0; i < activeActionObjectives.Count; i++)
            {
                result.AddRange(activeActionObjectives[i].progress(byPlayer, quest.actionObjectives[i].args));
            }
            return result;
        }

        public List<int> progress(IPlayer byPlayer)
        {
            var progress = gatherProgress(byPlayer);
            progress.AddRange(trackerProgress());
            progress.AddRange(actionProgress(byPlayer));
            return progress;
        }

        public int itemsGathered(IPlayer byPlayer, Objective gatherObjective)
        {
            int itemsFound = 0;

            byPlayer.Entity.WalkInventory((slot) =>
            {
                if (slot is ItemSlotCreative || !(slot.Inventory is InventoryBasePlayer)) return true;

                if (gatherObjective.validCodes.Contains(slot?.Itemstack?.Collectible?.Code?.Path))
                {
                    itemsFound += slot.Itemstack.StackSize;
                }

                return true;
            });

            return itemsFound;
        }

        public void handOverItems(IPlayer byPlayer, Objective gatherObjective)
        {
            int itemsFound = 0;
            foreach (var inventory in byPlayer.InventoryManager.Inventories.Values)
            {
                if (inventory.ClassName == GlobalConstants.creativeInvClassName)
                {
                    continue;
                }
                foreach (var slot in inventory)
                {
                    if (gatherObjective.validCodes.Contains(slot?.Itemstack?.Collectible?.Code?.Path))
                    {
                        var stack = slot.TakeOut(Math.Min(slot.Itemstack.StackSize, gatherObjective.demand - itemsFound));
                        slot.MarkDirty();
                        itemsFound += stack.StackSize;
                    }
                    if (itemsFound > gatherObjective.demand) { return; }
                }
            }
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class EventTracker
    {
        public HashSet<string> relevantCodes { get; set; } = new HashSet<string>();
        public int count { get; set; }
    }
}