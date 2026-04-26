using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VSQuest
{
	public interface IActiveActionObjective
	{
		bool IsCompletable(IPlayer byPlayer, params string[] args);
		List<int> Progress(IPlayer byPlayer, params string[] args);
	}
}