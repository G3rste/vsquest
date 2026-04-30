using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using VSQuest.Model.Action;

namespace VSQuest.Model
{
	public class QuestTemplate
	{
		public required string Id { get; init; }
		public int Cooldown { get; init; } = -1;
		public bool Shared { get; init; } = false;
		
		public bool Timed { get; init; } = false;
		public List<string> Predecessors { get; init; } = [];
		public required List<ObjectiveContainer> Objectives { get; init; }
		public List<ItemReward> ItemRewards { get; init; } = [];
		public List<QuestActionContainer> OnAcceptActions { get; init; } = [];
		public List<QuestActionContainer> OnCompleteActions { get; init; } = [];
		public List<QuestActionContainer> OnProgressActions { get; init; } = [];
		public List<QuestActionContainer> OnFailActions { get; init; } = [];
	}

	public interface IObjectiveData { }
	public class RawObjectiveData(JObject json) : IObjectiveData
	{
		public JObject Json { get; } = json;
	}

	public class ObjectiveContainer
	{
		readonly string _id;
		public string Id => _id;

		IObjectiveData? _data;
		IObjectiveTemplate? _template;

		[JsonConstructor]
		public ObjectiveContainer(JObject json)
		{
			if (json.Count != 1)
			{
				throw new ArgumentException("Objective blocks should be objects with only 1 property whose name is the objective id and its value is the objective data!");
			}

			var property = json.Properties().First();
			_id = property.Name;
			_data = new RawObjectiveData((JObject)property.Value);
		}

		public void SetTemplate(IObjectiveTemplate template)
		{
			if (_template != null)
			{
				return;
			}
			_template = template;

			if (_data is RawObjectiveData data)
			{
				_data = _template.CreateData(data.Json);
			}
		}
	}

	public interface IObjectiveTemplate
	{
		IObjectiveData? CreateData(JObject json);
	}

	public class ObjectiveTemplate() : IObjectiveTemplate
	{
		public IObjectiveData? CreateData(JObject json) => null;
	}

	public class ObjectiveTemplate<T>() : IObjectiveTemplate
		where T : IObjectiveData
	{

		public IObjectiveData? CreateData(JObject json) =>
			json.ToObject<T>() ?? throw new Exception("Deserialization problem");
	}

	public class ItemReward
	{
		public string ItemCode { get; set; } = string.Empty;
		public int Amount { get; set; }
	}
}