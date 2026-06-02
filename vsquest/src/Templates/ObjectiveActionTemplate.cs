using Newtonsoft.Json.Linq;
using System;
using VSQuest.Containers;
using VSQuest.Model.Server.Data;
using VSQuest.Server.Model.Objective;

namespace VSQuest.Templates
{
	public delegate void ObjectiveAction<O>(O objective)
		where O : IObjective;
	public delegate void ObjectiveAction<O, D>(O objective, D data)
		where O : IObjective
		where D : IData;

	public interface IObjectiveActionTemplate : ITemplate
	{
		IObjectiveActionContainer InflateContainer(string id, InflationContext context, JObject? obj);
	}
	public interface IObjectiveExecActionTemplate : IObjectiveActionTemplate
	{
		void Exec(IObjective objective);
	}
	public class ObjectiveActionTemplate<O>(ObjectiveAction<O> action) : IObjectiveExecActionTemplate
		where O : IObjective
	{
		public IObjectiveActionContainer InflateContainer(string id, InflationContext _, JObject? __ = null) =>
			ObjectiveActionContainer.New(id, this);

		public void Exec(IObjective objective)
		{
			if (objective is not O obj)
			{
				throw new InvalidOperationException($@"Template can only deal with ""{typeof(O).Name}"" data type : ""{objective.GetType().Name}"" provided");
			}
			action(obj);
		}
	}

	public interface IObjectiveExecActionTemplate<D> : IObjectiveActionTemplate, ITemplate<D>
		where D : IData
	{
		void Exec(IObjective objective, D data);
	}

	public class ObjectiveActionTemplate<O, D>(ObjectiveAction<O, D> action) : Template<D>, IObjectiveExecActionTemplate<D>
		where O : IObjective
		where D : IData
	{
		public IObjectiveActionContainer InflateContainer(string id, InflationContext context, JObject? obj)
		{
			if (obj is null)
			{
				throw new InflateException($@"""{GetType().Name}"" type requires data provision matching ""{typeof(D).Name}"" type", context);
			}
			return ObjectiveActionContainer.New(id, this, InflateData(obj, context));
		}

		public void Exec(IObjective objective, D data)
		{
			if (objective is not O obj)
			{
				throw new InvalidOperationException($@"Template can only deal with ""{typeof(O).Name}"" data type : ""{objective.GetType().Name}"" provided");
			}
			action(obj, data);
		}
	}
}