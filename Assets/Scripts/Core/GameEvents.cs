// Cosmic Bloom — central gameplay event hub. Systems publish; the quest
// engine (and UI) subscribe. Keeps NPC/farming/animal systems decoupled from
// quest logic — quests are data, triggers are events.
using System;

namespace CosmicBloom.Core
{
    public static class GameEvents
    {
        public static event Action<string> OnTalkedTo;                       // npcId
        public static event Action<string, string> OnCropPlanted;           // cropId, regionId
        public static event Action<string, string> OnCropHarvested;         // cropId, regionId
        public static event Action<string> OnPlotTilled;                    // regionId
        public static event Action<string, string> OnLandmarkRestored;      // landmarkId, regionId
        public static event Action<string, float> OnRestorationChanged;     // regionId, value
        public static event Action<string> OnRegionUnlocked;                // regionId
        public static event Action<string> OnRegionEntered;                 // regionId
        public static event Action<string> OnAnimalBought;                  // animalId
        public static event Action<string> OnItemGranted;                   // itemId (quest rewards)
        public static event Action OnFishCaught;                            // pond fishing (catch_fish steps)
        public static event Action OnMeteorShower;                          // scheduled event (wait_for/witness_event)

        public static void TalkedTo(string npcId) => OnTalkedTo?.Invoke(npcId);
        public static void CropPlanted(string cropId, string regionId) => OnCropPlanted?.Invoke(cropId, regionId);
        public static void CropHarvested(string cropId, string regionId) => OnCropHarvested?.Invoke(cropId, regionId);
        public static void PlotTilled(string regionId) => OnPlotTilled?.Invoke(regionId);
        public static void LandmarkRestored(string landmarkId, string regionId) => OnLandmarkRestored?.Invoke(landmarkId, regionId);
        public static void RestorationChanged(string regionId, float value) => OnRestorationChanged?.Invoke(regionId, value);
        public static void RegionUnlocked(string regionId) => OnRegionUnlocked?.Invoke(regionId);
        public static void RegionEntered(string regionId) => OnRegionEntered?.Invoke(regionId);
        public static void AnimalBought(string animalId) => OnAnimalBought?.Invoke(animalId);
        public static void ItemGranted(string itemId) => OnItemGranted?.Invoke(itemId);
        public static void FishCaught() => OnFishCaught?.Invoke();
        public static void MeteorShower() => OnMeteorShower?.Invoke();

        /// <summary>Tests/domain-reload hygiene: drop all subscribers.</summary>
        public static void Clear()
        {
            OnTalkedTo = null; OnCropPlanted = null; OnCropHarvested = null;
            OnPlotTilled = null; OnLandmarkRestored = null; OnRestorationChanged = null;
            OnRegionUnlocked = null; OnRegionEntered = null; OnAnimalBought = null;
            OnItemGranted = null; OnFishCaught = null; OnMeteorShower = null;
        }
    }
}
