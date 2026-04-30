using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using Vintagestory.API.Server;
using VSQuest.Server;

namespace VSQuest.Model.Action
{
	//ModSys.Start populates action and objective template lists
	//ModSys.AssetsLoaded instanciates quest templates with raw parts (actions and objectives) then adds those quest templates through
	//ServerSide.AddQuestTemplates in which raw parts get inflated to proper data types along with their relevant templates

	//Raw version containing json object strings
	public interface IActionData { }
	public class RawActionData(JObject json) : IActionData
	{
		public JObject Json => json;
	}

	//Containers responsible for basic json format checks and data structure swap from raw to inflated
	public class QuestActionContainer
	{
		public string Id { get; init; }

		//Data might be null in case of a data-less action
		IActionData? _data;
		IQuestActionTemplate? _template;

		[JsonConstructor]
		public QuestActionContainer(JObject json)
		{
			if (json.Count != 1)
			{
				throw new ArgumentException("Action blocks should be objects with only 1 property whose name is the action id and its value is the action data!");
			}

			var property = json.Properties().First();
			Id = property.Name;
			//At first, data is raw
			_data = new RawActionData((JObject)property.Value);
		}

		public void SetTemplate(IQuestActionTemplate template)
		{
			//We set template only once
			if (_template != null)
			{
				return;
			}
			_template = template;

			if (_data is RawActionData data)
			{
				//When we set template we use it to properly inflate data to its relevant type
				_data = _template.CreateData(data.Json);
			}
		}

		public void Exec(QuestInfo info)
		{
			_template?.Exec(info, _data);
		}
	}

	public interface IQuestActionTemplate
	{
		void Exec(QuestInfo info, IActionData? data);
		IActionData? CreateData(JObject json);
	}

	//Data-less action template
	public class QuestActionTemplate(QuestAction action) : IQuestActionTemplate
	{
		readonly QuestAction _action = action;

		public void Exec(QuestInfo info, IActionData? data) => _action(ApiModHelper.Api, info);

		public IActionData? CreateData(JObject json) => null;
	}

	public class QuestActionTemplate<T>(QuestAction<T> action) : IQuestActionTemplate
		where T : IActionData
	{
		readonly QuestAction<T> _action = action;

		public void Exec(QuestInfo info, IActionData? data)
		{
			//data can't be null in such a template
			ArgumentNullException.ThrowIfNull(data);
			_action(ApiModHelper.Api, info, (T)data);
		}

		//TODO : care to add better exception message...
		public IActionData? CreateData(JObject json) =>
			json.ToObject<T>() ?? throw new Exception("Deserialization problem");
	}

	public class QuestInfo(string id, long giverId, IServerPlayer player)
	{
		public string Id => id;
		public long GiverId => giverId;
		public IServerPlayer Player => player;
	}

	//Data-less action
	public delegate void QuestAction(ICoreServerAPI api, QuestInfo info);
	public delegate void QuestAction<T>(ICoreServerAPI api, QuestInfo info, T data)
		where T : IActionData;
}