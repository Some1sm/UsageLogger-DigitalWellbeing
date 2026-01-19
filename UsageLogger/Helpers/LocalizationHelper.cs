using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Diagnostics;

namespace UsageLogger.Helpers
{
    public static class LocalizationHelper
    {
        private static ResourceManager _resourceManager;
        private static ResourceContext _resourceContext;
        private static string _currentLanguage = "";

        /// <summary>
        /// Gets a localized string for the given key, respecting the user's language preference.
        /// </summary>
        public static string GetString(string key)
        {
            try
            {
                EnsureResources();
                // Use the ResourceManager with the specific context
                var map = _resourceManager.MainResourceMap.GetSubtree("Resources");
                var candidate = map.GetValue(key, _resourceContext);
                return candidate?.ValueAsString ?? key;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LocalizationHelper] GetString error for '{key}': {ex.Message}");
                return key;
            }
        }

        /// <summary>
        /// Forces recreation of the ResourceContext with the current language preference.
        /// Call this when the language is changed.
        /// </summary>
        public static void RefreshLanguage()
        {
            _resourceManager = null;
            _resourceContext = null;
            _currentLanguage = "";
            EnsureResources();
        }

        private static void EnsureResources()
        {
            string targetLanguage = UserPreferences.LanguageCode;
            
            // If language changed or first load, recreate the context
            if (_resourceManager == null || _currentLanguage != targetLanguage)
            {
                try
                {
                    _currentLanguage = targetLanguage;
                    
                    if (_resourceManager == null)
                        _resourceManager = new ResourceManager();
                    
                    _resourceContext = _resourceManager.CreateResourceContext();

                    if (!string.IsNullOrEmpty(targetLanguage))
                    {
                        // Set the language qualifier
                        _resourceContext.QualifierValues["Language"] = targetLanguage;
                        Debug.WriteLine($"[LocalizationHelper] Context set to language: {targetLanguage}");
                    }
                    else
                    {
                        // Default system context
                        Debug.WriteLine("[LocalizationHelper] Context set to default");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LocalizationHelper] EnsureResources error: {ex.Message}");
                    // Defensive init
                    if (_resourceManager == null) _resourceManager = new ResourceManager();
                    if (_resourceContext == null) _resourceContext = _resourceManager.CreateResourceContext();
                }
            }
        }
    }
}
