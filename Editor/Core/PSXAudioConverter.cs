using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using SplashEdit.RuntimeCode;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SplashEdit.EditorCode
{
    /// <summary>
    /// Downloads psxavenc and converts WAV audio to PS1 SPU ADPCM format.
    /// psxavenc is the standard tool for PS1 audio encoding from the
    /// WonderfulToolchain project.
    /// </summary>
    [InitializeOnLoad]
    public static class PSXAudioConverter
    {
        static PSXAudioConverter()
        {
            // Register the converter delegate so Runtime code can call it
            // without directly referencing this Editor assembly.
            PSXSceneExporter.AudioConvertDelegate = ConvertToADPCM;
        }

        private const string PSXAVENC_VERSION = "v0.3.1";
        private const string PSXAVENC_RELEASE_BASE = 
            "https://github.com/WonderfulToolchain/psxavenc/releases/download/";

        private static readonly HttpClient _http = new HttpClient();

        /// <summary>
        /// Path to the psxavenc binary inside .tools/
        /// </summary>
        public static string PsxavencBinary
        {
            get
            {
                string dir = Path.Combine(SplashBuildPaths.ToolsDir, "psxavenc");
                if (Application.platform == RuntimePlatform.WindowsEditor)
                    return Path.Combine(dir, "psxavenc.exe");
                return Path.Combine(dir, "psxavenc");
            }
        }

        public static bool IsInstalled() => File.Exists(PsxavencBinary);

        /// <summary>
        /// Downloads and installs psxavenc from the official GitHub releases.
        /// </summary>
        public static async Task<bool> DownloadAndInstall(Action<string> log = null)
        {
            string archiveName;
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    archiveName = $"psxavenc-windows.zip";
                    break;
                case RuntimePlatform.LinuxEditor:
                    archiveName = $"psxavenc-linux.zip";
                    break;
                default:
                    log?.Invoke("Only Windows and Linux are supported.");
                    return false;
            }

            string downloadUrl = $"{PSXAVENC_RELEASE_BASE}{PSXAVENC_VERSION}/{archiveName}";
            log?.Invoke($"Downloading psxavenc: {downloadUrl}");

            try
            {
                string tempFile = Path.Combine(Path.GetTempPath(), archiveName);
                EditorUtility.DisplayProgressBar("Downloading psxavenc", "Downloading...", 0.1f);

                using (var client = new System.Net.WebClient())
                {
                    client.Headers.Add("User-Agent", "SplashEdit/1.0");

                    client.DownloadProgressChanged += (s, e) =>
                    {
                        float progress = 0.1f + 0.8f * (e.ProgressPercentage / 100f);
                        string sizeMB = $"{e.BytesReceived / (1024 * 1024)}/{e.TotalBytesToReceive / (1024 * 1024)} MB";
                        EditorUtility.DisplayProgressBar("Downloading psxavenc", $"Downloading... {sizeMB}", progress);
                    };

                    await client.DownloadFileTaskAsync(new Uri(downloadUrl), tempFile);
                }

                log?.Invoke("Extracting...");
                EditorUtility.DisplayProgressBar("Installing psxavenc", "Extracting...", 0.9f);

                string installDir = Path.Combine(SplashBuildPaths.ToolsDir, "psxavenc");
                if (Directory.Exists(installDir))
                    Directory.Delete(installDir, true);
                Directory.CreateDirectory(installDir);

                if (tempFile.EndsWith(".zip"))
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(tempFile, installDir);
                }
                else
                {
                    // tar.gz extraction — use system tar
                    var psi = new ProcessStartInfo
                    {
                        FileName = "tar",
                        Arguments = $"xzf \"{tempFile}\" -C \"{installDir}\" --strip-components=1",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    var proc = Process.Start(psi);
                    proc.WaitForExit();
                }

                // Fix nested directory (sometimes archives have one extra level)
                SplashEdit.RuntimeCode.Utils.FixNestedDirectory(installDir);

                try { File.Delete(tempFile); } catch { }

                EditorUtility.ClearProgressBar();

                if (IsInstalled())
                {
                    // Make executable on Linux
                    if (Application.platform == RuntimePlatform.LinuxEditor)
                    {
                        var chmod = Process.Start("chmod", $"+x \"{PsxavencBinary}\"");
                        chmod?.WaitForExit();
                    }
                    log?.Invoke("psxavenc installed successfully!");
                    return true;
                }

                log?.Invoke($"psxavenc binary not found at: {PsxavencBinary}");
                return false;
            }
            catch (Exception ex)
            {
                log?.Invoke($"psxavenc download failed: {ex.Message}");
                EditorUtility.ClearProgressBar();
                return false;
            }
        }

        /// <summary>
        /// Converts a Unity AudioClip to PS1 SPU ADPCM format using psxavenc.
        /// Returns the ADPCM byte array, or null on failure.
        /// </summary>
        public static byte[] ConvertToADPCM(AudioClip clip, int targetSampleRate, bool loop)
        {
            if (!IsInstalled())
            {
                Debug.LogError("[SplashEdit] psxavenc not installed. Install it from the Setup tab.");
                return null;
            }

            if (clip == null)
            {
                Debug.LogError("[SplashEdit] AudioClip is null.");
                return null;
            }

            // Export Unity AudioClip to a temporary WAV file
            string tempWav = Path.Combine(Path.GetTempPath(), $"psx_audio_{clip.name}.wav");
            string tempVag = Path.Combine(Path.GetTempPath(), $"psx_audio_{clip.name}.vag");

            try
            {
                ExportWav(clip, tempWav);

                // Run psxavenc: convert WAV to SPU ADPCM
                // -t spu: raw SPU ADPCM output (no header, ready for DMA upload)
                // -f <rate>: target sample rate
                // -L: enable looping flag in the last ADPCM block
                string loopFlag = loop ? "-L" : "";
                string args = $"-t spu -f {targetSampleRate} {loopFlag} \"{tempWav}\" \"{tempVag}\"";

                var psi = new ProcessStartInfo
                {
                    FileName = PsxavencBinary,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                var process = Process.Start(psi);
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    Debug.LogError($"[SplashEdit] psxavenc failed: {stderr}");
                    return null;
                }

                if (!File.Exists(tempVag))
                {
                    Debug.LogError("[SplashEdit] psxavenc produced no output file.");
                    return null;
                }

                // -t spu outputs raw SPU ADPCM blocks (no header) — use directly.
                byte[] adpcm = File.ReadAllBytes(tempVag);
                if (adpcm.Length == 0)
                {
                    Debug.LogError("[SplashEdit] psxavenc produced empty output.");
                    return null;
                }
                return adpcm;
            }
            finally
            {
                try { if (File.Exists(tempWav)) File.Delete(tempWav); } catch { }
                try { if (File.Exists(tempVag)) File.Delete(tempVag); } catch { }
            }
        }

        /// <summary>
        /// Exports a Unity AudioClip to a 16-bit mono WAV file.
        /// </summary>
        private static void ExportWav(AudioClip clip, string path)
        {
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            // Downmix to mono if stereo
            float[] mono;
            if (clip.channels > 1)
            {
                mono = new float[clip.samples];
                for (int i = 0; i < clip.samples; i++)
                {
                    float sum = 0;
                    for (int ch = 0; ch < clip.channels; ch++)
                        sum += samples[i * clip.channels + ch];
                    mono[i] = sum / clip.channels;
                }
            }
            else
            {
                mono = samples;
            }

            // Write WAV
            using (var fs = new FileStream(path, FileMode.Create))
            using (var writer = new BinaryWriter(fs))
            {
                int sampleCount = mono.Length;
                int dataSize = sampleCount * 2; // 16-bit
                int fileSize = 44 + dataSize;

                // RIFF header
                writer.Write(new char[] { 'R', 'I', 'F', 'F' });
                writer.Write(fileSize - 8);
                writer.Write(new char[] { 'W', 'A', 'V', 'E' });

                // fmt chunk
                writer.Write(new char[] { 'f', 'm', 't', ' ' });
                writer.Write(16); // chunk size
                writer.Write((short)1); // PCM
                writer.Write((short)1); // mono
                writer.Write(clip.frequency);
                writer.Write(clip.frequency * 2); // byte rate
                writer.Write((short)2); // block align
                writer.Write((short)16); // bits per sample

                // data chunk
                writer.Write(new char[] { 'd', 'a', 't', 'a' });
                writer.Write(dataSize);

                for (int i = 0; i < sampleCount; i++)
                {
                    short sample = (short)(Mathf.Clamp(mono[i], -1f, 1f) * 32767f);
                    writer.Write(sample);
                }
            }
        }
    }
}
