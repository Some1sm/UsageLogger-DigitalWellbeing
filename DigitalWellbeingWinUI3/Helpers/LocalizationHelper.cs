using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Diagnostics;

namespace DigitalWellbeingWinUI3.Helpers
{
    public static class LocalizationHelper
    {
        private static ResourceLoader _resourceLoader;
        private static ResourceManager _resourceManager;
        private static string _currentLanguage = "";

        /// <summary>
        /// Gets a localized string for the given key, respecting the user's language preference.
        /// </summary>
        public static string GetString(string key)
        {
            try
            {
                EnsureResourceLoader();
                return _resourceLoader?.GetString(key) ?? key;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LocalizationHelper] GetString error for '{key}': {ex.Message}");
                return key;
            }
        }

        /// <summary>
        /// Forces recreation of the ResourceLoader with the current language preference.
        /// Call this when the language is changed.
        /// </summary>
        public static void RefreshLanguage()
        {
            _resourceLoader = null;
            _resourceManager = null;
            _currentLanguage = "";
            EnsureResourceLoader();
        }

        private static void EnsureResourceLoader()
        {
            string targetLanguage = UserPreferences.LanguageCode;
            
            // If language changed or first load, recreate the loader
            if (_resourceLoader == null || _currentLanguage != targetLanguage)
            {
                try
                {
                    _currentLanguage = targetLanguage;
                    
                    // Create ResourceManager and set language context
                    _resourceManager = new ResourceManager();
                    
                    if (!string.IsNullOrEmpty(targetLanguage))
                    {
                        // Create a context with the specific language
                        var context = _resourceManager.CreateResourceContext();
                        context.QualifierValues["Language"] = targetLanguage;
                        
                        // Get resource map and create loader from it
                        var resourceMap = _resourceManager.MainResourceMap.GetSubtree("Resources");
                        _resourceLoader = new ResourceLoader("Resources");
                        
                        Debug.WriteLine($"[LocalizationHelper] Created ResourceLoader with language: {targetLanguage}");
                    }
                    else
                    {
                        // Default loader
                        _resourceLoader = new ResourceLoader();
                        Debug.WriteLine("[LocalizationHelper] Created default ResourceLoader");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LocalizationHelper] EnsureResourceLoader error: {ex.Message}");
                    // Fallback to simple loader
                    try
                    {
                        _resourceLoader = new ResourceLoader();
                    }
                    catch
                    {
                        _resourceLoader = null;
                    }
                }
            }
        }
    }
}
