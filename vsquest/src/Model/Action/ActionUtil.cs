using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using VSQuest.Server;

namespace VSQuest.Model.Action
{
	public static class ActionUtil
	{
		public static void PlaySound(ICoreServerAPI api, QuestInfo info, SoundData data) =>
			api.World.PlaySoundFor(AssetLocation.CreateOrNull(data.Path), info.Player);

		public static void DespawnQuestGiver(ICoreServerAPI api, QuestInfo info, DelayData data) =>
			api.World.RegisterCallback(_ => api.World.GetEntityById(info.GiverId).Die(EnumDespawnReason.Removed), data.Milliseconds);

		public static void Heal(ICoreServerAPI _, QuestInfo info, HealData data) =>
			info.Player.Entity.ReceiveDamage(new() { Type = EnumDamageType.Heal }, data.Amount);

		public static void AddPlayerAttribute(ICoreServerAPI _, QuestInfo info, AttributeData<string> data) =>
			info.Player.Entity.WatchedAttributes.SetString(data.Name, data.Value);

		public static void RemovePlayerAttribute(ICoreServerAPI _, QuestInfo info, AttributeData data) =>
			info.Player.Entity.WatchedAttributes.RemoveAttribute(data.Name);

		public static void SpawnEntities(ICoreServerAPI api, QuestInfo info, EntityData data)
		{
			var pos = api.World.GetEntityById(info.GiverId).Pos;
			foreach (var code in data.Codes)
			{
				TrySpawnEntity(info, code, pos);
			}
		}

		public static void SpawnAnyOfEntities(ICoreServerAPI api, QuestInfo info, EntityData data)
		{
			var code = data.Codes[api.World.Rand.Next(0, data.Codes.Count)];
			var pos = api.World.GetEntityById(info.GiverId).Pos;
			TrySpawnEntity(info, code, pos);
		}

		static void TrySpawnEntity(QuestInfo info, string typeCode, EntityPos pos)
		{
			var props = ApiModHelper.GetEntityProps(typeCode) ?? throw new Exception($"Tried to spawn {typeCode} for quest {info.Id} but could not find the entity type!");
			var entity = ApiModHelper.CreateEntity(props);
			entity.Pos.SetFrom(pos);
			ApiModHelper.SpawnEntity(entity);
		}

		public static void RecruitEntity(ICoreServerAPI api, QuestInfo info)
		{
			var recruit = api.World.GetEntityById(info.GiverId);
			recruit.WatchedAttributes.SetDouble("employedSince", api.World.Calendar.TotalHours);
			recruit.WatchedAttributes.SetString("guardedPlayerUid", info.Player.PlayerUID);
			recruit.WatchedAttributes.SetBool("commandSit", false);
			recruit.WatchedAttributes.MarkPathDirty("guardedPlayerUid");
		}

		public static void GiveItem(ICoreServerAPI api, QuestInfo info, ItemData data)
		{
			var location = new AssetLocation(data.Code);
			CollectibleObject? item = api.World.GetItem(location);
			item ??= api.World.GetBlock(location);

			if (item == null)
			{
				throw new Exception($"Could not find item {data.Code} for quest {info.Id}!");
			}

			var stack = new ItemStack(item, data.Amount);
			if (!info.Player.InventoryManager.TryGiveItemstack(stack))
			{
				api.World.SpawnItemEntity(stack, info.Player.Entity.Pos.XYZ);
			}
		}

		public static void AcceptQuest(ICoreServerAPI api, QuestInfo info, QuestData data)
		{
			var questId = data.Id ?? info.Id;
			var giverId = data.GiverId ?? info.GiverId;

			var questSystem = api.ModLoader.GetModSystem<QuestSystem>();
			questSystem.Server.OnQuestAccepted(info.Player, new(questId, giverId));
		}

		public static void CompleteQuest(ICoreServerAPI api, QuestInfo info, QuestData data)
		{
			var questId = data.Id ?? info.Id;
			var giverId = data.GiverId ?? info.GiverId;

			var questSystem = api.ModLoader.GetModSystem<QuestSystem>();
			questSystem.Server.OnQuestCompleted(info.Player, new(questId, giverId));
		}

		public static void SpawnParticles(ICoreServerAPI api, QuestInfo info, ParticlesData data)
		{
			var questgiver = api.World.GetEntityById(info.GiverId);
			if (questgiver == null)
			{
				return;
			}

			var smoke = new SimpleParticleProperties(
				data.MinQty, data.MaxQty,
				ColorUtil.ColorFromRgba(data.Color),
				questgiver.Pos.XYZ.AddCopy(-1.5, -0.5, -1.5),
				data.MaxPosAsVec,
				data.MinSpeedAsVec,
				data.MaxSpeedAsVec,
				data.Duration,
				data.Gravity,
				data.MinSize,
				data.MaxSize,
				data.Model
			);
			api.World.SpawnParticles(smoke);
		}

		public static void AddTraits(ICoreServerAPI _, QuestInfo info, TraitsData data)
		{
			var player = info.Player;
			var traits = player.Entity.WatchedAttributes
				.GetStringArray("extraTraits", [])
				.ToHashSet();
			traits.AddRange(data.Labels);
			player.Entity.WatchedAttributes
				.SetStringArray("extraTraits", [.. traits]);
		}

		public static void RemoveTraits(ICoreServerAPI _, QuestInfo info, TraitsData data)
		{
			var player = info.Player;
			var traits = player.Entity.WatchedAttributes
				.GetStringArray("extraTraits", [])
				.ToHashSet();

			foreach (var label in data.Labels)
			{
				traits.Remove(label);
			}
			player.Entity.WatchedAttributes
				.SetStringArray("extraTraits", [.. traits]);
		}
	}
}