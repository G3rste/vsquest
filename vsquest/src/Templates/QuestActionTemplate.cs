using Newtonsoft.Json.Linq;
using VSQuest.Containers;
using VSQuest.Model.Server.Data;
using VSQuest.Server.Model;

namespace VSQuest.Templates
{
	public delegate void QuestAction(IQuest quest);
	public delegate void QuestActionWithData<D>(IQuest quest, D data)
		where D : IData;

	public interface IQuestActionTemplate : ITemplate
	{
		IQuestActionContainer InflateContainer(string id, InflationContext context, JObject? obj = null);
	}

	public interface IQuestExecActionTemplate : IQuestActionTemplate
	{
		QuestAction Exec { get; }
	}
	public class QuestActionTemplate(QuestAction action) : IQuestExecActionTemplate
	{
		public QuestAction Exec => action;

		public IQuestActionContainer InflateContainer(string id, InflationContext _, JObject? __ = null)
		{
			return QuestActionContainer.New(id, this);
		}
	}

	public interface IQuestExecActionTemplate<D> : ITemplate<D>, IQuestActionTemplate
		where D : IData
	{
		QuestActionWithData<D> Exec { get; }
	}
	public class QuestActionTemplate<D>(QuestActionWithData<D> action) : Template<D>, IQuestExecActionTemplate<D>
		where D : IData
	{
		public QuestActionWithData<D> Exec => action;

		public IQuestActionContainer InflateContainer(string id, InflationContext context, JObject? obj = null)
		{
			if (obj is null)
			{
				throw new InflateException($@"""{GetType().Name}"" type requires data provision matching ""{typeof(D).Name}"" type", context);
			}
			return QuestActionContainer.New(id, this, InflateData(obj, context));
		}
	}
}