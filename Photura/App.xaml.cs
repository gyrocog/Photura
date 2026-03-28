using System;
using System.Windows;
using Microsoft.Win32;
using Photura.Views;

namespace Photura
{
    public partial class App : Application
    {
        private const string RegKey = @"Software\Photura";

        public static bool IsDarkTheme { get; private set; } = true;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Read saved theme preference
            IsDarkTheme = LoadThemePreference();

            // Apply correct theme — overrides the default in App.xaml
            ApplyTheme(IsDarkTheme);

            var viewer = new ViewerView();
            viewer.Show();

            if (e.Args.Length > 0)
                viewer.OpenImageFromPath(e.Args[0]);
        }

        private static bool LoadThemePreference()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegKey);
                if (key == null) return true; // default dark
                var val = key.GetValue("DarkTheme");
                if (val == null) return true;
                return Convert.ToBoolean(val);
            }
            catch
            {
                return true;
            }
        }

        public static void SwitchTheme(bool dark)
        {
            IsDarkTheme = dark;
            ApplyTheme(dark);

            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegKey);
                key.SetValue("DarkTheme", dark.ToString());
            }
            catch { }
        }

        private static void ApplyTheme(bool dark)
        {
            var dict = new ResourceDictionary
            {
                Source = new Uri(dark
                    ? "Themes/DarkTheme.xaml"
                    : "Themes/LightTheme.xaml",
                    UriKind.Relative)
            };
            Current.Resources.MergedDictionaries.Clear();
            Current.Resources.MergedDictionaries.Add(dict);
        }
    }
}