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
using System.Runtime.InteropServices; // Добавлено для DllImport

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

        public static readonly BoolValue Cycle = new BoolValue(false);
        public static readonly BoolValue ChooseRng = new BoolValue(true);
        public static readonly BoolValue IsDarkTheme = new BoolValue(false);
        public static readonly BoolValue IsTtsEnabled = new BoolValue(true);

        public static List<string> AddedPaths = new List<string>();

        public static void RefreshFiles()
        {
            var paths = AddedPaths.ToList();
            lock (FilesLock)
            {
                foreach (var node in Files.Enumerate().ToArray())
                    node.Remove();
            }
            AddedPaths.Clear();
            foreach (var p in paths)
                AddName(p);
        }

        public static int StartVolume = 100;
        public static event Action? StartVolumeUpdated;

        public static event Action<string>? FileSwitch;
        private static void InvokeFileSwitch(string name) => FileSwitch?.Invoke(name);

        public const int RMData_Version = 1;
        public const byte RMData_CT_File = 1;
        public const byte RMData_CT_Volume = 2;
        public const byte RMData_CT_DirRemoved = 3;

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
            var loadedFiles = new Dictionary<string, (string basePath, int weight)>();
            var loadedRemoved = new Dictionary<string, HashSet<string>>();
            int? loadedStartVolume = null;
            var missingFiles = new List<string>();

            using (var br = new BinaryReader(File.OpenRead(rmDataFile), Encoding.UTF8))
            {
                Action ReadWeightedFile = () =>
                {
                    var basePath = br.ReadString();
                    var relPath = br.ReadString();
                    var fname = basePath + relPath;
                    var weight = br.ReadInt32();
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

        // --- ДОБАВЛЕНО: Метод воспроизведения через WinAPI (исправляет ошибку CS0103) ---
        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        private static extern int mciSendString(string command, StringBuilder? returnValue, int returnLength, IntPtr winHandle);

        public static void PlaySoundWinApi(string filename)
        {
            try
            {
                mciSendString($"open \"{filename}\" type waveaudio alias tts", null, 0, IntPtr.Zero);
                mciSendString("play tts wait", null, 0, IntPtr.Zero);
                mciSendString("close tts", null, 0, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WinAPI playback failed: {ex.Message}");
            }
        }
        // ------------------------------------------------------------------------------

        public static void StartPlaying()
        {
            var thread = new Thread(() =>
            {
                try
                {
                    // --- ИСПРАВЛЕННЫЙ БЛОК TTS ---
					Action<string> speak = text =>
					{
						string? tempFile = null;
						try
						{
							if (string.IsNullOrWhiteSpace(text))
								return;

							if (!RM.IsTtsEnabled.Val)
								return;

							System.Diagnostics.Debug.WriteLine($"TTS: Starting synthesis for '{text}'");

							bool hasRussian = Regex.IsMatch(text, @"\p{IsCyrillic}");
							var langToFind = hasRussian ? "ru" : "en";

							Windows.Media.SpeechSynthesis.SpeechSynthesizer? synthesizer = null;
							IRandomAccessStream? stream = null;

							try
							{
								synthesizer = new Windows.Media.SpeechSynthesis.SpeechSynthesizer();
								System.Diagnostics.Debug.WriteLine("TTS: Synthesizer created");

								var allVoices = Windows.Media.SpeechSynthesis.SpeechSynthesizer.AllVoices.ToList();
								System.Diagnostics.Debug.WriteLine($"TTS: Found {allVoices.Count} voices");

								if (allVoices.Count == 0)
								{
									MessageBox.Show("В системе не установлены голоса для синтеза речи.\n\nУстановите языковые пакеты Windows или отключите TTS в настройках.", "TTS недоступен");
									return;
								}

								var voice = allVoices
									.OrderBy(v => v.DisplayName.Contains("Natural") ? 0 : 1)
									.FirstOrDefault(v => v.Language.StartsWith(langToFind));

								if (voice == null)
								{
									voice = allVoices.FirstOrDefault();
									System.Diagnostics.Debug.WriteLine($"TTS: Using fallback voice: {voice?.DisplayName}");
								}
								else
								{
									System.Diagnostics.Debug.WriteLine($"TTS: Using voice: {voice.DisplayName} ({voice.Language})");
								}

								if (voice == null)
								{
									System.Diagnostics.Debug.WriteLine("TTS: No voices available");
									return;
								}

								synthesizer.Voice = voice;

								System.Diagnostics.Debug.WriteLine("TTS: Starting synthesis...");
								var synthesisTask = synthesizer.SynthesizeTextToStreamAsync(text);
								stream = synthesisTask.GetAwaiter().GetResult();
								System.Diagnostics.Debug.WriteLine($"TTS: Synthesis complete, stream size: {stream.Size}");

								if (stream.Size == 0)
								{
									MessageBox.Show("Синтез речи вернул пустой поток", "TTS Error");
									return;
								}

								var tempPath = AppContext.BaseDirectory;
								if (!Directory.Exists(tempPath))
								{
									MessageBox.Show($"Папка приложения не существует: {tempPath}", "TTS Error");
									return;
								}

								tempFile = Path.Combine(tempPath, $"rm_tts_{DateTime.Now.Ticks}.wav");
								System.Diagnostics.Debug.WriteLine($"TTS: Creating file at: {tempFile}");

								using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
								{
									using var inputStream = stream.AsStreamForRead();
									inputStream.CopyTo(fs);
									fs.Flush();
								}

								if (!File.Exists(tempFile))
								{
									MessageBox.Show($"TTS файл не был создан: {tempFile}", "TTS Error");
									return;
								}

								var fileInfo = new FileInfo(tempFile);
								System.Diagnostics.Debug.WriteLine($"TTS: File created, size: {fileInfo.Length} bytes");

								if (fileInfo.Length == 0)
								{
									MessageBox.Show("TTS файл пустой", "TTS Error");
									return;
								}

								System.Diagnostics.Debug.WriteLine($"TTS: Playing file...");

								// Способ 1: SoundPlayer
								bool played = false;
								try
								{
									using var player = new System.Media.SoundPlayer(tempFile);
									player.LoadAsync();

									int timeout = 0;
									while (!player.IsLoadCompleted && timeout < 50)
									{
										Thread.Sleep(100);
										timeout++;
									}

									if (player.IsLoadCompleted)
									{
										player.PlaySync();
										played = true;
										System.Diagnostics.Debug.WriteLine("TTS: Played via SoundPlayer");
									}
									else
									{
										System.Diagnostics.Debug.WriteLine("TTS: SoundPlayer timeout");
									}
								}
								catch (Exception ex)
								{
									System.Diagnostics.Debug.WriteLine($"TTS: SoundPlayer failed: {ex.Message}");
								}

								// Способ 2: MediaPlayer
								if (!played)
								{
									try
									{
										var mediaPlayer = new MediaPlayer();
										mediaPlayer.Open(new Uri(tempFile, UriKind.Absolute));
										mediaPlayer.Play();

										Thread.Sleep(3000);

										mediaPlayer.Stop();
										mediaPlayer.Close();
										played = true;
										System.Diagnostics.Debug.WriteLine("TTS: Played via MediaPlayer");
									}
									catch (Exception ex2)
									{
										System.Diagnostics.Debug.WriteLine($"TTS: MediaPlayer failed: {ex2.Message}");
									}
								}

								// Способ 3: WinAPI
								if (!played)
								{
									try
									{
										RM.PlaySoundWinApi(tempFile);
										System.Diagnostics.Debug.WriteLine("TTS: Played via WinAPI");
									}
									catch (Exception ex3)
									{
										System.Diagnostics.Debug.WriteLine($"TTS: WinAPI failed: {ex3.Message}");
									}
								}
							}
							finally
							{
								stream?.Dispose();
								synthesizer?.Dispose();
							}
						}
						catch (Exception e)
						{
							var details = $"TTS Error: {e.Message}\n\n{e.GetType().FullName}";
							if (e.InnerException != null)
								details += $"\n\nInner: {e.InnerException.Message}\n({e.InnerException.GetType().FullName})";
							details += $"\n\nFile: {tempFile ?? "null"}";
							details += $"\n\nStack: {e.StackTrace}";

							System.Diagnostics.Debug.WriteLine(details);
							MessageBox.Show(details);
						}
						finally
						{
							if (tempFile != null)
							{
								try
								{
									Thread.Sleep(500);
									if (File.Exists(tempFile))
									{
										File.Delete(tempFile);
										System.Diagnostics.Debug.WriteLine($"TTS: Temp file deleted: {tempFile}");
									}
								}
								catch (Exception ex)
								{
									System.Diagnostics.Debug.WriteLine($"TTS: Failed to delete temp file: {ex.Message}");
								}
							}
						}
					};
                    // ---------------------------

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
                        var relName = fullName.Substring(curr.File.BasePath.Length + 1);
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
                System.Diagnostics.Debug.WriteLine($"Playing file: {fname}");

                var command = ExtAnalysis.GetExecStr(fname).Replace("%1", fname);
                System.Diagnostics.Debug.WriteLine($"Command: {command}");

                int closingQuote = command.IndexOf('"', 1);
                if (closingQuote < 0)
                    throw new Exception("Не удалось найти исполняемый файл в команде");

                var executable = command.Substring(1, closingQuote - 1);
                var args = command.Substring(closingQuote + 1).Trim() + $" --window-minimized=yes --volume={StartVolume}";

                System.Diagnostics.Debug.WriteLine($"Executable: {executable}");
                System.Diagnostics.Debug.WriteLine($"Args: {args}");

                var psi = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = args,
                    UseShellExecute = true,
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Process started, PID: {proc.Id}");
                    proc.WaitForExit();
                    System.Diagnostics.Debug.WriteLine($"Process exited with code: {proc.ExitCode}");
                }
                else
                {
                    throw new Exception("Не удалось запустить процесс");
                }
            }
            catch (Exception e)
            {
                MessageBox.Show($"Play Error: {e.Message}\n\nFile: {fname}\nStack: {e.StackTrace}");
            }
        }
    }
}