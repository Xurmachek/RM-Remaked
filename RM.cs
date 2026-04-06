using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;

namespace RMPlayer
{
    public sealed class BoolValue
    {
        public bool Val;
        public BoolValue(bool val) => Val = val;
    }

    public static class ExtAnalysis
    {
        public static string GetExecStr(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            using var key = Registry.ClassesRoot.OpenSubKey(ext);
            if (key == null) throw new Exception($"Расширение {ext} не зарегистрировано.");
            var progId = key.GetValue(null) as string;
            if (progId == null) throw new Exception($"ProgID для {ext} не найден.");
            using var cmd = Registry.ClassesRoot.OpenSubKey($@"{progId}\shell\open\command");
            if (cmd == null) throw new Exception($"Команда открытия для {progId} не найдена.");
            return (cmd.GetValue(null) as string) ?? throw new Exception("Команда пуста.");
        }

        public static bool FileExists(string path) => File.Exists(path);
        public static IEnumerable<string> EnumerateFiles(string path) => Directory.Exists(path) ? Directory.EnumerateFiles(path) : Enumerable.Empty<string>();
        public static IEnumerable<string> EnumerateAllFiles(string path) => Directory.Exists(path) ? Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories) : Enumerable.Empty<string>();
    }

    public static class RM
    {
        public static FileListNode Files = new FileListNode();
        public static readonly object FilesLock = new object();

        public static event Action? FilesUpdated;
        public static void InvokeFilesUpdated() => FilesUpdated?.Invoke();
        public static event Action? WeightsUpdated;
        public static void InvokeWeightsUpdated() => WeightsUpdated?.Invoke();

        public static readonly BoolValue Cycle      = new BoolValue(false);
        public static readonly BoolValue ChooseRng  = new BoolValue(true);
        public static readonly BoolValue IsDarkTheme = new BoolValue(false);
        public static readonly BoolValue IsTtsEnabled = new BoolValue(true);

        public static List<string> AddedPaths = new List<string>();

        public static void RefreshFiles()
        {
            var paths = AddedPaths.ToList();
            lock(FilesLock)
            {
                foreach(var node in Files.Enumerate().ToArray())
                    node.Remove();
            }
            AddedPaths.Clear();
            foreach(var p in paths)
                AddName(p);
        }

        public static int StartVolume = 100;
        public static event Action? StartVolumeUpdated;

        public static event Action<string>? FileSwitch;
        private static void InvokeFileSwitch(string name) => FileSwitch?.Invoke(name);

        public const int  RMData_Version       = 1;
        public const byte RMData_CT_File        = 1;
        public const byte RMData_CT_Volume      = 2;
        public const byte RMData_CT_DirRemoved  = 3;

        public static void SaveRMData(Stream stream)
        {
            using var bw = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false);
            bw.Write(-1);
            bw.Write(RMData_Version);

            foreach (var node in Files.Enumerate())
            {
                var f = node.File!;
                bw.Write(RMData_CT_File);
                bw.Write(f.BasePath);
                bw.Write(f.FileName.Substring(f.BasePath.Length));
                bw.Write(f.Weight!.Value);
            }

            foreach (var dir in WeightedPath.All.Values)
            {
                bw.Write(RMData_CT_DirRemoved);
                bw.Write(dir.Path);
                bw.Write(dir.Removed.Count);
                foreach (var rem in dir.Removed)
                    bw.Write(rem);
            }

            bw.Write(RMData_CT_Volume);
            bw.Write(StartVolume);
        }

        public static void LoadRMData(string rmDataFile)
        {
            var loadedFiles   = new Dictionary<string, (string basePath, int weight)>();
            var loadedRemoved = new Dictionary<string, HashSet<string>>();
            int? loadedStartVolume = null;
            var missingFiles = new List<string>();

            using (var br = new BinaryReader(File.OpenRead(rmDataFile), Encoding.UTF8))
            {
                Action ReadWeightedFile = () =>
                {
                    var basePath = br.ReadString();
                    var relPath  = br.ReadString();
                    var fname    = basePath + relPath;
                    var weight   = br.ReadInt32();
                    loadedFiles[fname] = (basePath, weight);
                    if (!File.Exists(fname)) missingFiles.Add(fname);
                };

                int firstInt = br.ReadInt32();

                if (firstInt != -1)
                {
                    br.BaseStream.Position = 0;
                    while (br.BaseStream.Position < br.BaseStream.Length)
                    {
                        try { ReadWeightedFile(); } catch { break; }
                    }
                }
                else
                {
                    int dataVersion = br.ReadInt32();
                    while (br.BaseStream.Position < br.BaseStream.Length)
                    {
                        try
                        {
                            byte ct = br.ReadByte();
                            switch (ct)
                            {
                                case RMData_CT_File: ReadWeightedFile(); break;
                                case RMData_CT_DirRemoved:
                                {
                                    var path = br.ReadString();
                                    int count = br.ReadInt32();
                                    var removed = new HashSet<string>(count);
                                    for (int i = 0; i < count; i++) { var f = br.ReadString(); if (File.Exists(f)) removed.Add(f); }
                                    loadedRemoved[path] = removed;
                                    break;
                                }
                                case RMData_CT_Volume: loadedStartVolume = br.ReadInt32(); break;
                            }
                        }
                        catch { break; }
                    }
                }
            }

            lock (FilesLock)
            {
                foreach (var fname in loadedFiles.Keys)
                {
                    var (bp, w) = loadedFiles[fname];
                    Files = Files.Insert(new WeightedFile(bp, fname) { Weight = w });
                }
            }
            if (loadedStartVolume.HasValue) { StartVolume = loadedStartVolume.Value; StartVolumeUpdated?.Invoke(); }
            FilesUpdated?.Invoke();
        }

        private static bool CheckFile(string fname)
        {
            var ext = Path.GetExtension(fname).ToLowerInvariant();
            string[] ignored = { ".dll", ".exe", ".lnk", ".db", ".ini", ".bat", ".cmd", ".msi", ".sys", ".tmp", ".config", ".xml", ".json", "" };
            if (ignored.Contains(ext)) return false;
            try { return ExtAnalysis.GetExecStr(fname).Contains("\\mpv.exe", StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        public static void AddName(string name)
        {
            name = name.Replace('/', '\\');
            if (File.Exists(name))
            {
                if (Path.GetExtension(name).Equals(".RMData", StringComparison.OrdinalIgnoreCase)) { LoadRMData(name); return; }
                if (!CheckFile(name)) { MessageBox.Show("Файл не поддерживается.", "Ошибка"); return; }
                lock (FilesLock) Files.Insert(new WeightedFile(Path.GetDirectoryName(name)!, name));
                FilesUpdated?.Invoke();
            }
            else if (Directory.Exists(name))
            {
                AddedPaths.Add(name);
                var baseDir = Path.GetDirectoryName(name.TrimEnd('\\'))!.TrimEnd('\\');
                var prev = Files;
                foreach (var fname in ExtAnalysis.EnumerateAllFiles(name))
                {
                    if (!CheckFile(fname)) continue;
                    lock (FilesLock) prev = prev.Insert(new WeightedFile(baseDir, fname));
                }
                FilesUpdated?.Invoke();
            }
        }

        public static void StartPlaying()
        {
            var thread = new Thread(() =>
            {
                try
                {
                    Action<string> speak = text =>
                    {
                        try
                        {
                            bool hasRussian = Regex.IsMatch(text, @"\p{IsCyrillic}");
                            var langToFind = hasRussian ? "ru" : "en";
                            var synthesizer = new Windows.Media.SpeechSynthesis.SpeechSynthesizer();
                            var voice = Windows.Media.SpeechSynthesis.SpeechSynthesizer.AllVoices
                                .OrderBy(v => v.DisplayName.Contains("Natural") ? 0 : 1)
                                .FirstOrDefault(v => v.Language.StartsWith(langToFind)) 
                                ?? Windows.Media.SpeechSynthesis.SpeechSynthesizer.DefaultVoice;
                            synthesizer.Voice = voice;

                            var streamTask = synthesizer.SynthesizeTextToStreamAsync(text).AsTask();
                            streamTask.Wait();
                            var stream = streamTask.Result;

                            var tempFile = Path.Combine(Path.GetTempPath(), "rm_tts.wav");
                            using (var fs = File.Create(tempFile))
                            {
                                using (var inputStream = stream.AsStreamForRead()) inputStream.CopyTo(fs);
                            }
                            using (var player = new System.Media.SoundPlayer(tempFile))
                            {
                                if (RM.IsTtsEnabled.Val) player.PlaySync();
                            }
                        }
                        catch (Exception e) { MessageBox.Show("TTS Error: " + e.Message); }
                    };

                    while (true)
                    {
                        FileListNode[] nodes;
                        lock (FilesLock) nodes = Files.Enumerate().ToArray();
                        if (nodes.Length == 0) { InvokeFileSwitch("RM Player"); Thread.Sleep(100); continue; }

                        FileListNode curr;
                        if (Cycle.Val) curr = nodes[0];
                        else if (!ChooseRng.Val) curr = nodes[0] == Files ? (nodes.Length > 1 ? nodes[1] : nodes[0]) : nodes[0];
                        else
                        {
                            int totalWeight = nodes.Sum(n => n.File!.Weight!.Value);
                            int rnd = new Random().Next(totalWeight);
                            curr = nodes[0];
                            foreach (var n in nodes)
                            {
                                if (n.File!.Weight!.Value > rnd) { curr = n; break; }
                                rnd -= n.File.Weight.Value;
                            }
                        }

                        Files = curr;
                        if (!File.Exists(curr.File!.FileName)) { curr.Remove(); continue; }

                        var fullName = curr.File.FileName;
                        var relName  = fullName.Substring(curr.File.BasePath.Length + 1);
                        InvokeFileSwitch(relName);

                        var toSpeak = Path.ChangeExtension(relName, null).Replace('\\', ' ').Replace('-', ' ').Trim();
                        var m = Regex.Match(toSpeak, @"^\[\d+\]");
                        if (m.Success) toSpeak = toSpeak.Substring(m.Length).Trim();
                        
                        speak(toSpeak);
                        PlayFile(fullName);
                        InvokeFileSwitch("RM Player");
                    }
                }
                catch (Exception e) { MessageBox.Show(e.ToString()); }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        public static void PlayFile(string fname)
        {
            try
            {
                var command = ExtAnalysis.GetExecStr(fname).Replace("%1", fname);
                int closingQuote = command.IndexOf('"', 1);
                var executable = command.Substring(1, closingQuote - 1);
                var args = command.Substring(closingQuote + 1).Trim() + $" --window-minimized=yes --volume={StartVolume}";

                using var proc = Process.Start(executable, args);
                proc?.WaitForExit();
            }
            catch (Exception e) { MessageBox.Show("Play Error: " + e.Message); }
        }
    }
}