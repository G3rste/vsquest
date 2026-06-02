using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VSQuest.Templates
{
	public class QuestTemplateSystem : ModSystem
	{
		readonly Dictionary<Type, Dictionary<string, ITemplate>> _templates = new(6)
		{
			{ typeof(IQuestTemplate), [] },
			{ typeof(IObjectiveTemplate), [] },
			{ typeof(IRewardTemplate), [] },
			{ typeof(ILockTemplate), [] },
			{ typeof(IQuestActionTemplate), [] },
			{ typeof(IObjectiveActionTemplate), [] }
		};

		public void RegisterTemplate(IQuestTemplate template)
		{
			RegisterTemplate(template.Id, template);
		}

		public void RegisterTemplate<T>(string id, T template)
			where T : ITemplate
		{
			_templates[typeof(T)][id] = template;
		}

		public T GetTemplate<T>(string id)
			where T : ITemplate
		{
			return (T)_templates[typeof(T)][id];
		}
	}
}