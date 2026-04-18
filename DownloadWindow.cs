using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace RMPlayer
{
    public class DownloadWindow : Window
    {
        private TextBox _urlTextBox;
        private ComboBox _folderComboBox;
        private RadioButton _audioRadio;
        private RadioButton _videoRadio;
        private Button _downloadButton;
        private TextBlock _statusLabel;

        public DownloadWindow()
        {
                // Принудительно загружаем язык
			if (Lang.Get("downloadWindow.Title") == "downloadWindow.Title")
				Lang.Load("ru");
			// ... остальное
			Title = Lang.Get("downloadWindow.Title");
            Width = 550;
            Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            this.SetResourceReference(BackgroundProperty, "WindowBackground");
            SettingsWindow.ApplyDarkTitleBar(this);

            var mainStack = new StackPanel { Margin = new Thickness(20) };
            Content = mainStack;

            var linkLabel = new TextBlock
            {
                Text = Lang.Get("downloadWindow.Link"),
                Margin = new Thickness(0, 0, 0, 5)
            };
            linkLabel.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryText");
            mainStack.Children.Add(linkLabel);

            _urlTextBox = new TextBox { Margin = new Thickness(0, 0, 0, 15) };
            mainStack.Children.Add(_urlTextBox);

            var pathLabel = new TextBlock
            {
                Text = Lang.Get("downloadWindow.Path"),
                Margin = new Thickness(0, 0, 0, 5)
            };
            pathLabel.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryText");
            mainStack.Children.Add(pathLabel);

            _folderComboBox = new ComboBox { Margin = new Thickness(0, 0, 0, 20) };
            foreach (var p in WeightedPath.All.Keys.OrderBy(p => p))
                _folderComboBox.Items.Add(p);
            if (_folderComboBox.Items.Count > 0)
                _folderComboBox.SelectedIndex = 0;
            mainStack.Children.Add(_folderComboBox);

            var formatPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 0) };

            _audioRadio = new RadioButton
            {
                Content = Lang.Get("downloadWindow.Audio"),
                IsChecked = true,
                Margin = new Thickness(0, 0, 20, 0)
            };
            _audioRadio.SetResourceReference(ForegroundProperty, "PrimaryText");

            _videoRadio = new RadioButton { Content = Lang.Get("downloadWindow.Video") };
            _videoRadio.SetResourceReference(ForegroundProperty, "PrimaryText");

            formatPanel.Children.Add(_audioRadio);
            formatPanel.Children.Add(_videoRadio);
            mainStack.Children.Add(formatPanel);

            _downloadButton = new Button
            {
                Content = Lang.Get("downloadWindow.Download"),
                Height = 40,
                Margin = new Thickness(0, 20, 0, 0)
            };
            _downloadButton.Click += StartDownload;
            mainStack.Children.Add(_downloadButton);

            _statusLabel = new TextBlock
            {
                Text = Lang.Get("downloadWindow.StatusReady"),
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            _statusLabel.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryText");
            mainStack.Children.Add(_statusLabel);
        }

        private async void StartDownload(object sender, RoutedEventArgs e)
        {
            string url = _urlTextBox.Text.Trim();
            string targetDir = _folderComboBox.SelectedItem?.ToString() ?? "";

            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(targetDir))
            {
                MessageBox.Show(Lang.Get("downloadWindow.EmptyFields"));
                return;
            }

            bool isAudio = _audioRadio.IsChecked == true;

            _downloadButton.IsEnabled = false;
            _statusLabel.Text = Lang.Get("downloadWindow.StatusDownloading");

            try
            {
                await Task.Run(() => RunDownload(url, targetDir, isAudio));

                RM.AddName(targetDir);
                MessageBox.Show(Lang.Get("downloadWindow.Success"));
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(Lang.Get("downloadWindow.Error") + ": " + ex.Message);
            }
            finally
            {
                _downloadButton.IsEnabled = true;
            }
        }

        private void RunDownload(string url, string targetDir, bool isAudio)
        {
            string ytdlpPath = @"C:\Users\Denis\Desktop\YoutubeDLP\yt-dlp.exe";
            string ffmpegPath = @"C:\Users\Denis\Desktop\YoutubeDLP\ffmpeg-6.1.1-full_build\bin";
            string fileNameTemplate = @"[%(upload_date>%d.%m.%Y)s] %(title)s.%(ext)s";
            string outputPath = Path.Combine(targetDir, fileNameTemplate);
            string format = isAudio ? "bestaudio/best" : "bestvideo+bestaudio/best";

            var psi = new ProcessStartInfo
            {
                FileName = ytdlpPath,
                Arguments = $@"-f {format} --ffmpeg-location ""{ffmpegPath}"" -o ""{outputPath}"" ""{url}""",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = Process.Start(psi))
            {
                if (process == null)
                    throw new Exception(Lang.Get("downloadWindow.ProcessError"));

                process.OutputDataReceived += (s, args) =>
                {
                    if (args.Data != null)
                        Dispatcher.Invoke(() => _statusLabel.Text = args.Data);
                };

                process.ErrorDataReceived += (s, args) =>
                {
                    if (args.Data != null)
                        Dispatcher.Invoke(() => _statusLabel.Text = "Err: " + args.Data);
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (process.ExitCode != 0)
                    throw new Exception(Lang.Get("downloadWindow.ExitCodeError") + $" ({process.ExitCode})");
            }
        }
    }
}