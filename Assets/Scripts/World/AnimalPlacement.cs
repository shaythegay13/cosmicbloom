// Cosmic Bloom — P3-Phase 18: "place mode" for livestock, the creature
// equivalent of Phase 17's tap-to-till. RanchUI arms an owned livestock
// animal; the next ground tap in the world places it (PlayerController routes
// the tap here before its interact/till branches). Static + fire-and-forget,
// matching how FarmGrid.ForRegion lets the tap path reach systems it isn't
// wired into at spawn.
namespace CosmicBloom.World
{
    public static class AnimalPlacement
    {
        /// <summary>Animal id awaiting a ground tap, or null when not placing.</summary>
        public static string Armed { get; private set; }

        public static bool IsArmed => Armed != null;

        /// <summary>Arm placement for a livestock animal. Companions are never
        /// placeable — they walk a schedule, so a fixed spot would fight it.</summary>
        public static bool Arm(string animalId)
        {
            if (animalId == null || !AnimalRoles.IsLivestock(animalId)) return false;
            Armed = animalId;
            return true;
        }

        public static void Disarm() => Armed = null;
    }
}
