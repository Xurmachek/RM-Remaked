using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.IO;

namespace RMPlayer
{
    public class SettingsWindow : Window
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public SettingsWindow()
        {
            Title = Lang.Get("settingsWindow.Title");
            Width = 350;
            Height = 280;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            this.SetResourceReference(BackgroundProperty, "WindowBackground");
            this.SourceInitialized += (s, e) => ApplyDarkTitleBar(this);

            var stackPanel = new StackPanel { Margin = new Thickness(20) };
            Content = stackPanel;

            // Тема
            var themeLabel = new TextBlock { Text = Lang.Get("settingsWindow.ThemeTitle"), Margin = new Thickness(0, 0, 0, 5) };
            themeLabel.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryText");
            stackPanel.Children.Add(themeLabel);

            var themeCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 15) };
            themeCombo.Items.Add(Lang.Get("settingsWindow.Theme.Light"));
            themeCombo.Items.Add(Lang.Get("settingsWindow.Theme.Dark"));
            themeCombo.SelectedIndex = RM.IsDarkTheme.Val ? 1 : 0;
            themeCombo.SelectionChanged += (s, e) => {
                RM.IsDarkTheme.Val = themeCombo.SelectedIndex == 1;
                UpdateTheme();
            };
            stackPanel.Children.Add(themeCombo);

            // Язык
            var langLabel = new TextBlock { Text = "Язык / Language:", Margin = new Thickness(0, 10, 0, 5) };
            langLabel.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryText");
            stackPanel.Children.Add(langLabel);

            var langCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 15) };
            langCombo.Items.Add("Русский");
            langCombo.Items.Add("English");
            langCombo.SelectedIndex = Lang.CurrentCode == "en" ? 1 : 0;
            langCombo.SelectionChanged += (s, e) => {
                Lang.Load(langCombo.SelectedIndex == 1 ? "en" : "ru");
                var newWin = new SettingsWindow { Owner = this.Owner };
                this.Close();
                newWin.ShowDialog();
            };
            stackPanel.Children.Add(langCombo);

            // TTS
            var ttsLabel = new TextBlock { Text = Lang.Get("settingsWindow.TSSSpeech"), Margin = new Thickness(0, 0, 0, 5) };
            ttsLabel.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryText");
            stackPanel.Children.Add(ttsLabel);

            var ttsCheckbox = new CheckBox { Margin = new Thickness(0, 0, 0, 15) };
            ttsCheckbox.IsChecked = RM.IsTtsEnabled.Val;
            ttsCheckbox.SetResourceReference(ForegroundProperty, "PrimaryText");
            ttsCheckbox.Checked += (_, _) => RM.IsTtsEnabled.Val = true;
            ttsCheckbox.Unchecked += (_, _) => RM.IsTtsEnabled.Val = false;
            stackPanel.Children.Add(ttsCheckbox);

            var closeBtn = new Button { Content = Lang.Get("settingsWindow.Close"), Width = 100 };
            closeBtn.Click += (s, e) => Close();
            stackPanel.Children.Add(closeBtn);
        }

        public static void UpdateTheme()
        {
            var themeName = RM.IsDarkTheme.Val ? "Dark" : "Light";
            var uri = new Uri($"Themes/{themeName}.xaml", UriKind.Relative);
            
            try {
                ResourceDictionary resourceDict = Application.LoadComponent(uri) as ResourceDictionary;
                if (resourceDict != null)
                {
                    Application.Current.Resources.MergedDictionaries.Clear();
                    Application.Current.Resources.MergedDictionaries.Add(resourceDict);
                }
            } catch { }
            
            foreach (Window window in Application.Current.Windows)
            {
                window.SetResourceReference(BackgroundProperty, "WindowBackground");
                ApplyDarkTitleBar(window);
            }
        }

        public static void ApplyDarkTitleBar(Window window)
        {
            try {
                IntPtr hwnd = new WindowInteropHelper(window).EnsureHandle();
                int useDark = RM.IsDarkTheme.Val ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
            } catch { }
        }
    }
}