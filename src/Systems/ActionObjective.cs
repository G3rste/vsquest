using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    public interface ActionObjective
    {
        bool isCompletable(IPlayer byPlayer);
        List<int> progress(IPlayer byPlayer);
    }

    public class ActionObjectiveNearbyFlowers : ActionObjective
    {
        public bool isCompletable(IPlayer byPlayer)
        {
            return progress(byPlayer)[0] >= 8;
        }

        public List<int> progress(IPlayer byPlayer)
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