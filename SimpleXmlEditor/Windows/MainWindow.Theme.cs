using System;
using System.Windows;

namespace SimpleXmlEditor
{
    public partial class MainWindow
    {
        // App accent palettes (light-purple personality colors only;
        // generic chrome comes from the HandyControl skin dictionaries).
        private static readonly Uri LightAccentUri = new Uri("pack://application:,,,/Themes/LightColors.xaml");
        private static readonly Uri DarkAccentUri = new Uri("pack://application:,,,/Themes/DarkColors.xaml");

        private static readonly Uri HcDefaultSkinUri = new Uri("pack://application:,,,/HandyControl;component/Themes/SkinDefault.xaml");
        private static readonly Uri HcDarkSkinUri = new Uri("pack://application:,,,/HandyControl;component/Themes/SkinDark.xaml");

        private void ApplyTheme()
        {
            // 1) Switch the HandyControl skin (official theme dictionaries) —
            //    all HandyControl-styled controls & semantic brushes adapt automatically.
            ReplaceMergedDictionary(
                _isDarkMode ? HcDarkSkinUri : HcDefaultSkinUri,
                d => d.Source?.OriginalString.Contains("/HandyControl;component/Themes/Skin") == true);

            // 2) Switch the app accent palette (toolbar/column-header/chip tints).
            ReplaceMergedDictionary(
                _isDarkMode ? DarkAccentUri : LightAccentUri,
                d => d.Source == LightAccentUri || d.Source == DarkAccentUri);
        }

        /// <summary>
        /// Replace (or insert) a merged resource dictionary while keeping its original
        /// position, so StaticResource resolution order inside Theme.xaml stays intact.
        /// </summary>
        private static void ReplaceMergedDictionary(Uri source, Func<ResourceDictionary, bool> match)
        {
            var merged = Application.Current.Resources.MergedDictionaries;
            var index = merged.Count;
            for (var i = 0; i < merged.Count; i++)
            {
                if (match(merged[i]))
                {
                    index = i;
                    merged.RemoveAt(i);
                    break;
                }
            }
            merged.Insert(Math.Min(index, merged.Count), new ResourceDictionary { Source = source });
        }
    }
}
