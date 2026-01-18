#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace DigitalWellbeingService.Helpers;

/// <summary>
/// Tracks audio session persistence with debouncing logic.
/// Audio sources must be active for multiple polling intervals to be considered "valid".
/// </summary>
public class AudioUsageTracker
{
    private readonly Dictionary<string, int> _audioPersistenceCounter = [];
    private const int RequiredPersistenceCount = 2; // ~6 seconds at 3s interval

    /// <summary>
    /// Updates audio persistence state and returns apps that have been playing consistently.
    /// </summary>
    public List<string> UpdatePersistence(List<string> currentApps)
    {
        var validApps = new List<string>();
        var currentAppsSet = new HashSet<string>(currentApps);

        // Remove apps that stopped playing
        var keysToRemove = _audioPersistenceCounter.Keys
            .Where(key => !currentAppsSet.Contains(key))
            .ToList();
        
        foreach (var key in keysToRemove)
        {
            _audioPersistenceCounter.Remove(key);
        }

        // Increment or add current apps
        foreach (var app in currentApps)
        {
            if (!_audioPersistenceCounter.TryAdd(app, 1))
            {
                _audioPersistenceCounter[app]++;
            }
        }

        // Filter for apps with sufficient persistence
        foreach (var (key, count) in _audioPersistenceCounter)
        {
            if (count >= RequiredPersistenceCount)
            {
                validApps.Add(key);
            }
        }

        return validApps;
    }

    /// <summary>
    /// Clears all tracked audio state.
    /// </summary>
    public void Reset() => _audioPersistenceCounter.Clear();
}
