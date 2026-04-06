using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
            Title = Lang.Get("downloadWindow.Title");
            Width = 550;
            Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            this.SetResourceReference(BackgroundProperty, "WindowBackground");
            SettingsWindow.ApplyDarkTitleBar(this);

            var mainStack = new StackPanel { Margin = new Thickness(20) };
            Content = mainStack;

            // Ссылка
            var linkLabel = new TextBlock { Text = Lang.Get("downloadWindow.Link"), Margin = new Thickness(0, 0, 0, 5) };
            linkLabel.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryText");
            mainStack.Children.Add(linkLabel);
            
            _urlTextBox = new TextBox { Margin = new Thickness(0, 0, 0, 15) };
            mainStack.Children.Add(_urlTextBox);

            // Выбор папки
            var pathLabel = new TextBlock { Text = Lang.Get("downloadWindow.Path"), Margin = new Thickness(0, 0, 0, 5) };
            pathLabel.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryText");
            mainStack.Children.Add(pathLabel);
            _folderComboBox = new ComboBox { Margin = new Thickness(0, 0, 0, 20) };
            
            var allPaths = WeightedPath.All.Keys.OrderBy(p => p).ToList();
            foreach (var p in allPaths) _folderComboBox.Items.Add(p);
            if (_folderComboBox.Items.Count > 0) _folderComboBox.SelectedIndex = 0;
            mainStack.Children.Add(_folderComboBox);

            // Формат
            _audioRadio = new RadioButton { Content = Lang.Get("downloadWindow.Audio"), IsChecked = true, Margin = new Thickness(0, 0, 20, 0) };
            _videoRadio = new RadioButton { Content = Lang.Get("downloadWindow.Video") };
            _audioRadio.SetResourceReference(ForegroundProperty, "PrimaryText");
            _videoRadio.SetResourceReference(ForegroundProperty, "PrimaryText");
            mainStack.Children.Add(_audioRadio);
            mainStack.Children.Add(_videoRadio);

            _downloadButton = new Button { Content = Lang.Get("downloadWindow.Download"), Height = 40, Margin = new Thickness(0, 20, 0, 0) };
            _downloadButton.Click += StartDownload;
            mainStack.Children.Add(_downloadButton);

            _statusLabel = new TextBlock { Text = Lang.Get("downloadWindow.StatusReady"), Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
            _statusLabel.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryText");
            mainStack.Children.Add(_statusLabel);
        }

        private async void StartDownload(object sender, RoutedEventArgs e)
        {
            string url = _urlTextBox.Text.Trim();
            string targetDir = _folderComboBox.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(targetDir)) return;

            _downloadButton.IsEnabled = false;
            _statusLabel.Text = Lang.Get("downloadWindow.StatusDownloading");
            
            try
            {
                await Task.Run(() =>
                {
                    string ytdlpPath = @"C:\Users\Denis\Desktop\YoutubeDLP\yt-dlp.exe";
                    string ffmpegPath = @"C:\Users\Denis\Desktop\YoutubeDLP\ffmpeg-6.1.1-full_build\bin";
                    
                    string fileNameTemplate = @"[%(upload_date>%d.%m.%Y)s] %(title)s.%(ext)s";
                    string outputPath = Path.Combine(targetDir, fileNameTemplate);
                    
                    string format = _audioRadio.IsChecked == true ? "bestaudio/best" : "bestvideo+bestaudio/best";

                    var psi = new ProcessStartInfo
                    {
                        FileName = ytdlpPath,
                        Arguments = $@"-f {format} --ffmpeg-location ""{ffmpegPath}"" -o ""{outputPath}"" ""{url}""",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using (var p = Process.Start(psi))
                    {
                        p.OutputDataReceived += (s, args) => { if(args.Data != null) Application.Current.Dispatcher.Invoke(() => _statusLabel.Text = args.Data); };
                        p.ErrorDataReceived  += (s, args) => { if(args.Data != null) Application.Current.Dispatcher.Invoke(() => _statusLabel.Text = "Err: " + args.Data); };
                        p.BeginOutputReadLine();
                        p.BeginErrorReadLine();
                        p.WaitForExit();
                    }
                });

                Application.Current.Dispatcher.Invoke(() => 
                {
                    RM.AddName(targetDir);
                    MessageBox.Show(Lang.Get("downloadWindow.Success"));
                    Close();
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() => MessageBox.Show(Lang.Get("downloadWindow.Error") + ": " + ex.Message));
            }
            finally 
            { 
                Application.Current.Dispatcher.Invoke(() => _downloadButton.IsEnabled = true); 
            }
        }
    }
}