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
            Width = 450;
            Height = 550;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            this.SetResourceReference(BackgroundProperty, "WindowBackground");
            this.SourceInitialized += (s, e) => ApplyDarkTitleBar(this);

            var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Content = scrollViewer;

            var stackPanel = new StackPanel { Margin = new Thickness(20) };
            scrollViewer.Content = stackPanel;

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

            // yt-dlp path
            var ytdlpLabel = new TextBlock { Text = Lang.Get("settingsWindow.YtDlpPath"), Margin = new Thickness(0, 10, 0, 5) };
            ytdlpLabel.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryText");
            stackPanel.Children.Add(ytdlpLabel);

            var ytdlpGrid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            ytdlpGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ytdlpGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            var ytdlpBox = new TextBox { Text = RM.YtDlpPath, VerticalAlignment = VerticalAlignment.Center };
            ytdlpBox.TextChanged += (s, e) => RM.YtDlpPath = ytdlpBox.Text;
            Grid.SetColumn(ytdlpBox, 0);
            ytdlpGrid.Children.Add(ytdlpBox);

            var ytdlpBrowse = new Button { Content = Lang.Get("settingsWindow.Browse"), Margin = new Thickness(5, 0, 0, 0), Padding = new Thickness(10, 0, 10, 0) };
            ytdlpBrowse.Click += (s, e) => {
                var d = new Microsoft.Win32.OpenFileDialog { Filter = "yt-dlp.exe|yt-dlp.exe|All files|*" };
                if (d.ShowDialog() == true) ytdlpBox.Text = d.FileName;
            };
            Grid.SetColumn(ytdlpBrowse, 1);
            ytdlpGrid.Children.Add(ytdlpBrowse);
            stackPanel.Children.Add(ytdlpGrid);

            // FFmpeg path
            var ffmpegLabel = new TextBlock { Text = Lang.Get("settingsWindow.FfmpegPath"), Margin = new Thickness(0, 10, 0, 5) };
            ffmpegLabel.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryText");
            stackPanel.Children.Add(ffmpegLabel);

            var ffmpegGrid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            ffmpegGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ffmpegGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var ffmpegBox = new TextBox { Text = RM.FfmpegPath, VerticalAlignment = VerticalAlignment.Center };
            ffmpegBox.TextChanged += (s, e) => RM.FfmpegPath = ffmpegBox.Text;
            Grid.SetColumn(ffmpegBox, 0);
            ffmpegGrid.Children.Add(ffmpegBox);

            var ffmpegBrowse = new Button { Content = Lang.Get("settingsWindow.Browse"), Margin = new Thickness(5, 0, 0, 0), Padding = new Thickness(10, 0, 10, 0) };
            ffmpegBrowse.Click += (s, e) => {
                // Folder selection is tricky in old WPF, using standard hack or System.Windows.Forms is usually needed
                // But for simplicity let's use OpenFileDialog and get directory
                var d = new Microsoft.Win32.OpenFileDialog { Title = "Select any file in FFmpeg bin folder", Filter = "All files|*.*" };
                if (d.ShowDialog() == true) ffmpegBox.Text = Path.GetDirectoryName(d.FileName) ?? "";
            };
            Grid.SetColumn(ffmpegBrowse, 1);
            ffmpegGrid.Children.Add(ffmpegBrowse);
            stackPanel.Children.Add(ffmpegGrid);

            var closeBtn = new Button { Content = Lang.Get("settingsWindow.Close"), Width = 100, Margin = new Thickness(0, 20, 0, 0) };
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