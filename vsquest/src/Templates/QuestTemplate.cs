using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using VSQuest.Containers;

namespace VSQuest.Templates
{
	public interface IQuestTemplate : ITemplate
	{
		List<IObjectiveContainer> Objectives { get; }
		List<IRewardContainer> Rewards { get; }
		List<ILockContainer> Locks { get; }
		List<IQuestActionContainer> OnAcceptActions { get; }
		List<IQuestActionContainer> OnCompleteActions { get; }
		List<IObjectiveActionContainer> OnProgressActions { get; }
		List<IObjectiveActionContainer> OnFailActions { get; }
	}
	public record QuestTemplate(
		List<IObjectiveContainer> Objectives,
		List<IRewardContainer> Rewards,
		List<ILockContainer> Locks,
		List<IQuestActionContainer> OnAcceptActions,
		List<IQuestActionContainer> OnCompleteActions,
		List<IObjectiveActionContainer> OnProgressActions,
		List<IObjectiveActionContainer> OnFailActions) : IQuestTemplate
	{

		//public bool CompletedBy(EntityPlayer player)
		//{

		//}

		//public bool LockedFor(EntityPlayer player)
		//{

		//}

		//public bool AvailableFrom(Entity giver)
		//{

		//}

		//public bool PredecessorsCompletedBy(EntityPlayer player)
		//{
		//	//TODO : check
		//	var completedQuests = player.WatchedAttributes.GetStringArray($"playercompleted-{player.PlayerUID}", []);
		//	return Predecessors.Length == 0 || Predecessors.All(questId => completedQuests.Contains(questId));
		//}

		public static IQuestTemplate[] Inflate(JToken token, InflationContext context)
		{
			return token.Type switch
			{
				JTokenType.Array => [.. Inflate((JArray)token, context)],
				JTokenType.Object => [.. Inflate((JObject)token, context)],
				_ => throw new InflateException($"Quest template files should contain else :\na single quest template object\nan array of template objects\nan object whose properties are template objects with their ids as property names\nan array of these", context),
			};
		}

		static List<IQuestTemplate> Inflate(JArray arr, InflationContext context)
		{
			if (!arr.HasValues)
			{
				throw new InflateException("Root array is empty", context);
			}

			var questTemplates = new List<IQuestTemplate>();
			for (var child = arr.First;  child != null; child = child.Next)
			{
				context.QuestTemplateArrayIndex.Incr();
				try
				{
					if (child is JObject obj)
					{
						questTemplates.AddRange(Inflate(obj, context));
						continue;
					}
					throw new InflateException($"Quest templates should be objects", context);
				}
				catch (InflateException ex)
				{
					context.Logger.Error(ex);
				}
				catch (Exception ex)
				{
					context.Logger.Error("Implementation mistake : report to dev!");
					context.Logger.Error(ex);
				}
			}
			context.QuestTemplateArrayIndex.Reset();
			return questTemplates;
		}

		static List<IQuestTemplate> Inflate(JObject obj, InflationContext context)
		{
			var id = obj.Value<string>("id");
			if (id != null)
			{
				return [Inflate(id, obj, context)];
			}

			var questTemplates = new List<IQuestTemplate>();
			foreach (var property in obj.Properties())
			{
				context.QuestTemplateRegistryIndex.Incr();
				try
				{
					if (property.Value is JObject val)
					{
						questTemplates.Add(Inflate(property.Name, val, context));
						continue;
					}
					throw new InflateException($"Quest templates should be objects", context);
					
				}
				catch (Exception ex)
				{
					context.Logger.Error(ex);
				}
			}
			context.QuestTemplateRegistryIndex.Reset();
			return questTemplates;
		}

		static QuestTemplate Inflate(string id, JObject obj, InflationContext context)
		{
			if (id.IsWhiteSpace())
			{
				context.Throw("Each quest template requires a valid id");
			}

			return new QuestTemplate(
				ObjectiveContainer.Inflate(obj.Property("objectives"), context),
				RewardContainer.Inflate(obj.Property("rewards"), context),
				LockContainer.Inflate(obj.Property("locks"), context),
				QuestActionContainer.Inflate(obj.Property("onAcceptActions"), context),
				QuestActionContainer.Inflate(obj.Property("onCompleteActions"), context),
				ObjectiveActionContainer.Inflate(obj.Property("onProgressActions"), context),
				ObjectiveActionContainer.Inflate(obj.Property("onFailActions"), context)
			);
		}
	}

	public class Counter(int initial)
	{
		readonly int _initial = initial;
		public int Value { get; private set; } = initial;

		public Counter Reset()
		{
			Value = _initial;
			return this;
		}

		public Counter Incr()
		{
			Value++;
			return this;
		}
	}

	public record InflationContext(ILogger Logger, TemplateRepositories Templates)
	{
		public AssetLocation? Location { get; set; }
		public Counter QuestTemplateArrayIndex { get; } = new(-1);
		public Counter QuestTemplateRegistryIndex { get; } = new(-1);
		public string? PropertyName { get; set; }
		public Counter PropertyArrayIndex { get; } = new(-1);

		[DoesNotReturn]
		public void Throw(string message) =>
			throw new InflateException(message, this);
	}

	public class InflateException : Exception
	{
		readonly string[] _parts = new string[6];

		public override string Message => string.Join(", ", _parts);

		public InflateException(string message, InflationContext context)
		{
			_parts[0] = message;
			if (context.Location != null)
			{
				_parts[1] = $"in file {context.Location.Path}";
			}
			if (context.QuestTemplateArrayIndex.Value > -1)
			{
				_parts[2] = $"root array element no.{context.QuestTemplateArrayIndex.Value}";
			}
			if (context.QuestTemplateRegistryIndex.Value > -1)
			{
				_parts[3] = $"registry element no.{context.QuestTemplateArrayIndex.Value}";
			}
			if (context.PropertyName != null)
			{
				_parts[4] = @$"property ""{context.PropertyName}""";

				if (context.PropertyArrayIndex.Value > -1)
				{
					_parts[5] = $"element no.{context.PropertyArrayIndex.Value}";
				}
			}
		}
	}

	public interface IQuestCreationContext
	{
		ICoreServerAPI Api { get; }
		IQuestTemplate Template { get; }
		long? GiverId { get; }
		IServerPlayer Player { get; }
	}
	public record QuestCreationContext(ICoreServerAPI Api, IQuestTemplate Template, IServerPlayer Player, long? GiverId) : IQuestCreationContext;
}