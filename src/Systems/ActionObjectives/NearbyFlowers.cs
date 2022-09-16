using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    public class NearbyFlowersActionObjective : ActiveActionObjective
    {
        public bool isCompletable(IPlayer byPlayer, params string[] args)
        {
            return progress(byPlayer)[0] >= int.Parse(args[0]);
        }

        public List<int> progress(IPlayer byPlayer, params string[] args)
        {
            var entity = byPlayer.Entity;
            int flowersNearby = 0;
            entity.World.BlockAccessor.WalkBlocks(entity.Pos.AsBlockPos.AddCopy(-15, -5, -15), entity.Pos.AsBlockPos.AddCopy(15, 5, 15), (block, x, y, z) =>
            {
                if (block.Code.Path.StartsWith("flower-"))
                {
                    flowersNearby++;
                }
            });
            return new List<int>(new int[] { flowersNearby });
        }
    }
}