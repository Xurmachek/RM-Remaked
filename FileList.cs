using System;
using System.Collections.Generic;
using System.Linq;

namespace RMPlayer
{
    // ─────────────────────────────────────────────
    //  FileListNode
    //  Узел двусвязного кольцевого списка файлов.
    //  Удалённые узлы определяются по Weight == null.
    //  Список остаётся отсортированным по имени файла.
    // ─────────────────────────────────────────────
    public sealed class FileListNode
    {
        private FileListNode _prev;
        private FileListNode _next;
        public WeightedFile? File;

        // Создать sentinel-узел (голова пустого кольцевого списка)
        public FileListNode()
        {
            _prev = this;
            _next = this;
            File = null;
        }

        // Создать узел с файлом
        public FileListNode(WeightedFile f)
        {
            _prev = this;
            _next = this;
            File = f;
        }

        // Вставить узел между prev и next
        private FileListNode(FileListNode prev, FileListNode next, WeightedFile f)
        {
            _prev = prev; prev._next = this;
            _next = next; next._prev = this;
            File = f;
        }

        // Узел считается «удалённым», если файл убран из списка
        public bool IsRemoved => File == null || File.Weight == null;

        // ──────────────────────────────────────────
        //  Enumerate — обход живых узлов кольца.
        //  Пропускает «удалённые» узлы в начале,
        //  затем обходит кольцо ровно один раз.
        // ──────────────────────────────────────────
        public IEnumerable<FileListNode> Enumerate()
        {
            var curr = this;

            // Найти первый живой узел (защита от зацикливания)
            var visited = new HashSet<FileListNode> { this };
            while (curr.IsRemoved)
            {
                curr = curr._next;
                if (!visited.Add(curr)) yield break;
            }

            var first = curr;
            do
            {
                if (!curr.IsRemoved)
                    yield return curr;
                curr = curr._next;
            } while (curr != first);
        }

        // ──────────────────────────────────────────
        //  Insert — вставить файл в отсортированный
        //  список. Если файл с тем же именем уже
        //  есть — перенести его вес и заменить узел.
        // ──────────────────────────────────────────
        public FileListNode Insert(WeightedFile f)
        {
            var nodes = Enumerate().ToList();

            // Пустой список
            if (nodes.Count == 0)
            {
                var single = new FileListNode(f);
                this._next = single;
                return single;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                var prev = nodes[i];
                var next = (i + 1 < nodes.Count) ? nodes[i + 1] : nodes[0];

                // Файл уже существует — обновить вес и заменить узел
                if (prev.File!.FileName == f.FileName)
                {
                    f.UpdateWeight(prev.File.Weight!.Value - f.Weight!.Value);
                    prev.Remove();

                    FileListNode replacement;
                    if (next == prev)
                    {
                        // Был единственным узлом
                        replacement = new FileListNode(f);
                    }
                    else
                    {
                        replacement = new FileListNode(next._prev, next, f);
                    }
                    prev._next = replacement;
                    return replacement;
                }

                // Найти правильное место в отсортированном порядке
                if (prev.File == null || next.File == null) continue;

                bool goesHere = (string.Compare(prev.File.FileName, next.File.FileName,
                                                StringComparison.Ordinal) < 0)
                    ? (string.Compare(prev.File.FileName, f.FileName, StringComparison.Ordinal) < 0 &&
                       string.Compare(f.FileName, next.File.FileName, StringComparison.Ordinal) < 0)
                    : (string.Compare(prev.File.FileName, f.FileName, StringComparison.Ordinal) < 0 ||
                       string.Compare(f.FileName, next.File.FileName, StringComparison.Ordinal) < 0);

                if (goesHere)
                    return new FileListNode(prev, next, f);
            }

            // Список «удалён» целиком — создаём новое кольцо
            if (IsRemoved)
            {
                var single = new FileListNode(f);
                this._next = single;
                return single;
            }

            throw new InvalidOperationException("Не удалось вставить файл в список.");
        }

        // Физически удалить узел из кольца и вызвать Remove у файла
        public void Remove()
        {
            _prev._next = _next;
            _next._prev = _prev;
            File?.Remove();
            File = null;
        }
    }
}