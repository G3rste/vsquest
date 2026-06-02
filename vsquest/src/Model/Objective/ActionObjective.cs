using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace VSQuest.Server.Model.Objective
{
	public interface IActiveActionObjective
	{
		bool IsCompletable(IPlayer byPlayer, params string[] args);
		List<int> Progress(IPlayer byPlayer, params string[] args);
	}

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

	public class NearbyFlowersActionObjective : IActiveActionObjective
	{
		public bool IsCompletable(IPlayer byPlayer, params string[] args)
		{
			return Progress(byPlayer)[0] >= int.Parse(args[0]);
		}

		public List<int> Progress(IPlayer byPlayer, params string[] args)
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
			return new List<int>([flowersNearby]);
		}
	}

	public class ObjectiveUtil
	{
		public static int CountBlockEntities(Vec3i pos, IBlockAccessor blockAccessor, Func<BlockEntity, bool> matcher)
		{
			int blockCount = 0;
			int chunksize = GlobalConstants.ChunkSize;
			for (int x = pos.X - 100; x <= pos.X + 100; x += chunksize)
			{
				for (int y = pos.Y - 15; y <= pos.Y + 15; y += chunksize)
				{
					for (int z = pos.Z - 100; z <= pos.Z + 100; z += chunksize)
					{
						var chunk = blockAccessor.GetChunkAtBlockPos(new BlockPos(x, y, z, 0));
						if (chunk == null) { continue; }
						foreach (var blockEntity in chunk.BlockEntities.Values)
						{
							if (matcher.Invoke(blockEntity))
							{
								blockCount++;
							}
						}
					}
				}
			}
			return blockCount;
		}
	}
}