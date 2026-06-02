using Newtonsoft.Json.Linq;
using System;
using VSQuest.Containers;
using VSQuest.Model.Server.Data;
using VSQuest.Server.Model;
using VSQuest.Server.Model.Objective;

namespace VSQuest.Templates
{
	public delegate O ObjectiveFactory<O, C>(string id, IQuest quest, C context)
		where O : IObjective
		where C : IQuestCreationContext;
	public delegate O ObjectiveFactory<O, OD, C>(string id, IQuest quest, OD data, C context)
		where O : IObjective<OD>
		where OD : IObjectiveData
		where C : IQuestCreationContext;
	public interface IObjectiveTemplate : ITemplate
	{
		IObjectiveContainer InflateContainer(string id, InflationContext context, JObject? obj = null);
	}
	public interface IObjectiveCreatorTemplate : IObjectiveTemplate
	{
		IObjective CreateObjective(string id, IQuest quest, IQuestCreationContext context);
	}
	public interface IObjectiveTrackerTemplate<O, T, C> : IObjectiveCreatorTemplate
		where O : IObjective
		where T : IObjectiveTracker<O>
		where C : IQuestCreationContext;
	public abstract class ObjectiveTemplate<O, T, C>(ObjectiveFactory<O, C> factory, T tracker) : IObjectiveTrackerTemplate<O, T, C>
		where O : IObjective
		where T : IObjectiveTracker<O>
		where C : IQuestCreationContext
	{
		protected readonly T _tracker = tracker;

		public IObjective CreateObjective(string id, IQuest quest, IQuestCreationContext context)
		{
			if (context is not C c)
			{
				throw new Exception();
			}

			var objective = factory(id, quest, c);
			_tracker.Register(objective);
			return objective;
		}

		public IObjectiveContainer InflateContainer(string id, InflationContext context, JObject? obj = null)
		{
			return ObjectiveContainer.New(id, this);
		}
	}

	public interface IObjectiveCreatorTemplate<OD> : IObjectiveTemplate, ITemplate<OD>
		where OD : IObjectiveData
	{
		IObjective CreateObjective(string id, IQuest quest, OD data, IQuestCreationContext context);
	}
	public interface IObjectiveTrackerTemplate<O, OD, T, C> : IObjectiveCreatorTemplate<OD>
		where O : IObjective<OD>
		where OD : IObjectiveData
		where T : IObjectiveTracker<O>
		where C : IQuestCreationContext;
	public class ObjectiveTemplate<O, OD, T, C>(ObjectiveFactory<O, OD, C> factory, T tracker) : Template<OD>, IObjectiveTrackerTemplate<O, OD, T, C>
		where O : IObjective<OD>
		where OD : IObjectiveData
		where T : IObjectiveTracker<O>
		where C : IQuestCreationContext
	{
		protected readonly T _tracker = tracker;

		public IObjective CreateObjective(string id, IQuest quest, OD data, IQuestCreationContext context)
		{
			if (context is not C c)
			{
				throw new Exception();
			}

			var objective = factory(id, quest, data, c);
			_tracker.Register(objective);
			return objective;
		}

		public IObjectiveContainer InflateContainer(string id, InflationContext context, JObject? obj = null)
		{
			if (obj is null)
			{
				throw new InflateException($@"""{GetType().Name}"" type requires data provision matching ""{typeof(OD).Name}"" type", context);
			}
			return ObjectiveContainer.New(id, this, InflateData(obj, context));
		}
	}
}