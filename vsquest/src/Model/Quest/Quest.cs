using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Server;
using VSQuest.Server.Model.Objective;
using VSQuest.Templates;

namespace VSQuest.Server.Model
{
	public interface IQuest
	{
		ICoreServerAPI Api { get; }

		string Id { get; }
		long? GiverId { get; }
		IServerPlayer Player { get; }

		bool Failed { get; }
		bool Fullfilled { get; }

		void Accept();
		void Progress(IObjective objective);
		void Fail(IObjective objective);
		void Fullfill();
	}

	public record QuestInfo(string Id, long? GiverId, bool Failed, bool Fullfilled, IObjective[] Objectives);
	public class Quest : IQuest
	{
		public ICoreServerAPI Api { get; }

		public string Id => _template.Id;
		public long? GiverId { get; }
		public IServerPlayer Player { get; }

		public bool Failed => _objectives.Any(o => o.Failed);
		public bool Fullfilled => _objectives.All(o => o.Fullfilled);

		readonly IQuestTemplate _template;
		readonly IObjective[] _objectives;

		public Quest(IQuestCreationContext context)
		{
			Api = context.Api;

			GiverId = context.GiverId;
			Player = context.Player;

			_template = context.Template;

			var objectives = new List<IObjective>();
			foreach (var container in context.Template.Objectives)
			{
				objectives.Add(container.CreateObjective(this, context));

			}
			_objectives = [.. objectives];
		}

		public void Accept()
		{
			foreach (var actionContainer in _template.OnAcceptActions)
			{
				try
				{
					actionContainer.Exec(this);
				}
				catch (Exception ex)
				{
					ApiModHelper.Error($"Action {actionContainer.Id} caused an error in quest {Id}.");
					ApiModHelper.Error(ex);
					ApiModHelper.SendLogMessage(Player, $"An error occurred during quest {Id}, please check the server logs for more details.");
				}
			}
		}

		public void Progress(IObjective objective)
		{
			foreach (var actionContainer in _template.OnProgressActions)
			{
				try
				{
					actionContainer.Exec(objective);
				}
				catch (Exception ex)
				{
					ApiModHelper.Error($"Action {actionContainer.Id} caused an error in quest {Id}.");
					ApiModHelper.Error(ex);
					ApiModHelper.SendLogMessage(Player, $"An error occurred during quest {Id}, please check the server logs for more details.");
				}
			}
		}

		public void Fail(IObjective objective)
		{
			foreach (var actionContainer in _template.OnFailActions)
			{
				try
				{
					actionContainer.Exec(objective);
				}
				catch (Exception ex)
				{
					ApiModHelper.Error($"Action {actionContainer.Id} caused an error in quest {Id}.");
					ApiModHelper.Error(ex);
					ApiModHelper.SendLogMessage(Player, $"An error occurred during quest {Id}, please check the server logs for more details.");
				}
			}
		}

		public void Fullfill()
		{
			foreach (var actionContainer in _template.OnCompleteActions)
			{
				try
				{
					actionContainer.Exec(this);
				}
				catch (Exception ex)
				{
					ApiModHelper.Error($"Action {actionContainer.Id} caused an error in quest {Id}.");
					ApiModHelper.Error(ex);
					ApiModHelper.SendLogMessage(Player, $"An error occurred during quest {Id}, please check the server logs for more details.");
				}
			}
		}

		//public void CompleteQuest(IPlayer byPlayer)
		//{
		//	foreach (var gatherObjective in _template.GatherObjectives)
		//	{
		//		HandOverItems(byPlayer, gatherObjective);
		//	}
		//}

		//public List<int> ActionProgress(IPlayer byPlayer)
		//{
		//	var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
		//	var activeActionObjectives = _template.ActionObjectives.ConvertAll<IActiveActionObjective>(objective => questSystem.ActionObjectiveRegistry[objective.Id]);

		//	List<int> result = [];
		//	for (int i = 0; i < activeActionObjectives.Count; i++)
		//	{
		//		result.AddRange(activeActionObjectives[i].Progress(byPlayer, quest.ActionObjectives[i].Args));
		//	}
		//	return result;
		//}

		//public List<int> Progress(IPlayer byPlayer)
		//{
		//	var progress = GatherProgress(byPlayer);
		//	progress.AddRange(TrackerProgress());
		//	progress.AddRange(ActionProgress(byPlayer));
		//	return progress;
		//}

		//public static int ItemsGathered(IPlayer byPlayer, IObjective gatherObjective)
		//{
		//	int itemsFound = 0;
		//	foreach (var inventory in byPlayer.InventoryManager.Inventories.Values)
		//	{
		//		if (inventory.ClassName == GlobalConstants.creativeInvClassName)
		//		{
		//			continue;
		//		}
		//		foreach (var slot in inventory)
		//		{
		//			if (GatherObjectiveMatches(slot, gatherObjective))
		//			{
		//				itemsFound += slot.Itemstack.StackSize;
		//			}
		//		}
		//	}
		//	;

		//	return itemsFound;
		//}

		//private static bool GatherObjectiveMatches(ItemSlot slot, IObjective gatherObjective)
		//{
		//	if (slot.Empty) return false;

		//	var code = slot.Itemstack.Collectible.Code.Path;
		//	foreach (var candidate in gatherObjective.ValidCodes)
		//	{
		//		if (candidate == code || candidate.EndsWith('*') && code.StartsWith(candidate[..^1]))
		//		{
		//			return true;
		//		}
		//	}
		//	return false;
		//}

		//public static void HandOverItems(IPlayer byPlayer, IObjective gatherObjective)
		//{
		//	int itemsFound = 0;
		//	foreach (var inventory in byPlayer.InventoryManager.Inventories.Values)
		//	{
		//		if (inventory.ClassName == GlobalConstants.creativeInvClassName)
		//		{
		//			continue;
		//		}
		//		foreach (var slot in inventory)
		//		{
		//			if (GatherObjectiveMatches(slot, gatherObjective))
		//			{
		//				var stack = slot.TakeOut(Math.Min(slot.Itemstack.StackSize, gatherObjective.Demand - itemsFound));
		//				slot.MarkDirty();
		//				itemsFound += stack.StackSize;
		//			}
		//			if (itemsFound > gatherObjective.Demand) { return; }
		//		}
		//	}
		//}
	}
}