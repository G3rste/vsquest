using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class ActionUtil
    {
        private ActionUtil()
        {
        }

        public static void SpawnEntities(ICoreServerAPI sapi, QuestMessage message, IPlayer byPlayer, string[] args)
        {
            foreach (var code in args)
            {
                var type = sapi.World.GetEntityType(new AssetLocation(code));
                if (type == null)
                {
                    throw new QuestException(string.Format("Tried to spawn {0} for quest {1} but could not find the entity type!", code, message.questId));
                }
                var entity = sapi.World.ClassRegistry.CreateEntity(type);
                entity.ServerPos = sapi.World.GetEntityById(message.questGiverId).ServerPos.Copy();
                sapi.World.SpawnEntity(entity);
            }
        }

        public static void SpawnAnyOfEntities(ICoreServerAPI sapi, QuestMessage message, IPlayer byPlayer, string[] args)
        {
            var code = args[sapi.World.Rand.Next(0, args.Length)];
            var type = sapi.World.GetEntityType(new AssetLocation(code));
            if (type == null)
            {
                throw new QuestException(string.Format("Tried to spawn {0} for quest {1} but could not find the entity type!", code, message.questId));
            }
            var entity = sapi.World.ClassRegistry.CreateEntity(type);
            entity.ServerPos = sapi.World.GetEntityById(message.questGiverId).ServerPos.Copy();
            sapi.World.SpawnEntity(entity);
        }

        public static void RecruitEntity(ICoreServerAPI sapi, QuestMessage message, IPlayer byPlayer, string[] args)
        {
            var recruit = sapi.World.GetEntityById(message.questGiverId);
            recruit.WatchedAttributes.SetDouble("employedSince", sapi.World.Calendar.TotalHours);
            recruit.WatchedAttributes.SetString("guardedPlayerUid", byPlayer.PlayerUID);
            recruit.WatchedAttributes.SetBool("commandSit", false);
            recruit.WatchedAttributes.MarkPathDirty("guardedPlayerUid");
        }

        public static void GiveItem(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            string code = args[0];
            CollectibleObject item = sapi.World.GetItem(new AssetLocation(code));
            if (item == null)
            {
                item = sapi.World.GetBlock(new AssetLocation(code));
            }
            if (item == null)
            {
                throw new QuestException(string.Format("Could not find item {0} for quest {1}!", code, message.questId));
            }

            var stack = new ItemStack(item, int.Parse(args[1]));
            if (!byPlayer.InventoryManager.TryGiveItemstack(stack))
            {
                sapi.World.SpawnItemEntity(stack, byPlayer.Entity.ServerPos.XYZ);
            }
        }
    }


    public class QuestException : Exception
    {
        public QuestException() { }

        public QuestException(string message) : base(message) { }

        public QuestException(string message, Exception inner) : base(message, inner) { }
    }
}