using System.Collections.Generic;
using VintagestoryAPI.Math;

namespace VsQuest
{
    public class Quest
    {
        public long questGiverId { get; set; }
        public string questTitle { get; set; }
        public string questDesc { get; set; }
        public List<Objective> objectives { get; set; }
    }

    public abstract class Objective
    {
        public abstract float progress { get; }
    }

    public class KillObjective : Objective
    {
        public override float progress => (float)entitiesKilled / (float)entitiesToKill;
        HashSet<string> validEntityCodes { get; set; }

        public int entitiesToKill { get; set; }
        public int entitiesKilled { get; private set; }

        public void OnEntityKilled() { if (entitiesKilled < entitiesToKill) entitiesKilled++; }
    }
}