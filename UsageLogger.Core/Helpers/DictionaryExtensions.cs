using System;
using System.Collections.Generic;

namespace UsageLogger.Core.Helpers;

/// <summary>
/// Extension methods for common dictionary operations.
/// </summary>
public static class DictionaryExtensions
{
    /// <summary>
    /// Updates or inserts a value into a dictionary, or removes the key if the value satisfies a removal condition.
    /// Eliminates the common pattern of "if exists update, else add, unless value means remove".
    /// </summary>
    /// <typeparam name="TKey">Dictionary key type</typeparam>
    /// <typeparam name="TValue">Dictionary value type</typeparam>
    /// <param name="dictionary">The dictionary to modify</param>
    /// <param name="key">The key to upsert or remove</param>
    /// <param name="value">The value to set</param>
    /// <param name="shouldRemove">Predicate that returns true if the key should be removed instead of upserted</param>
    /// <returns>True if a modification was made (upsert or remove), false otherwise</returns>
    public static bool UpsertOrRemove<TKey, TValue>(
        this Dictionary<TKey, TValue> dictionary,
        TKey key,
        TValue value,
        Func<TValue, bool> shouldRemove) where TKey : notnull
    {
        if (shouldRemove(value))
        {
            return dictionary.Remove(key);
        }
        else
        {
            dictionary[key] = value; // C# indexer handles both add and update
            return true;
        }
    }

    /// <summary>
    /// Updates or inserts a value into a dictionary. Convenience overload without removal logic.
    /// </summary>
    public static void Upsert<TKey, TValue>(
        this Dictionary<TKey, TValue> dictionary,
        TKey key,
        TValue value) where TKey : notnull
    {
        dictionary[key] = value;
    }
}
