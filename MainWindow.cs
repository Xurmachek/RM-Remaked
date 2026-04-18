using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RMPlayer
{
    // ─────────────────────────────────────────────
    //  App — точка входа WPF-приложения.
    //  Создаёт главное окно и запускает плеер.
    // ─────────────────────────────────────────────
    public class App : Application
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Lang.Load("ru");
            var app = new App();
            app.Run(new MainWindow(args));
        }
    }

    // ─────────────────────────────────────────────
    //  MainWindow — главное окно приложения.
    //
    //  Структура:
    //    DockPanel
    //      └── StackPanel (настройки, Dock.Bottom)
    //            ├── CycleButton
    //            ├── RngButton
    //            ├── SaveButton
    //            ├── LoadButton
    //            └── StartVolumeSlider
    //      └── FileDisplayContainer (прокручиваемое дерево)
    //
    //  Поддерживает перетаскивание (Drag & Drop) файлов/папок.
    // ─────────────────────────────────────────────
    public sealed class MainWindow : Window
    {
        public MainWindow(string[] args)
        {
            Title  = "RM Player";
            Width  = 1100;
            Height = 600;

            try
            {
                Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Resources/Icon.ico"));
            }
            catch { /* Игнорировать ошибки загрузки иконки */ }

            // ── Корневой контейнер ──────────────────
            var dp = new DockPanel();
            Content = dp;

            // Установка начальной темы
            SettingsWindow.UpdateTheme();
            dp.SetResourceReference(Panel.BackgroundProperty, "WindowBackground");

            // ── Панель настроек (снизу) ─────────────
            var settingsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            settingsPanel.SetResourceReference(Panel.BackgroundProperty, "PanelBackground");
            DockPanel.SetDock(settingsPanel, Dock.Bottom);
            dp.Children.Add(settingsPanel);

            settingsPanel.Children.Add(new CycleButton());
            settingsPanel.Children.Add(new RngButton());
            settingsPanel.Children.Add(new SaveButton());
            settingsPanel.Children.Add(new LoadButton());
            settingsPanel.Children.Add(new SettingsButton());
            settingsPanel.Children.Add(new DownloadButton());
            settingsPanel.Children.Add(new RefreshButton());
            settingsPanel.Children.Add(new StartVolumeSlider());

            // ── Дерево файлов (основная область) ────
            var filesDisplay = new FileDisplayContainer();
            dp.Children.Add(filesDisplay);

            // ── Drag & Drop ─────────────────────────
            AllowDrop = true;

            DragOver += (_, e) =>
            {
                e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                    ? DragDropEffects.Link
                    : DragDropEffects.None;
                e.Handled = true;
            };

            Drop += (_, e) =>
            {
                lock (RM.FilesLock)
                {
                    try
                    {
                        // С Shift — очистить текущий список
                        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                        {
                            foreach (var node in RM.Files.Enumerate().ToArray())
                                node.Remove();
                            RM.AddedPaths.Clear();
                        }

                        if (e.Data.GetData(DataFormats.FileDrop) is string[] names)
                            foreach (var name in names)
                                RM.AddName(name);

                        e.Handled = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                    }
                }
            };

            // ── Аргументы командной строки ──────────
            foreach (var arg in args)
                RM.AddName(arg);

            // ── Обновлять заголовок при смене трека ─
            RM.FileSwitch += fname =>
                Dispatcher.Invoke(() => Title = fname);

            // ── Запустить воспроизведение ───────────
            RM.StartPlaying();

            // ── Завершить процесс при закрытии окна ─
            Closed += (_, _) => System.Environment.Exit(0);
        }
    }
}