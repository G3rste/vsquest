using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VSQuest
{
	public static class ActionUtil
	{
		public static void SpawnEntities(ICoreServerAPI api, QuestMessage message, IPlayer _, string[] args)
		{
			foreach (var code in args)
			{
				TrySpawnEntity(api, message.QuestId, code, message.QuestGiverId);
			}
		}

		public static void SpawnAnyOfEntities(ICoreServerAPI api, QuestMessage message, IPlayer _, string[] args)
		{
			var code = args[api.World.Rand.Next(0, args.Length)];

			TrySpawnEntity(api, message.QuestId, code, message.QuestGiverId);
		}

		static void TrySpawnEntity(ICoreServerAPI api, string questId, string typeCode, long targetId)
		{
			var type = api.World.GetEntityType(new(typeCode)) ?? throw new QuestException($"Tried to spawn {typeCode} for quest {questId} but could not find the entity type!");

			var target = api.World.GetEntityById(targetId);
			var entity = api.World.ClassRegistry.CreateEntity(type);
			entity.Pos.SetFrom(target.Pos);
			api.World.SpawnEntity(entity);
		}

		public static void RecruitEntity(ICoreServerAPI sapi, QuestMessage message, IPlayer byPlayer, string[] _)
		{
			var recruit = sapi.World.GetEntityById(message.QuestGiverId);
			recruit.WatchedAttributes.SetDouble("employedSince", sapi.World.Calendar.TotalHours);
			recruit.WatchedAttributes.SetString("guardedPlayerUid", byPlayer.PlayerUID);
			recruit.WatchedAttributes.SetBool("commandSit", false);
			recruit.WatchedAttributes.MarkPathDirty("guardedPlayerUid");
		}

		public static void GiveItem(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
		{
			string code = args[0];
			CollectibleObject? item = sapi.World.GetItem(new AssetLocation(code));
			item ??= sapi.World.GetBlock(new AssetLocation(code));

			if (item == null)
			{
				throw new QuestException($"Could not find item {code} for quest {message.QuestId}!");
			}

			var stack = new ItemStack(item, int.Parse(args[1]));
			if (!byPlayer.InventoryManager.TryGiveItemstack(stack))
			{
				sapi.World.SpawnItemEntity(stack, byPlayer.Entity.Pos.XYZ);
			}
		}

		public static void CompleteQuest(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
		{
			var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
			QuestCompletedMessage questCompletedMessage = args.Length switch
			{
				1 => new QuestCompletedMessage(long.Parse(args[1]), args[0]),
				2 => new QuestCompletedMessage(message.QuestGiverId, args[0]),
				_ => new QuestCompletedMessage(message.QuestGiverId, message.QuestId),
			};
			questSystem.OnQuestCompleted(byPlayer, questCompletedMessage);
		}

		public static void SpawnSmoke(ICoreServerAPI sapi, QuestMessage message, IServerPlayer _, string[] __)
		{
			SimpleParticleProperties smoke = new(
					40, 60,
					ColorUtil.ToRgba(80, 100, 100, 100),
					new Vec3d(),
					new Vec3d(2, 1, 2),
					new Vec3f(-0.25f, 0f, -0.25f),
					new Vec3f(0.25f, 0f, 0.25f),
					0.6f,
					-0.075f,
					0.5f,
					3f,
					EnumParticleModel.Quad
				);
			var questgiver = sapi.World.GetEntityById(message.QuestGiverId);
			if (questgiver != null)
			{
				smoke.MinPos = questgiver.Pos.XYZ.AddCopy(-1.5, -0.5, -1.5);
				sapi.World.SpawnParticles(smoke);
			}
		}
		public static void AddTraits(ICoreServerAPI _, QuestMessage __, IServerPlayer byPlayer, string[] args)
		{
			var traits = byPlayer.Entity.WatchedAttributes
				.GetStringArray("extraTraits", [])
				.ToHashSet();
			traits.AddRange(args);
			byPlayer.Entity.WatchedAttributes
				.SetStringArray("extraTraits", [.. traits]);
		}
		public static void RemoveTraits(ICoreServerAPI _, QuestMessage __, IServerPlayer byPlayer, string[] args)
		{
			var traits = byPlayer.Entity.WatchedAttributes
				.GetStringArray("extraTraits", [])
				.ToHashSet();
			args.Foreach(trait => traits.Remove(trait));
			byPlayer.Entity.WatchedAttributes
				.SetStringArray("extraTraits", [.. traits]);
		}
	}


	public class QuestException : Exception
	{
		public QuestException() { }

		public QuestException(string message) : base(message) { }

		public QuestException(string message, Exception inner) : base(message, inner) { }
	}
}