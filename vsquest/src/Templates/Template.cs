using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using VSQuest.Containers;
using VSQuest.Model.Server.Data;
using VSQuest.Server.Model;
using VSQuest.Server.Model.Objective;

namespace VSQuest.Templates
{
	public interface ITemplate;
	public interface ITemplate<D> : ITemplate
	{
		D InflateData(JObject obj, InflationContext context);
	}
	public abstract class Template<D>() : ITemplate<D>
		where D : IData
	{
		public D InflateData (JObject obj, InflationContext context) =>
			obj.ToObject<D>() ?? throw new InflateException($@"Data object should match the type ""{typeof(D).Name}""", context);
	}

	public interface IObjectiveTracker<O>
	{
		void Register(O objective);
		void Remove(O objective);

	}
	public abstract class ObjectiveTracker<O> : IObjectiveTracker<O>
		where O : IObjective
	{
		protected readonly List<O> _objectives = [];

		public void Register(O objective)
		{
			if (_objectives.Count == 0)
			{
				Attach();
			}
			_objectives.Add(objective);
		}

		public void Remove(O objective)
		{
			if (_objectives.Remove(objective) && _objectives.Count == 0)
			{
				Detach();
			}
		}

		protected abstract void Attach();
		protected abstract void Detach();
	}

	#region Objective

	








	public abstract class OnEntityDeathTracker<O> : ObjectiveTracker<O>
		where O : IObjective
	{
		void OnEntityDeath(Entity entity, DamageSource? source)
		{
			if (ShouldStop(entity, source))
			{
				return;
			}

			foreach (var objective in _objectives)
			{
				if (ShouldProgress(objective, entity, source))
				{
					objective.Progress();

					if (objective.Fullfilled || objective.Failed)
					{
						Remove(objective);
					}
				}
			}
		}

		protected override void Attach() =>
			ApiModHelper.Api.Event.OnEntityDeath += OnEntityDeath;

		protected override void Detach() =>
			ApiModHelper.Api.Event.OnEntityDeath -= OnEntityDeath;

		protected abstract bool ShouldStop(Entity entity, DamageSource? source);
		protected abstract bool ShouldProgress(O objective, Entity entity, DamageSource? source);
	}

	public class KillTracker : OnEntityDeathTracker<KillObjective>
	{
		protected override bool ShouldStop(Entity entity, DamageSource? source) =>
			source is null || source.GetCauseEntity() is not EntityPlayer;

		protected override bool ShouldProgress(KillObjective objective, Entity entity, DamageSource? source)
		{
			var player = (EntityPlayer?)source?.GetCauseEntity();
			if (player?.PlayerUID == objective.Quest.Player.PlayerUID)
			{
				return objective.Data.Codes.Any(code => AssetLocation.Create(code).CompareTo(entity.Code) == 0);
			}
			return false;
		}
	}

	public class KillObjectiveTemplate() : ObjectiveTemplate<KillObjective, KillData, KillTracker, QuestCreationContext>(KillObjective.Create, new());

	public abstract class DidPlaceBlockObjectiveTracker<O> : ObjectiveTracker<O>
		where O : IObjective
	{
		void DidPlaceBlock(IServerPlayer player, int oldBlockId, BlockSelection blockSel, ItemStack withItemStack)
		{
			if (ShouldStop(player, oldBlockId, blockSel, withItemStack))
			{
				return;
			}

			foreach (var objective in _objectives)
			{
				if (ShouldProgress(objective, player, oldBlockId, blockSel, withItemStack))
				{
					objective.Progress();
				}

			}
		}

		protected abstract bool ShouldStop(IServerPlayer player, int oldBlockId, BlockSelection blockSel, ItemStack withItemStack);
		protected abstract bool ShouldProgress(O objective, IServerPlayer player, int oldBlockId, BlockSelection blockSel, ItemStack withItemStack);

		protected override void Attach() =>
			ApiModHelper.Api.Event.DidPlaceBlock += DidPlaceBlock;

		protected override void Detach() =>
			ApiModHelper.Api.Event.DidPlaceBlock -= DidPlaceBlock;
	}

	//public class FlowersNearbyObjectiveTemplate<>

	#endregion
}
