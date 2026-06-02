using VSQuest.Model.Server.Data;

namespace VSQuest.Server.Model.Objective
{
	public interface IObjective
	{
		string Id { get; }
		IQuest Quest { get; }
		bool Failed { get; }
		bool Fullfilled { get; }

		public bool Progress();
	}
	public abstract record Objective(string Id, IQuest Quest) : IObjective
	{
		public bool Failed { get; protected set; } = false;
		public bool Fullfilled { get; protected set; } = false;

		public virtual bool Progress()
		{
			Quest.Progress(this);

			return Fullfilled || Failed;
		}
	}

	public interface IObjective<OD> : IObjective
		where OD : IObjectiveData
	{
		OD Data { get; }
	}

	public record ObjectiveInfo(string Id, IObjectiveData Data, bool Failed, bool Fullfiled);

	public abstract record Objective<OD>(string Id, IQuest Quest, OD Data) : Objective(Id, Quest), IObjective<OD>
		where OD : IObjectiveData;

	public interface IObjective<OD, V> : IObjective<OD>
		where OD : IObjectiveData<V>
	{
		V Current { get; }
	}
	public abstract record Objective<OD, V>(string Id, IQuest Quest, OD Data, V Current)
		: Objective<OD>(Id, Quest, Data), IObjective<OD, V>
		where OD : IObjectiveData<V>
	{
		
		public V Current { get; protected set; } = Current;
	}

	public record KillObjective(string Id, IQuest Quest, KillData Data) : Objective<KillData, int>(Id, Quest, Data, 0)
	{
		public override bool Progress()
		{
			Fullfilled = ++Current == Data.Goal;

			return base.Progress();
		}

		public static KillObjective Create(string id, IQuest quest, KillData data, IQuestCreationContext _) => new(id, quest, data);
	}

	//public class GatherObjective : Objective<GatherData, int>
	//{
	//	public GatherObjective(string id, Quest quest, GatherData data) : base(id, quest, data, 0)
	//	{

	//	}

	//	public override void Progress()
	//	{
	//		Fullfilled = ++Current == Data.Goal;


	//	}
	//}

	
}