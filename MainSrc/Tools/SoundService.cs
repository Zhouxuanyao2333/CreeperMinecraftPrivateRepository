using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform;
using NAudio.Wave;

namespace Project.Launch.Tools
{
    /// <summary>
    /// 音效播放服务——从内嵌资源读取 wav
    /// Sound playback service - reads wav from embedded resources
    /// </summary>
    public static class SoundService
    {
        private static readonly List<WaveOutEvent> _active = new();
        private static readonly object _lock = new();

        /// <summary>
        /// 播放音效（逻辑名 == 文件名，不含 .wav）
        /// Play a sound (logical name == filename, no .wav extension)
        /// 注意：内部走后台线程，不会阻塞 UI
        /// </summary>
        public static void Play(string name)
        {
            if (!SoundConfig.Enabled) return;
            if (string.IsNullOrWhiteSpace(name)) return;

            // ★ 音频设备初始化是同步 IO（100~300ms），放后台线程避免卡 UI
            Task.Run(() => PlayInternal(name));
        }

        private static void PlayInternal(string name)
        {
            try
            {
                string uriStr = SoundConfig.AssetRoot + name + ".wav";
                using var assetStream = AssetLoader.Open(new Uri(uriStr));

                // NAudio 的 WaveFileReader 需要可 Seek 的流，先拷进内存
                var memory = new MemoryStream();
                assetStream.CopyTo(memory);
                memory.Position = 0;

                var reader = new WaveFileReader(memory);
                var output = new WaveOutEvent
                {
                    Volume = (float)Math.Clamp(SoundConfig.GetVolume(name), 0.0, 1.0)
                };
                output.Init(reader);

                output.PlaybackStopped += (s, e) =>
                {
                    lock (_lock) _active.Remove(output);
                    try { output.Dispose(); } catch { }
                    try { reader.Dispose(); } catch { }
                    try { memory.Dispose(); } catch { }
                };

                lock (_lock) _active.Add(output);

                output.Play();
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"播放音效失败 ({name}): {ex.Message}",
                    $"Failed to play sound ({name}): {ex.Message}");
            }
        }

        /// <summary>
        /// 停止全部正在播放的音效
        /// Stop all currently playing sounds
        /// </summary>
        public static void StopAll()
        {
            lock (_lock)
            {
                foreach (var o in _active)
                {
                    try { o.Stop(); } catch { }
                }
                _active.Clear();
            }
        }
    }
}