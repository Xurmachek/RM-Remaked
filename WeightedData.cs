using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RMPlayer
{
    // ─────────────────────────────────────────────
    //  WeightedPath
    //  Представляет папку с накопленным весом.
    //  Вес папки = минимальный вес среди её файлов.
    // ─────────────────────────────────────────────
    public sealed class WeightedPath
    {
        // Глобальный реестр всех отслеживаемых папок
        public static readonly Dictionary<string, WeightedPath> All =
            new Dictionary<string, WeightedPath>();

        private int? _weight = null;

        public readonly string Path;

        // Файлы, которые ссылаются на эту папку
        public readonly HashSet<WeightedFile> Ref = new HashSet<WeightedFile>();

        // Файлы, явно помеченные как удалённые (имена файлов)
        public readonly HashSet<string> Removed = new HashSet<string>();

        // Показывать ли содержимое папки в UI
        public bool DisplayContent = false;

        public WeightedPath(string path) => Path = path;

        // Получить или создать папку по пути
        public static WeightedPath FromPath(string path)
        {
            if (All.TryGetValue(path, out var existing))
                return existing;
            var wp = new WeightedPath(path);
            All[path] = wp;
            return wp;
        }

        // Вес папки — минимальный вес среди всех её файлов
        public int GetWeight()
        {
            if (_weight == null)
                _weight = Ref.Min(f => f.Weight!.Value);
            return _weight.Value;
        }

        // Добавить файл-ссылку
        public void AddRef(WeightedFile f)
        {
            _weight = null;
            Ref.Add(f);
            Removed.Remove(f.FileName);
        }

        // Удалить файл-ссылку; если папка опустела — убрать из реестра
        public void RemoveRef(WeightedFile f)
        {
            if (!Ref.Remove(f))
                throw new InvalidOperationException("Файл не найден в Ref папки.");
            _weight = null;
            if (Ref.Count == 0)
            {
                All.Remove(Path);
                return;
            }
            Removed.Add(f.FileName);
        }

        // Сбросить кэш веса (вызывается при изменении веса файла)
        public void InvalidateWeight() => _weight = null;

        // Попробовать добавить имя файла в список «удалённых»
        // Возвращает false, если файл всё ещё активен
        public bool TryAddRemoved(string fileName)
        {
            if (Ref.Any(f => f.FileName == fileName))
                return false;
            return Removed.Add(fileName);
        }
    }

    // ─────────────────────────────────────────────
    //  WeightedFile
    //  Представляет один медиафайл с весом.
    //  Вес определяет вероятность выбора трека.
    // ─────────────────────────────────────────────
    public sealed class WeightedFile
    {
        public int? Weight = 1;

        public readonly string BasePath;   // корневая папка, с которой начинается дерево
        public readonly string FileName;   // полный путь к файлу

        // Все промежуточные папки от BasePath до файла
        public readonly WeightedPath[] Refs;

        public WeightedFile(string basePath, string fileName)
        {
            BasePath = basePath;
            FileName = fileName;

            // Разбиваем путь от корня до файла на составляющие папки
            var relative = fileName
                .Substring(basePath.Length)
                .TrimStart('\\');
            var parts = relative.Split('\\');
            // Убираем последний элемент (имя файла)
            var dirs = parts.Take(parts.Length - 1).ToArray();

            var sb = new StringBuilder(fileName.Length);
            sb.Append(basePath);
            if (sb.Length != 0) sb.Append('\\');

            Refs = new WeightedPath[dirs.Length];
            for (int i = 0; i < dirs.Length; i++)
            {
                sb.Append(dirs[i]);
                sb.Append('\\');
                var wp = WeightedPath.FromPath(sb.ToString());
                wp.AddRef(this);
                Refs[i] = wp;
            }
        }

        // Удалить файл из всех папок и сбросить вес
        public void Remove()
        {
            foreach (var p in Refs)
                p.RemoveRef(this);
            Weight = null;
        }

        // Изменить вес файла (и сбросить кэши в папках)
        public void UpdateWeight(int delta)
        {
            Weight = Math.Clamp(Weight!.Value + delta, 1, 999);
            foreach (var p in Refs)
                p.InvalidateWeight();
        }
    }
}