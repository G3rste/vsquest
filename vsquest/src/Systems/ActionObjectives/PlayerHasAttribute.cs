using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    public class PlayerHasAttributeActionObjective : ActiveActionObjective
    {
        public bool isCompletable(IPlayer byPlayer, params string[] args)
        {
            return byPlayer.Entity.WatchedAttributes.GetString(args[0]) == args[1];
        }

        public List<int> progress(IPlayer byPlayer, params string[] args)
        {
            return isCompletable(byPlayer, args) ? new List<int>(new int[] { 1 }) : new List<int>(new int[] { 0 });
        }
    }
}