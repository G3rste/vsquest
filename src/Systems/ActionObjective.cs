using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    public interface ActiveActionObjective
    {
        bool isCompletable(IPlayer byPlayer, params string[] args);
        List<int> progress(IPlayer byPlayer, params string[] args);
    }
}