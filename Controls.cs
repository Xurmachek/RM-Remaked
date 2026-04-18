using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RMPlayer
{
    public abstract class IconButtonBase : Button
    {
        public const double ContentSize = 32;

        protected IconButtonBase()
        {
            var canvas = new Canvas
            {
                Width = ContentSize,
                Height = ContentSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            Content = canvas;
            BuildContent(canvas);
        }

        protected abstract void BuildContent(Canvas canvas);
    }

    public abstract class ButtonSwitch : IconButtonBase
    {
        protected ButtonSwitch(BoolValue source)
        {
            if (Content is not Canvas canvas) return;

            void SetState() =>
                canvas.Background = source.Val
                    ? new SolidColorBrush(Color.FromArgb(128, 255, 0, 0))
                    : Brushes.Transparent;

            SetState();

            Click += (_, e) =>
            {
                source.Val = !source.Val;
                SetState();
                e.Handled = true;
            };
        }
    }

    public sealed class CycleButton : ButtonSwitch
    {
        public CycleButton() : base(RM.Cycle) { }

        protected override void BuildContent(Canvas c)
        {
            var circle = new Ellipse
            {
                Width = ContentSize * 0.70,
                Height = ContentSize * 0.70
            };
            circle.SetResourceReference(Shape.StrokeProperty, "PrimaryText");
            Canvas.SetLeft(circle, ContentSize * 0.15);
            Canvas.SetTop(circle, ContentSize * 0.15);
            c.Children.Add(circle);

            Line MakeLine(double x1, double y1, double x2, double y2)
            {
                var l = new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2 };
                l.SetResourceReference(Shape.StrokeProperty, "PrimaryText");
                return l;
            }

            c.Children.Add(MakeLine(ContentSize * 0.00, ContentSize * 0.40, ContentSize * 0.15, ContentSize * 0.60));
            c.Children.Add(MakeLine(ContentSize * 0.30, ContentSize * 0.40, ContentSize * 0.15, ContentSize * 0.60));
            c.Children.Add(MakeLine(ContentSize * 1.00, ContentSize * 0.60, ContentSize * 0.85, ContentSize * 0.40));
            c.Children.Add(MakeLine(ContentSize * 0.70, ContentSize * 0.60, ContentSize * 0.85, ContentSize * 0.40));
        }
    }

    public sealed class RngButton : ButtonSwitch
    {
        public RngButton() : base(RM.ChooseRng) { }

        protected override void BuildContent(Canvas c)
        {
            var path1 = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 0,25 C 16,25 16,6 32,6")
            };
            path1.SetResourceReference(Shape.StrokeProperty, "PrimaryText");
            c.Children.Add(path1);

            var path2 = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 0,6 C 16,6 16,25 32,25")
            };
            path2.SetResourceReference(Shape.StrokeProperty, "PrimaryText");
            c.Children.Add(path2);
        }
    }

    public sealed class SaveButton : Button
    {
        public SaveButton()
        {
            Content = new System.Windows.Controls.Image
            {
                Width = IconButtonBase.ContentSize,
                Height = IconButtonBase.ContentSize,
                Source = TryLoadImage("pack://application:,,,/Resources/save.png")
            };

            Click += (_, __) =>
            {
                var d = new Microsoft.Win32.SaveFileDialog
                {
                    DefaultExt = ".RMData",
                    Filter = "RM Save data|*.RMData|All files|*",
                    InitialDirectory = Directory.GetCurrentDirectory()
                };

                if (d.ShowDialog() == true)
                    RM.SaveRMData(d.OpenFile());
            };
        }

        private static ImageSource? TryLoadImage(string u)
        {
            try { return new System.Windows.Media.Imaging.BitmapImage(new Uri(u)); }
            catch { return null; }
        }
    }

    public sealed class LoadButton : Button
    {
        public LoadButton()
        {
            Content = new System.Windows.Controls.Image
            {
                Width = IconButtonBase.ContentSize,
                Height = IconButtonBase.ContentSize,
                Source = TryLoadImage("pack://application:,,,/Resources/load.png")
            };

            Click += (_, __) =>
            {
                var d = new Microsoft.Win32.OpenFileDialog
                {
                    DefaultExt = ".RMData",
                    Filter = "RM Save data|*.RMData|All files|*",
                    InitialDirectory = Directory.GetCurrentDirectory()
                };

                if (d.ShowDialog() == true)
                    RM.LoadRMData(d.FileName);
            };
        }

        private static ImageSource? TryLoadImage(string u)
        {
            try { return new System.Windows.Media.Imaging.BitmapImage(new Uri(u)); }
            catch { return null; }
        }
    }

    public sealed class SettingsButton : Button
    {
        public SettingsButton()
        {
            Content = new System.Windows.Controls.Image
            {
                Width = IconButtonBase.ContentSize,
                Height = IconButtonBase.ContentSize,
                Source = TryLoadImage("pack://application:,,,/Resources/settings.png")
            };

            Click += (_, __) =>
                new SettingsWindow { Owner = Application.Current.MainWindow }.ShowDialog();
        }

        private static ImageSource? TryLoadImage(string u)
        {
            try { return new System.Windows.Media.Imaging.BitmapImage(new Uri(u)); }
            catch { return null; }
        }
    }

    public sealed class DownloadButton : IconButtonBase
    {
        public DownloadButton()
        {
            Click += (_, __) =>
            {
                if (!File.Exists(RM.YtDlpPath))
                {
                    MessageBox.Show(Lang.Get("downloadWindow.YtDlpMissing"), Lang.Get("downloadWindow.Error"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!RM.IsYtDlpValid())
                {
                    MessageBox.Show(Lang.Get("downloadWindow.YtDlpInvalidExt"), Lang.Get("downloadWindow.Error"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    // Продолжаем открытие окна после предупреждения
                }

                new DownloadWindow { Owner = Application.Current.MainWindow }.ShowDialog();
            };
        }

        protected override void BuildContent(Canvas canvas)
        {
            var arrow = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 16,8 L 16,22 M 10,16 L 16,22 L 22,16 M 10,26 L 22,26"),
                StrokeThickness = 2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            arrow.SetResourceReference(Shape.StrokeProperty, "PrimaryText");
            canvas.Children.Add(arrow);
        }
    }

    public sealed class RefreshButton : IconButtonBase
    {
        public RefreshButton()
        {
            Click += (_, __) =>
            {
                if (MessageBox.Show(
                        Lang.Get("main.Reset"),
                        Lang.Get("main.ResetTitle"),
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    RM.RefreshFiles();
                }
            };
        }

        protected override void BuildContent(Canvas c)
        {
            var circle = new Ellipse
            {
                Width = ContentSize * 0.70,
                Height = ContentSize * 0.70
            };
            circle.SetResourceReference(Shape.StrokeProperty, "PrimaryText");
            Canvas.SetLeft(circle, ContentSize * 0.15);
            Canvas.SetTop(circle, ContentSize * 0.15);
            c.Children.Add(circle);

            Line MakeLine(double x1, double y1, double x2, double y2)
            {
                var l = new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2 };
                l.SetResourceReference(Shape.StrokeProperty, "PrimaryText");
                return l;
            }

            c.Children.Add(MakeLine(ContentSize * 0.00, ContentSize * 0.40, ContentSize * 0.15, ContentSize * 0.60));
            c.Children.Add(MakeLine(ContentSize * 0.30, ContentSize * 0.40, ContentSize * 0.15, ContentSize * 0.60));
            c.Children.Add(MakeLine(ContentSize * 1.00, ContentSize * 0.60, ContentSize * 0.85, ContentSize * 0.40));
            c.Children.Add(MakeLine(ContentSize * 0.70, ContentSize * 0.60, ContentSize * 0.85, ContentSize * 0.40));
        }
    }

    public sealed class StartVolumeSlider : DockPanel
    {
        public StartVolumeSlider()
        {
            var label = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryText");
            DockPanel.SetDock(label, Dock.Top);
            Children.Add(label);

            var slider = new Slider
            {
                Minimum = 0,
                Maximum = 100,
                TickFrequency = 1,
                IsSnapToTickEnabled = true,
                Width = 200,
                VerticalAlignment = VerticalAlignment.Center
            };
            Children.Add(slider);

            slider.ValueChanged += (_, e) =>
            {
                RM.StartVolume = (int)Math.Round(e.NewValue);
                label.Text = RM.StartVolume + "%";
            };

            slider.Value = RM.StartVolume;
            label.Text = RM.StartVolume + "%";

            RM.StartVolumeUpdated += () => slider.Value = RM.StartVolume;
        }
    }
}