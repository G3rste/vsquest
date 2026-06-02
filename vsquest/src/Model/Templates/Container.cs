using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using VSQuest.Model.Server.Data;
using VSQuest.Server.Model.Objective;
using VSQuest.Templates;

namespace VSQuest.Containers
{
	public interface IContainer
	{
		string Id { get; }
	}
	public interface IContainer<T> : IContainer
		where T : ITemplate;
	public abstract class Container<T>(string id, T template) : IContainer<T>
		where T : ITemplate
	{
		public string Id => id;
		protected T _template = template;
	}
	public abstract class Container<T, D>(string id, T template, D data) : Container<T>(id, template)
		where T : ITemplate
		where D : IData
	{
		protected D _data = data;
	}

	#region QuestAction

	public static class QuestActionContainer
	{
		public static IQuestActionContainer[] Inflate(JProperty? property, InflationContext context)
		{
			if (property == null)
			{
				return [];
			}

			context.PropertyName = property.Name;
			if (property.Value is not JArray arr)
			{
				throw new InflateException("Action list should be an array", context);
			}

			var actionContainers = new List<IQuestActionContainer>();
			for (var child = arr.First; child != null; child = child.Next)
			{
				context.PropertyArrayIndex.Incr();
				try
				{
					actionContainers.Add(Inflate(child, context));
				}
				catch (Exception ex)
				{
					context.Logger.Error(ex);
				}
			}
			return [.. actionContainers];
		}

		static IQuestActionContainer Inflate(JToken json, InflationContext context)
		{
			var id = json.Value<string>();
			if (id != null)
			{
				return Inflate(id, null, context);
			}

			if (json is not JObject obj)
			{
				throw new InflateException("Action should be an object or a bare string as an action template id", context);
			}
			
			id = obj["id"]?.Value<string>() ?? throw new InflateException($"An id is required as a string", context);
			return Inflate(id, obj, context);
		}

		static IQuestActionContainer Inflate(string id, JObject? obj, InflationContext context)
		{
			if (id.IsWhiteSpace())
			{
				throw new InflateException($"Id should contain visible characters", context);
			}
			return context.Templates.Get<IQuestActionTemplate>(id).InflateContainer(id, context, obj);
		}

		public static QuestActionContainer<T> New<T>(string id, T template)
			where T : IQuestExecActionTemplate =>
			new(id, template);

		public static QuestActionContainer<T, D> New<T, D>(string id, T template, D data)
			where T : IQuestExecActionTemplate<D>
			where D : IData =>
			new(id, template, data);
	}

	public interface IQuestActionContainer : IContainer
	{
		void Exec(IQuest quest);
	}
	public class QuestActionContainer<T>(string id, T template) : Container<T>(id, template), IQuestActionContainer
		where T : IQuestExecActionTemplate
	{
		public void Exec(IQuest quest) =>
			_template.Exec(quest);
	}

	public class QuestActionContainer<T, D>(string id, T template, D data) : Container<T, D>(id, template, data), IQuestActionContainer
		where T : IQuestExecActionTemplate<D>
		where D : IData
	{
		public void Exec(IQuest quest) =>
			_template.Exec(quest, _data);
	}

	#endregion

	#region ObjectiveAction

	public static class ObjectiveActionContainer
	{
		public static IObjectiveActionContainer[] Inflate(JProperty? property, InflationContext context)
		{
			if (property == null)
			{
				return [];
			}

			context.PropertyName = property.Name;
			if (property.Value is not JArray arr)
			{
				throw new InflateException("Action list should be an array", context);
			}

			var actionContainers = new List<IObjectiveActionContainer>();
			for (var child = arr.First; child != null; child = child.Next)
			{
				context.PropertyArrayIndex.Incr();
				try
				{
					actionContainers.Add(Inflate(child, context));
				}
				catch (Exception ex)
				{
					context.Logger.Error(ex);
				}
			}
			return [.. actionContainers];
		}

		static IObjectiveActionContainer Inflate(JToken json, InflationContext context)
		{
			var id = json.Value<string>();
			if (id != null)
			{
				return Inflate(id, null, context);
			}

			if (json is not JObject obj)
			{
				throw new InflateException("Action should be an object or a bare string as an action template id", context);
			}

			id = obj["id"]?.Value<string>() ?? throw new InflateException($"An id is required as a string", context);
			return Inflate(id, obj, context);
		}

		static IObjectiveActionContainer Inflate(string id, JObject? obj, InflationContext context)
		{
			if (id.IsWhiteSpace())
			{
				throw new InflateException($"Id should contain visible characters", context);
			}
			return context.Templates.Get<IObjectiveActionTemplate>(id).InflateContainer(id, context, obj);
		}

		public static ObjectiveActionContainer<T> New<T>(string id, T template)
			where T : IObjectiveExecActionTemplate =>
			new(id, template);

		public static ObjectiveActionContainer<T, D> New<T, D>(string id, T template, D data)
			where T : IObjectiveExecActionTemplate<D>
			where D : IData =>
			new(id, template, data);
	}

	public interface IObjectiveActionContainer : IContainer
	{
		void Exec(IObjective objective);
	}
	public class ObjectiveActionContainer<T>(string id, T template) : Container<T>(id, template), IObjectiveActionContainer
		where T : IObjectiveExecActionTemplate
	{
		public void Exec(IObjective objective) =>
			_template.Exec(objective);
	}

	public class ObjectiveActionContainer<T, D>(string id, T template, D data) : Container<T, D>(id, template, data), IObjectiveActionContainer
		where T : IObjectiveExecActionTemplate<D>
		where D : IData
	{
		public void Exec(IObjective objective) =>
			_template.Exec(objective, _data);
	}

	#endregion

	#region Reward

	public static class RewardContainer
	{
		public static IRewardContainer[] Inflate(JProperty? property, InflationContext context)
		{
			if (property == null)
			{
				return [];
			}

			return [];
		}
	}

	public interface IRewardContainer : IContainer
	{

	}
	public class RewardContainer<T>(string id , T template) : Container<T>(id, template), IRewardContainer
		where T : ITemplate
	{

	}

	public class RewardContainer<T, D>(string id, T template, D data) : Container<T, D>(id, template, data), IRewardContainer
		where T : ITemplate<D>
		where D : IData
	{

	}

	#endregion

	#region Lock

	public static class LockContainer
	{
		public static ILockContainer[] Inflate(JProperty? property, InflationContext context)
		{
			if (property == null)
			{
				return [];
			}

			return [];
		}

		static ILockContainer? Inflate(string id, InflationContext context)
		{
			return null;
		}

		public static LockContainer<T, RD> New<T, RD>(string id, T template)
			where T : ILockTemplate<RD>
			where RD : IRuntimeData =>
			new(id, template);

		public static LockContainer<T, D, RD> New<T, D, RD>(string id, T template, D data)
			where T : ILockTemplate<D, RD>
			where D : IData
			where RD : IRuntimeData =>
			new(id, template, data);
	}

	public interface ILockContainer : IContainer
	{
		bool Check(IRuntimeData runtimeData);
	}
	public class LockContainer<T, RD>(string id, T template) : Container<T>(id, template), ILockContainer
		where T : ILockTemplate<RD>
		where RD : IRuntimeData
	{
		public bool Check(IRuntimeData runtimeData) =>
			runtimeData is RD rd ? _template.Check(rd) : throw new InvalidOperationException($"Lock<{typeof(RD).Name}> can't deal with {runtimeData.GetType().Name}");
	}

	public class LockContainer<T, D, RD>(string id, T template, D data) : Container<T, D>(id, template, data), ILockContainer
		where T : ILockTemplate<D, RD>
		where D : IData
		where RD : IRuntimeData
	{
		public bool Check(IRuntimeData runtimeData) =>
			runtimeData is RD rd ? _template.Check(_data, rd) : throw new InvalidOperationException($"Lock<{typeof(RD).Name}> can't deal with {runtimeData.GetType().Name}");
	}

	#endregion

	#region Objective

	public static class ObjectiveContainer
	{
		public static IObjectiveContainer[] Inflate(JProperty? property, InflationContext context)
		{
			if (property is null)
			{
				throw new InflateException($@"Quests should have an ""objectives"" property referencing an array of objective data blocks", context);
			}
			context.PropertyName = "objectives";

			if (property.Value is not JArray arr)
			{
				throw new InflateException($"Objectives are to be contained in an array", context);
			}

			if (arr.Count == 0)
			{
				throw new InflateException($"Array is null : quests should have at least one objective", context);
			}
			context.PropertyArrayIndex.Reset();

			var containers = new List<IObjectiveContainer>();
			for (var objective = arr.First; objective != null; objective = objective.Next)
			{
				context.PropertyArrayIndex.Incr();
				containers.Add(Inflate(objective, context));
			}
			return [.. containers];
		}

		static IObjectiveContainer Inflate(JToken json, InflationContext context)
		{
			if (json is not JObject obj)
			{
				if (json.Type != JTokenType.String)
				{
					throw new InflateException($"An objective should be an object or a bare string as an action template id", context);
				}
				return Inflate((string)json!, context);
			}

			var id = obj["id"]?.Value<string>() ?? throw new InflateException($"An id is required as a string", context);
			return Inflate(id, obj, context);
		}

		static IObjectiveContainer Inflate(string id, InflationContext context)
		{
			return context.Templates.Get<IObjectiveTemplate>(id).InflateContainer(id, context);
		}

		static IObjectiveContainer Inflate(string id, JObject obj, InflationContext context)
		{
			return context.Templates.Get<IObjectiveTemplate>(id).InflateContainer(id, context, obj);
		}

		public static ObjectiveContainer<T> New<T>(string id, T template)
			where T : IObjectiveCreatorTemplate =>
			new(id, template);

		public static ObjectiveContainer<T, OD> New<T, OD>(string id, T template, OD data)
			where T : IObjectiveCreatorTemplate<OD>
			where OD : IObjectiveData =>
			new(id, template, data);
	}

	public interface IObjectiveContainer : IContainer
	{
		IObjective CreateObjective(IQuest quest, IQuestCreationContext context);
	}
	public class ObjectiveContainer<T>(string id, T template) : Container<T>(id, template), IObjectiveContainer
		where T : IObjectiveCreatorTemplate
	{
		public IObjective CreateObjective(IQuest quest, IQuestCreationContext context) =>
			_template.CreateObjective(Id, quest, context);
	}

	public class ObjectiveContainer<T, OD>(string id, T template, OD data) : Container<T, OD>(id, template, data), IObjectiveContainer
		where T : IObjectiveCreatorTemplate<OD>
		where OD : IObjectiveData
	{
		public IObjective CreateObjective(IQuest quest, IQuestCreationContext context) =>
			_template.CreateObjective(Id, quest, _data, context);
	}

	#endregion
}