using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Vintagestory.API.Common;

namespace VSQuest
{
	public class Quest
	{
		public string Id { get; set; } = string.Empty;
		public int Cooldown { get; set; }
		public bool PerPlayer { get; set; }
		public List<string> Predecessors { get; set; } = [];
		public List<ActionWithArgs> OnAcceptedActions { get; set; } = [];
		public List<Objective> Objectives { get; set; } = [];
		public List<ItemReward> ItemRewards { get; set; } = [];
		public RandomItemReward RandomItemRewards { get; set; } = new RandomItemReward();
		public List<ActionWithArgs> OnCompletedActions { get; set; } = [];
	}

	public class Objective(string id, JObject data)
	{
		public string Id => id;
		public JObject Data => data;
	}

	public class ItemReward
	{
		public string ItemCode { get; set; } = string.Empty;
		public int Amount { get; set; }
	}

	public class ActionWithArgs(string id, JObject data)
	{
		public string Id => id;
		public JObject Data => data;
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