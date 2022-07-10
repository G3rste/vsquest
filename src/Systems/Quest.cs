using System.Collections.Generic;

namespace VsQuest
{
    public class Quest
    {
        public string id { get; set; }
        public int cooldown { get; set; }
        public bool perPlayer { get; set; }
        public string predecessor { get; set; }
        public List<Objective> gatherObjectives { get; set; } = new List<Objective>();
        public List<Objective> killObjectives { get; set; } = new List<Objective>();
        public List<string> actionObjectives { get; set; } = new List<string>();
        public List<ItemReward> itemRewards { get; set; } = new List<ItemReward>();
        public List<string> actionRewards { get; set; } = new List<string>();
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
}