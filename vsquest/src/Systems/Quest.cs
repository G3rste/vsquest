using System.Collections.Generic;

namespace VsQuest
{
    public class Quest
    {
        public string Id { get; set; } = string.Empty;
        public int Cooldown { get; set; }
        public bool PerPlayer { get; set; }
        public string Predecessor { get; set; } = string.Empty;
        public List<ActionWithArgs> OnAcceptedActions { get; set; } = [];
        public List<Objective> GatherObjectives { get; set; } = [];
        public List<Objective> KillObjectives { get; set; } = [];
        public List<Objective> BlockPlaceObjectives { get; set; } = [];
        public List<Objective> BlockBreakObjectives { get; set; } = [];
        public List<ActionWithArgs> ActionObjectives { get; set; } = [];
        public List<ItemReward> ItemRewards { get; set; } = [];
        public RandomItemReward RandomItemRewards { get; set; } = new RandomItemReward();
        public List<ActionWithArgs> ActionRewards { get; set; } = [];
    }

    public class Objective
    {
        public List<string> ValidCodes { get; set; } = [];
        public int Demand { get; set; }
    }

    public class ItemReward
    {
        public string ItemCode { get; set; } = string.Empty;
        public int Amount { get; set; }
    }

    public class ActionWithArgs
    {
        public string Id { get; set; } = string.Empty;
        public string[] Args { get; set; } = [];
    }

    public class RandomItemReward
    {
        public int SelectAmount { get; set; }
        public List<RandomItem> Items { get; set; } = [];
    }

    public class RandomItem
    {
        public string ItemCode { get; set; } = string.Empty;
        public int MinAmount { get; set; }
        public int MaxAmount { get; set; }
    }
}