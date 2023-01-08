using System.Collections.Generic;

namespace VsQuest
{
    public class Quest
    {
        public string id { get; set; }
        public int cooldown { get; set; }
        public bool perPlayer { get; set; }
        public string predecessor { get; set; }
        public List<ActionWithArgs> onAcceptedActions { get; set; } = new List<ActionWithArgs>();
        public List<Objective> gatherObjectives { get; set; } = new List<Objective>();
        public List<Objective> killObjectives { get; set; } = new List<Objective>();
        public List<Objective> blockPlaceObjectives { get; set; } = new List<Objective>();
        public List<Objective> blockBreakObjectives { get; set; } = new List<Objective>();
        public List<ActionWithArgs> actionObjectives { get; set; } = new List<ActionWithArgs>();
        public List<ItemReward> itemRewards { get; set; } = new List<ItemReward>();
        public RandomItemReward randomItemRewards { get; set; } = new RandomItemReward();
        public List<ActionWithArgs> actionRewards { get; set; } = new List<ActionWithArgs>();
    }

    public class Objective
    {
        public List<string> validCodes { get; set; }
        public int demand { get; set; }
    }

    public class ItemReward
    {
        public string itemCode { get; set; }
        public int amount { get; set; }
    }

    public class ActionWithArgs
    {
        public string id { get; set; }
        public string[] args { get; set; } = new string[0];
    }

    public class RandomItemReward
    {
        public int selectAmount { get; set; }
        public List<RandomItem> items { get; set; } = new List<RandomItem>();
    }

    public class RandomItem
    {
        public string itemCode { get; set; }
        public int minAmount { get; set; }
        public int maxAmount { get; set; }
    }
}