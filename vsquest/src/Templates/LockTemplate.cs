using System;
using VSQuest.Model.Server.Data;

namespace VSQuest.Templates
{
	public delegate bool Lock<RD>(RD runtimeData)
		where RD : IRuntimeData;
	public delegate bool Lock<D, RD>(D data, RD runtimeData)
		where D : IData
		where RD : IRuntimeData;

	public interface ILockTemplate : ITemplate
	{

	}
	public interface ILockTemplate<RD> : ITemplate
		where RD : IRuntimeData
	{
		public bool Check(IRuntimeData data);
	}
	public class LockTemplate<RD>(Lock<RD> function) : ILockTemplate<RD>
		where RD : IRuntimeData
	{
		public bool Check(IRuntimeData runtimeData)
		{
			if (runtimeData is not RD rd)
			{
				throw new InvalidOperationException($"Lock<{typeof(RD).Name}> can't deal with {runtimeData.GetType().Name}");
			}
			return function(rd);
		}
	}

	public interface ILockTemplate<D, RD> : ITemplate
		where D : IData
		where RD : IRuntimeData
	{
		public bool Check(D data, IRuntimeData runtimeData);
	}
	public class LockTemplate<D, RD>(Lock<D, RD> function) : ILockTemplate<D, RD>
		where D : IData
		where RD : IRuntimeData
	{
		public bool Check(D data, IRuntimeData runtimeData)
		{
			if (runtimeData is not RD rd)
			{
				throw new InvalidOperationException($"Lock<{typeof(RD).Name}> can't deal with {data.GetType().Name}");
			}
			return function(data, rd);
		}
	}
}