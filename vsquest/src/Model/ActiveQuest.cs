using System;
using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VSQuest
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ActiveQuest
    {
        public long QuestGiverId { get; set; }
        public string QuestId { get; set; } = string.Empty;
        public List<EventTracker> KillTrackers { get; set; } = [];
        public List<EventTracker> BlockPlaceTrackers { get; set; } = [];
        public List<EventTracker> BlockBreakTrackers { get; set; } = [];

        public void OnEntityKilled(string entityCode)
        {
            CheckEventTrackers(KillTrackers, entityCode);
        }

        public void OnBlockPlaced(string blockCode)
        {
            CheckEventTrackers(BlockPlaceTrackers, blockCode);
        }

        public void OnBlockBroken(string blockCode)
        {
            CheckEventTrackers(BlockBreakTrackers, blockCode);
        }

        private static void CheckEventTrackers(List<EventTracker> trackers, string code)
        {
            foreach (var tracker in trackers)
            {
                if (TrackerMatches(tracker, code))
                {
                    tracker.Count++;
                }
            }
        }

        private static bool TrackerMatches(EventTracker tracker, string code)
        {
            foreach (var candidate in tracker.RelevantCodes)
            {
                if (candidate == code || candidate.EndsWith('*') && code.StartsWith(candidate[..^1]))
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsCompletable(IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            var quest = questSystem.QuestRegistry[QuestId];
            var activeActionObjectives = quest.ActionObjectives.ConvertAll<IActiveActionObjective>(objective => questSystem.ActionObjectiveRegistry[objective.Id]);
            bool completable = true;
            for (int i = 0; i < quest.BlockPlaceObjectives.Count; i++)
            {
                completable &= quest.BlockPlaceObjectives[i].Demand <= BlockPlaceTrackers[i].Count;
            }
            for (int i = 0; i < quest.BlockBreakObjectives.Count; i++)
            {
                completable &= quest.BlockBreakObjectives[i].Demand <= BlockBreakTrackers[i].Count;
            }
            for (int i = 0; i < quest.KillObjectives.Count; i++)
            {
                completable &= quest.KillObjectives[i].Demand <= KillTrackers[i].Count;
            }
            foreach (var gatherObjective in quest.GatherObjectives)
            {
                int itemsFound = ItemsGathered(byPlayer, gatherObjective);
                completable &= itemsFound >= gatherObjective.Demand;
            }
            for (int i = 0; i < activeActionObjectives.Count; i++)
            {
                //completable &= activeActionObjectives[i].IsCompletable(byPlayer, quest.ActionObjectives[i].Args);
            }
            return completable;
        }

        public void CompleteQuest(IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            var quest = questSystem.QuestRegistry[QuestId];
            foreach (var gatherObjective in quest.GatherObjectives)
            {
                HandOverItems(byPlayer, gatherObjective);
            }
        }

        public List<int> TrackerProgress()
        {
            var result = new List<int>();
            foreach (var trackerList in new List<EventTracker>[] { KillTrackers, BlockPlaceTrackers, BlockBreakTrackers })
            {
                if (trackerList != null)
                {
                    result.AddRange(trackerList.ConvertAll(tracker => tracker.Count));
                }
            }
            return result;
        }

        public List<int> GatherProgress(IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            var quest = questSystem.QuestRegistry[QuestId];
            return quest.GatherObjectives.ConvertAll(gatherObjective => ItemsGathered(byPlayer, gatherObjective));
        }

        public List<int> ActionProgress(IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            var quest = questSystem.QuestRegistry[QuestId];
            var activeActionObjectives = quest.ActionObjectives.ConvertAll<IActiveActionObjective>(objective => questSystem.ActionObjectiveRegistry[objective.Id]);
            
            List<int> result = [];
            for (int i = 0; i < activeActionObjectives.Count; i++)
            {
                //result.AddRange(activeActionObjectives[i].Progress(byPlayer, quest.ActionObjectives[i].Args));
            }
            return result;
        }

        public List<int> Progress(IPlayer byPlayer)
        {
            var progress = GatherProgress(byPlayer);
            progress.AddRange(TrackerProgress());
            progress.AddRange(ActionProgress(byPlayer));
            return progress;
        }

        public static int ItemsGathered(IPlayer byPlayer, Objective gatherObjective)
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
                    if (GatherObjectiveMatches(slot, gatherObjective))
                    {
                        itemsFound += slot.Itemstack.StackSize;
                    }
                }
            };

            return itemsFound;
        }

        private static bool GatherObjectiveMatches(ItemSlot slot, Objective gatherObjective)
        {
            if (slot.Empty) return false;

            var code = slot.Itemstack.Collectible.Code.Path;
            foreach (var candidate in gatherObjective.ValidCodes)
            {
                if (candidate == code || candidate.EndsWith('*') && code.StartsWith(candidate[..^1]))
                {
                    return true;
                }
            }
            return false;
        }

        public static void HandOverItems(IPlayer byPlayer, Objective gatherObjective)
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
                    if (GatherObjectiveMatches(slot, gatherObjective))
                    {
                        var stack = slot.TakeOut(Math.Min(slot.Itemstack.StackSize, gatherObjective.Demand - itemsFound));
                        slot.MarkDirty();
                        itemsFound += stack.StackSize;
                    }
                    if (itemsFound > gatherObjective.Demand) { return; }
                }
            }
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class EventTracker
    {
        public List<string> RelevantCodes { get; set; } = [];
        public int Count { get; set; }
    }
}