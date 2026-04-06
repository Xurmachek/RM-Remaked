using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RMPlayer
{
    // ─────────────────────────────────────────────
    //  DisplayHeader — заголовок строки (файл/папка).
    //  Содержит:
    //    • символ состояния (•, ►, ▼)
    //    • числовой вес (кликабельный)
    //    • прогресс-бар относительного веса
    //    • кнопка удаления (×)
    //    • кнопка сброса веса (■)
    //    • название
    // ─────────────────────────────────────────────
    public sealed class DisplayHeader : StackPanel
    {
        private readonly string _charSet;
        private readonly TextBlock _charBox  = new TextBlock { Width = 16, Background = Brushes.Transparent };
        private readonly TextBlock _weightBox = new TextBlock { Margin = new Thickness(2,0,2,0), Background = Brushes.Transparent };
        private readonly ProgressBar _relWeightBar  = new ProgressBar { Minimum = 0, Maximum = 1 };
        private readonly TextBlock  _relWeightText  = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center };

        public event Action?      ArrowClicked;
        public event Action<int>? WeightChanged;
        public event Action?      DeleteRequested;
        public event Action?      ResetRequested;

        public DisplayHeader(string charSet, string name)
        {
            _charSet  = charSet;
            Orientation = Orientation.Horizontal;

            // Символ состояния
            _charBox.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryText");
            _charBox.MouseUp += (_, e) => { ArrowClicked?.Invoke(); e.Handled = true; };
            Children.Add(_charBox);

            // Вес (не кликабельный)
            _weightBox.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryText");
            _weightBox.VerticalAlignment = VerticalAlignment.Center;
            _weightBox.HorizontalAlignment = HorizontalAlignment.Center;
            _weightBox.FontFamily = new FontFamily("Consolas");
            
            var weightBorder = new Border
            {
                Width           = 32,
                Height          = 18,
                BorderThickness = new Thickness(1),
                Margin          = new Thickness(0,0,5,0),
                Child           = _weightBox
            };
            weightBorder.SetResourceReference(Border.BorderBrushProperty, "PrimaryText");
            Children.Add(weightBorder);

            // Прогресс-бар относительного веса (ТЕПЕРЬ КЛИКАБЕЛЬНЫЙ)
            var relGrid = new Grid 
            { 
                Width = 60, 
                Height = 16, 
                Margin = new Thickness(0,0,5,0),
                Background = Brushes.Transparent // Область бокса теперь прозрачная, но кликабельная
            };
            _relWeightBar.Height = 16;
            _relWeightBar.IsHitTestVisible = false; // Чтобы не перехватывал клики у Grid
            relGrid.Children.Add(_relWeightBar);
            
            _relWeightText.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryText");
            _relWeightText.FontSize = 10;
            _relWeightText.FontFamily = new FontFamily("Consolas");
            _relWeightText.HorizontalAlignment = HorizontalAlignment.Center;
            _relWeightText.VerticalAlignment = VerticalAlignment.Center;
            _relWeightText.IsHitTestVisible = false; // Чтобы текст не мешал клику
            relGrid.Children.Add(_relWeightText);
            Children.Add(relGrid);

            // Логика клика по боксу шанса
            relGrid.MouseDown += (_, e) =>
            {
                int delta = 0;
                if (e.ChangedButton == MouseButton.Left) delta = 1;
                else if (e.ChangedButton == MouseButton.Right) delta = -1;
                
                if (delta == 0) return;
                
                if (Keyboard.IsKeyDown(Key.LeftShift)) delta *= 10;
                if (Keyboard.IsKeyDown(Key.LeftCtrl))  delta *= 100;
                
                WeightChanged?.Invoke(delta);
                e.Handled = true;
            };

            // Кнопка удаления
            var deleteBtn = new Button { Margin = new Thickness(0,0,5,0) };
            deleteBtn.Click += (_, _) => DeleteRequested?.Invoke();
            var deleteContent = new Grid { Width = 16, Height = 16 };

            void AddLine(double x1, double y1, double x2, double y2)
            {
                var line = new Line
                {
                    X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                    StrokeThickness = 2,
                    StrokeEndLineCap = PenLineCap.Flat
                };
                line.Stroke = Brushes.Red; // Удаление всегда красное
                deleteContent.Children.Add(line);
            }
            AddLine(3, 3, 13, 13);
            AddLine(3, 13, 13, 3);
            deleteBtn.Content = deleteContent;
            Children.Add(deleteBtn);

            // Кнопка сброса
            var resetBtn = new Button
            {
                Width  = 16, Height = 16,
                Margin = new Thickness(0,0,5,0),
                Content = new Rectangle { Width = 7, Height = 7, Fill = Brushes.Blue }
            };
            resetBtn.Click += (_, _) => ResetRequested?.Invoke();
            Children.Add(resetBtn);

            // Название
            var nameBlock = new TextBlock { Text = name };
            nameBlock.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryText");
            Children.Add(nameBlock);
        }

        public void UpdateChar(int index) =>
            _charBox.Text = _charSet[index].ToString();

        public void UpdateWeight(int w, double relW)
        {
            _weightBox.Text     = w.ToString("D3");
            _relWeightBar.Value = relW;
            _relWeightText.Text = relW.ToString("f3");
        }
    }

    // ─────────────────────────────────────────────
    //  FileDisplay — отображение одного файла.
    // ─────────────────────────────────────────────
    public sealed class FileDisplay : ContentControl
    {
        private readonly WeightedFile   _file;
        private readonly DisplayHeader  _header;

        public FileDisplay(string parentPath, WeightedFile f)
        {
            _file   = f;
            _header = new DisplayHeader("•", f.FileName.Substring(parentPath.Length));
            Content = _header;
            _header.UpdateChar(0);

            _header.WeightChanged += delta =>
            {
                f.UpdateWeight(delta);
                RM.InvokeWeightsUpdated();
            };
            _header.DeleteRequested += () =>
            {
                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    if (Lang.ShowYesNo($"{Lang.Get("button.DeleteDisk")}\n{f.FileName}", Lang.Get("button.Delete")))
                    {
                        try {
                            f.Remove();
                            System.IO.File.Delete(f.FileName);
                            RM.InvokeFilesUpdated();
                        } catch (Exception ex) {
                            MessageBox.Show(Lang.Get("button.Error") + ": " + ex.Message);
                        }
                    }
                }
                else
                {
                    f.Remove();
                    RM.InvokeFilesUpdated();
                }
            };
            _header.ResetRequested += () =>
            {
                f.UpdateWeight(-f.Weight!.Value + 1);
                RM.InvokeWeightsUpdated();
            };
        }

        public WeightedFile File => _file;

        public void UpdateWeight(int maxWeightSum) =>
            _header.UpdateWeight(_file.Weight!.Value,
                                 (double)_file.Weight.Value / maxWeightSum);
    }

    // ─────────────────────────────────────────────
    //  FolderDisplay — отображение папки с
    //  вложенными папками и файлами.
    // ─────────────────────────────────────────────
    public sealed class FolderDisplay : StackPanel
    {
        private readonly WeightedPath _path;
        private readonly DisplayHeader? _titleBar;
        public readonly StackPanel _dirsPanel  = new StackPanel();
        public readonly StackPanel _filesPanel = new StackPanel();

        public readonly Dictionary<string, FolderDisplay> SubDirs = new();
        public readonly Dictionary<string, FileDisplay>   Files   = new();

        public FolderDisplay(string? parentPath, WeightedPath path)
        {
            _path = path;

            Action? updateBodyShown = null;

            if (parentPath != null)
            {
                _titleBar = new DisplayHeader(
                    "►▼",
                    path.Path.Substring(parentPath.Length).TrimEnd('\\'));
                Children.Add(_titleBar);

                _titleBar.WeightChanged += delta =>
                {
                    foreach (var f in path.Ref) f.UpdateWeight(delta);
                    RM.InvokeWeightsUpdated();
                };
                _titleBar.DeleteRequested += () =>
                {
                    foreach (var f in path.Ref.ToArray()) f.Remove();
                    RM.InvokeFilesUpdated();
                };
                _titleBar.ResetRequested += () =>
                {
                    foreach (var f in path.Ref.ToArray())
                        f.UpdateWeight(-f.Weight!.Value + 1);
                    RM.InvokeWeightsUpdated();
                };

                _titleBar.ArrowClicked += () =>
                {
                    path.DisplayContent = !path.DisplayContent;
                    updateBodyShown?.Invoke();
                };
                updateBodyShown += () => _titleBar.UpdateChar(path.DisplayContent ? 1 : 0);
            }

            var body = new Border { Margin = new Thickness(5,0,0,0) };
            Children.Add(body);

            if (parentPath != null)
            {
                body.BorderBrush     = Brushes.Black;
                body.BorderThickness = new Thickness(1,0,0,0);
            }

            var bodySp = new StackPanel { Margin = new Thickness(8,0,0,0) };
            body.Child = bodySp;
            bodySp.Children.Add(_dirsPanel);
            bodySp.Children.Add(_filesPanel);

            updateBodyShown += () =>
                body.Visibility = path.DisplayContent
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            updateBodyShown?.Invoke();
        }

        // Добавить вложенную папку (рекурсивно)
        public FolderDisplay AddFolder(WeightedPath subPath)
        {
            foreach (var key in SubDirs.Keys)
            {
                var combined = System.IO.Path.Combine(_path.Path, key);
                if (subPath.Path.StartsWith(combined))
                    return SubDirs[key].AddFolder(subPath);
            }
            var display = new FolderDisplay(_path.Path, subPath);
            SubDirs[subPath.Path.Substring(_path.Path.Length)] = display;
            _dirsPanel.Children.Add(display);
            return display;
        }

        // Добавить файл в нужную подпапку
        public void AddFile(WeightedFile f)
        {
            foreach (var sub in SubDirs.Values)
                if (f.Refs.Contains(sub._path))
                {
                    sub.AddFile(f);
                    return;
                }
            var fd = new FileDisplay(_path.Path, f);
            Files[f.FileName.Substring(_path.Path.Length)] = fd;
            _filesPanel.Children.Add(fd);
        }

        // ── Вычисление суммы весов ─────────────────
        private object? _weightSumLock;
        private int?   _weightSumCache;

        private int GetWeightSum(object uLock)
        {
            if (_weightSumLock == uLock && _weightSumCache.HasValue)
                return _weightSumCache.Value;

            int sum = 0;
            foreach (var sub in SubDirs.Values) sum += sub.GetWeightSum(uLock);
            foreach (var fd  in Files.Values)   sum += fd.File.Weight!.Value;

            _weightSumLock  = uLock;
            _weightSumCache = sum;
            return sum;
        }

        // Обновить отображение весов рекурсивно
        public void UpdateWeights(object uLock, int maxWeightSum)
        {
            int selfSum = GetWeightSum(uLock);

            if (_titleBar != null)
                _titleBar.UpdateWeight(
                    _path.GetWeight(),
                    maxWeightSum > 0 ? (double)selfSum / maxWeightSum : 0);

            int childMax = SubDirs.Values.Select(s => s.GetWeightSum(uLock))
                .Concat(Files.Values.Select(f => f.File.Weight!.Value))
                .DefaultIfEmpty(0).Max();

            if (childMax == 0) return;

            foreach (var sub in SubDirs.Values) sub.UpdateWeights(uLock, childMax);
            foreach (var fd  in Files.Values)   fd.UpdateWeight(childMax);
        }
    }

    // ─────────────────────────────────────────────
    //  FileDisplayContainer — ScrollViewer с
    //  корневым деревом папок.
    // ─────────────────────────────────────────────
    public sealed class FileDisplayContainer : ScrollViewer
    {
        public FileDisplayContainer()
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

            var rootPath = new WeightedPath("");   // Используем фиктивный путь как корень
            // WeightedPath не имеет публичного конструктора — создаём через FromPath
            // (для корня используем пустую строку, которая не конфликтует с реальными путями)
            rootPath.DisplayContent = true;

            var rootDisplay = new FolderDisplay(null, rootPath);
            Content = rootDisplay;

            Action updateWeights = () => rootDisplay.UpdateWeights(new object(), 0);

            RM.FilesUpdated += () =>
            {
                WeightedFile[] files;
                lock (RM.FilesLock)
                    files = RM.Files.Enumerate().Select(n => n.File!).ToArray();

                var paths = files.SelectMany(f => f.Refs).ToHashSet();

                // Полная перестройка дерева
                rootDisplay.SubDirs.Clear();
                rootDisplay.Files.Clear();
                rootDisplay._dirsPanel?.Children.Clear();
                rootDisplay._filesPanel?.Children.Clear();

                var pathsDisplay = new Dictionary<WeightedPath, FolderDisplay>
                {
                    [rootPath] = rootDisplay
                };

                foreach (var p in paths.OrderBy(p => p.Path))
                    pathsDisplay[p] = rootDisplay.AddFolder(p);

                foreach (var f in files.OrderBy(f => f.FileName))
                {
                    var key = f.Refs.LastOrDefault() ?? rootPath;
                    if (pathsDisplay.TryGetValue(key, out var fd))
                        fd.AddFile(f);
                    else
                        rootDisplay.AddFile(f);
                }

                updateWeights();
            };

            RM.WeightsUpdated += updateWeights;
        }
    }
}