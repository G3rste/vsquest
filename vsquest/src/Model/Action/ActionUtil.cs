using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using VSQuest.Model.Server.Data;

namespace VSQuest.Server.Model.Action
{
	public static class ActionUtil
	{
		public static void PlaySound(IQuest quest, SoundData data) =>
			quest.Api.World.PlaySoundFor(AssetLocation.CreateOrNull(data.Path), quest.Player);

		public static void DespawnQuestGiver(IQuest quest, DelayData data) =>
			quest.Api.World.RegisterCallback(_ => ApiModHelper.GetEntity(quest.GiverId)?.Die(EnumDespawnReason.Removed), data.Milliseconds);

		public static void Heal(IQuest quest, HealData data) =>
			quest.Player.Entity.ReceiveDamage(new() { Type = EnumDamageType.Heal }, data.Amount);

		public static void AddPlayerAttribute(IQuest quest, AttributeData<string> data) =>
			quest.Player.Entity.WatchedAttributes.SetString(data.Name, data.Value);

		public static void RemovePlayerAttribute(IQuest quest, AttributeData data) =>
			quest.Player.Entity.WatchedAttributes.RemoveAttribute(data.Name);

		public static void SpawnEntities(IQuest quest, EntityData data)
		{
			if (ApiModHelper.GetEntity(quest.GiverId) is not Entity target)
			{
				return;
			}

			foreach (var code in data.Codes)
			{
				TrySpawnEntity(quest, code, target.Pos);
			}
		}

		public static void SpawnAnyOfEntities(IQuest quest, EntityData data)
		{
			var target = ApiModHelper.GetEntity(quest.GiverId) ?? quest.Player.Entity;
			var code = data.Codes[quest.Api.World.Rand.Next(0, data.Codes.Count)];
			TrySpawnEntity(quest, code, target.Pos);
		}

		static void TrySpawnEntity(IQuest quest, string typeCode, EntityPos pos)
		{
			var props = ApiModHelper.GetEntityProps(typeCode) ?? throw new Exception($"Tried to spawn {typeCode} forIQuest {quest.Id} but could not find the entity type!");
			var target = ApiModHelper.CreateEntity(props);
			target.Pos.SetFrom(pos);
			ApiModHelper.SpawnEntity(target);
		}

		public static void RecruitEntity(IQuest quest)
		{
			if (ApiModHelper.GetEntity(quest.GiverId) is not Entity recruit)
			{
				return;
			}

			recruit.WatchedAttributes.SetDouble("employedSince", quest.Api.World.Calendar.TotalHours);
			recruit.WatchedAttributes.SetString("guardedPlayerUid", quest.Player.PlayerUID);
			recruit.WatchedAttributes.SetBool("commandSit", false);
			recruit.WatchedAttributes.MarkPathDirty("guardedPlayerUid");
		}

		public static void GiveItem(IQuest quest, ItemData data)
		{
			var location = new AssetLocation(data.Code);
			CollectibleObject? item = quest.Api.World.GetItem(location);
			item ??= quest.Api.World.GetBlock(location);

			if (item == null)
			{
				throw new Exception($"Could not find item {data.Code} forIQuest {quest.Id}!");
			}

			var stack = new ItemStack(item, data.Amount);
			if (!quest.Player.InventoryManager.TryGiveItemstack(stack))
			{
				quest.Api.World.SpawnItemEntity(stack, quest.Player.Entity.Pos.XYZ);
			}
		}

		public static void AcceptQuest(IQuest quest, QuestData data)
		{
			var questId = data.Id ?? quest.Id;
			var giverId = data.GiverId ?? quest.GiverId;

			var questSystem = quest.Api.ModLoader.GetModSystem<QuestSystem>();
			questSystem.Server.OnQuestAccepted(quest.Player, new(questId, giverId));
		}

		public static void CompleteQuest(IQuest quest, QuestData data)
		{
			var questId = data.Id ?? quest.Id;
			var giverId = data.GiverId ?? quest.GiverId;

			var questSystem = quest.Api.ModLoader.GetModSystem<QuestSystem>();
			//questSystem.Server.OnQuestCompleted(quest.Player, new(questId, giverId));
		}

		public static void SpawnParticles(IQuest quest, ParticlesData data)
		{
			if (ApiModHelper.GetEntity(quest.GiverId) is not Entity target)
			{
				return;
			}

			var smoke = new SimpleParticleProperties(
				data.MinQty, data.MaxQty,
				ColorUtil.ColorFromRgba(data.Color),
				target.Pos.XYZ.AddCopy(-1.5, -0.5, -1.5),
				data.MaxPosAsVec,
				data.MinSpeedAsVec,
				data.MaxSpeedAsVec,
				data.Duration,
				data.Gravity,
				data.MinSize,
				data.MaxSize,
				data.Model
			);
			quest.Api.World.SpawnParticles(smoke);
		}

		public static void AddTraits(IQuest quest, TraitsData data)
		{
			var player = quest.Player;
			var traits = player.Entity.WatchedAttributes
				.GetStringArray("extraTraits", [])
				.ToHashSet();
			traits.AddRange(data.Labels);
			player.Entity.WatchedAttributes
				.SetStringArray("extraTraits", [.. traits]);
		}

		public static void RemoveTraits(IQuest quest, TraitsData data)
		{
			var player = quest.Player;
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