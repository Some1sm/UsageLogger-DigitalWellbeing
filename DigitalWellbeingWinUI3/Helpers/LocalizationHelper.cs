using Microsoft.Windows.ApplicationModel.Resources;
using System;

namespace DigitalWellbeingWinUI3.Helpers
{
    public static class LocalizationHelper
    {
        private static ResourceLoader _resourceLoader;

        public static string GetString(string key)
        {
            if (_resourceLoader == null)
            {
                try
                {
                    _resourceLoader = new ResourceLoader();
                }
                catch
                {
                    // Fallback for design time or if ResourceLoader fails
                    return key;
                }
            }
            return _resourceLoader.GetString(key);
        }
    }
}
