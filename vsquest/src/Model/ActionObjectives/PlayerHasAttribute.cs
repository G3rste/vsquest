using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VSQuest
{
	public class PlayerHasAttributeActionObjective : IActiveActionObjective
	{
		public bool IsCompletable(IPlayer byPlayer, params string[] args)
		{
			return byPlayer.Entity.WatchedAttributes.GetString(args[0]) == args[1];
		}

		public List<int> Progress(IPlayer byPlayer, params string[] args)
		{
			return IsCompletable(byPlayer, args) ? new List<int>([1]) : new List<int>([0]);
		}
	}
}