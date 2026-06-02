using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Server;

namespace VSQuest.Server.Model
{
	public class PlayerState
	{
		readonly IServerPlayer _player;
		readonly List<IQuest> _quests;

		public PlayerState(IServerPlayer player)
		{
			_player = player;
			
			try
			{
				_quests = ApiModHelper.LoadData<List<IQuest>>($"quests-{player.PlayerUID}") ?? [];
			}
			catch (ProtoException)
			{
				ApiModHelper.Error($@"Could not load quests for player with id ""{player.PlayerUID}"", corrupted quests will be deleted.");
				_quests = [];
			}
		}

		public void AcceptQuest(IQuest quest)
		{
			_quests.Add(quest);
			quest.Accept();
		}

		public void CompleteQuest(QuestSummary data)
		{
			//TODO : sanitize and check if ok 

			var quest = _quests.FirstOrDefault(q => q.Id == data.Id && q.GiverId == data.GiverId) ??
				throw new Exception($@"Quest ""{data.Id}"" from ""{data.GiverId}"" not found for user ""{_player.PlayerName}"" : cannot complete");

			if (!quest.Fullfilled)
			{
				throw new Exception($@"Quest ""{data.Id}"" from ""{data.GiverId}"" not fullfilled for user ""{_player.PlayerName}"" : cannot complete");
			}
			quest.Complete();
			_quests.Remove(quest);

			var quest = _questTemplates[message.Id];

			var questgiver = ApiModHelper.GetEntity(data.GiverId);
			var key = quest.PerPlayer ? $"lastaccepted-{quest.Id}-{_player.PlayerUID}" : $"lastaccepted-{quest.Id}";
			questgiver.WatchedAttributes.SetDouble(key, ApiModHelper.TotalDays);
			questgiver.WatchedAttributes.MarkPathDirty(key);

				RewardPlayer(player, message, questgiver);
				MarkQuestCompleted(player, message, questgiver);
		}

		public void Save()
		{
			ApiModHelper.SaveData($"quests-{_player.PlayerUID}", _quests);
		}

		public bool QuestExists(QuestSummary data) =>
			_quests.Exists(q => q.Id == data.Id && q.GiverId == data.GiverId);

		public IQuest[] GetQuestsFor(long giverId) =>
			[.. _quests.Where(q => q.GiverId == giverId)];


	}
}