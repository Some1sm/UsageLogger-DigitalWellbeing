using System;
using System.Collections.Generic;
using System.Linq;

namespace DigitalWellbeingService.Helpers
{
    /// <summary>
    /// Tracks audio session persistence with debouncing logic.
    /// Audio sources must be active for multiple polling intervals to be considered "valid".
    /// </summary>
    public class AudioUsageTracker
    {
        private Dictionary<string, int> _audioPersistenceCounter = new Dictionary<string, int>();
        private const int REQUIRED_PERSISTENCE_COUNT = 2; // ~6 seconds at 3s interval

        /// <summary>
        /// Updates audio persistence state and returns apps that have been playing consistently.
        /// </summary>
        /// <param name="currentApps">List of currently active audio apps</param>
        /// <returns>List of apps that have been active long enough to count</returns>
        public List<string> UpdatePersistence(List<string> currentApps)
        {
            var validApps = new List<string>();
            // Use HashSet for O(1) lookup instead of O(n) List.Contains
            var currentAppsSet = new HashSet<string>(currentApps);

            // 1. Remove apps that stopped playing
            var keysToRemove = _audioPersistenceCounter.Keys
                .Where(key => !currentAppsSet.Contains(key))
                .ToList();
            foreach (var key in keysToRemove)
            {
                _audioPersistenceCounter.Remove(key);
            }

            // 2. Increment or add current apps
            foreach (var app in currentApps)
            {
                if (_audioPersistenceCounter.ContainsKey(app))
                {
                    _audioPersistenceCounter[app]++;
                }
                else
                {
                    _audioPersistenceCounter[app] = 1;
                }
            }

            // 3. Filter for >= 2 (approx > 3 seconds)
            foreach (var kvp in _audioPersistenceCounter)
            {
                if (kvp.Value >= REQUIRED_PERSISTENCE_COUNT)
                {
                    validApps.Add(kvp.Key);
                }
            }

            return validApps;
        }

        /// <summary>
        /// Clears all tracked audio state.
        /// </summary>
        public void Reset()
        {
            _audioPersistenceCounter.Clear();
        }
    }
}
