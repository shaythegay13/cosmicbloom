// Cosmic Bloom — day/night cycle. Crops with growsAt "day"/"night" only
// advance during their window; some NPCs appear only at night.
using System;
using UnityEngine;

namespace CosmicBloom.Core
{
    public class TimeManager : MonoBehaviour
    {
        public static TimeManager Instance { get; private set; }

        [Tooltip("Real-time minutes for one full in-game day")]
        public float dayLengthMinutes = 24f;

        [Tooltip("0 = midnight, 0.25 = sunrise, 0.5 = noon, 0.75 = sunset")]
        [Range(0f, 1f)] public float timeOfDay = 0.3f;

        [Tooltip("Night spans [nightStart, 1] and [0, dayStart]")]
        public float dayStart = 0.25f;
        public float nightStart = 0.78f;

        public bool IsNight => timeOfDay >= nightStart || timeOfDay < dayStart;
        public bool IsDay => !IsNight;
        public int DayNumber { get; private set; } = 1;

        /// <summary>
        /// P3-Phase 16: the daylight span [dayStart, nightStart) split into three
        /// equal periods so NPC schedules get intermediate stops; Night is the
        /// existing night window unchanged. Boundaries derive from dayStart/
        /// nightStart, so the day/night binary and the periods can never disagree
        /// (Morning|Midday|Evening ⇔ IsDay).
        /// </summary>
        public enum DayPeriod { Morning, Midday, Evening, Night }

        public DayPeriod CurrentPeriod
        {
            get
            {
                if (IsNight) return DayPeriod.Night;
                float third = (nightStart - dayStart) / 3f;
                if (timeOfDay < dayStart + third) return DayPeriod.Morning;
                if (timeOfDay < dayStart + third * 2f) return DayPeriod.Midday;
                return DayPeriod.Evening;
            }
        }

        public event Action<DayPeriod> OnPeriodChanged;

        /// <summary>Restore the persisted day counter on load.</summary>
        public void SetDayNumber(int day) => DayNumber = Mathf.Max(1, day);

        public event Action OnSunrise;
        public event Action OnNightfall;
        public event Action<int> OnNewDay;

        bool _wasNight;
        DayPeriod _lastPeriod;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _wasNight = IsNight;
            _lastPeriod = CurrentPeriod;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            float prev = timeOfDay;
            timeOfDay += Time.deltaTime / (dayLengthMinutes * 60f);
            if (timeOfDay >= 1f)
            {
                timeOfDay -= 1f;
                DayNumber++;
                OnNewDay?.Invoke(DayNumber);
            }

            if (_wasNight && IsDay) OnSunrise?.Invoke();
            else if (!_wasNight && IsNight) OnNightfall?.Invoke();
            _wasNight = IsNight;

            var period = CurrentPeriod;
            if (period != _lastPeriod)
            {
                _lastPeriod = period;
                OnPeriodChanged?.Invoke(period);
            }
        }

        /// <summary>Can a crop with this growsAt window ("day"/"night"/"any") grow right now?</summary>
        public bool GrowWindowOpen(string growsAt) => growsAt switch
        {
            "day" => IsDay,
            "night" => IsNight,
            _ => true,
        };

        /// <summary>Ambient light 0 (deep night) → 1 (noon), for URP 2D global light.</summary>
        public float AmbientIntensity()
        {
            // Smooth curve peaking at noon (0.5), floor of 0.18 at midnight
            float d = Mathf.Cos((timeOfDay - 0.5f) * 2f * Mathf.PI) * 0.5f + 0.5f;
            return Mathf.Lerp(0.18f, 1f, d);
        }
    }
}
