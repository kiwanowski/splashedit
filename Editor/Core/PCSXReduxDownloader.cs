using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace SplashEdit.EditorCode
{
    /// <summary>
    /// Downloads and installs PCSX-Redux from the official distrib.app CDN.
    /// Mirrors the logic from pcsx-redux.js (the official download script).
    /// 
    /// Flow: fetch platform manifest → find latest build ID → fetch build manifest →
    ///       get download URL → download zip → extract to .tools/pcsx-redux/
    /// </summary>
    public static class PCSXReduxDownloader
    {
        private const string MANIFEST_BASE = "https://distrib.app/storage/manifests/pcsx-redux/";

        private static readonly HttpClient _http;

        static PCSXReduxDownloader()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip
                                       | System.Net.DecompressionMethods.Deflate
            };
            _http = new HttpClient(handler);
            _http.Timeout = TimeSpan.FromSeconds(60);
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("SplashEdit/1.0");
        }

        /// <summary>
        /// Returns the platform variant string for the current platform. 
        /// </summary> 
        private static string GetPlatformVariant()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    return "dev-win-cli-x64"; 
                case RuntimePlatform.LinuxEditor:
                    return "dev-linux-x64";
                default:
                    return "dev-win-cli-x64";
            }
        }

        /// <summary>
        /// Downloads and installs PCSX-Redux to .tools/pcsx-redux/.
        /// Shows progress bar during download.
        /// </summary>
        public static async Task<bool> DownloadAndInstall(Action<string> log = null)
        {
            string variant = GetPlatformVariant();
            log?.Invoke($"Platform variant: {variant}");

            try
            {
                // Step 1: Fetch the master manifest to get the latest build ID
                string manifestUrl = $"{MANIFEST_BASE}{variant}/manifest.json";
                log?.Invoke($"Fetching manifest: {manifestUrl}");
                string manifestJson = await _http.GetStringAsync(manifestUrl);

                // Parse the latest build ID from the manifest.
                // The manifest is JSON with a "builds" array. We want the highest ID.
                // Simple JSON parsing without dependencies:
                int latestBuildId = ParseLatestBuildId(manifestJson);
                if (latestBuildId < 0)
                {
                    log?.Invoke("Failed to parse build ID from manifest.");
                    return false;
                }
                log?.Invoke($"Latest build ID: {latestBuildId}");

                // Step 2: Fetch the specific build manifest
                string buildManifestUrl = $"{MANIFEST_BASE}{variant}/manifest-{latestBuildId}.json";
                log?.Invoke($"Fetching build manifest...");
                string buildManifestJson = await _http.GetStringAsync(buildManifestUrl);

                // Parse the download path
                string downloadPath = ParseDownloadPath(buildManifestJson);
                if (string.IsNullOrEmpty(downloadPath))
                {
                    log?.Invoke("Failed to parse download path from build manifest.");
                    return false;
                }

                string downloadUrl = $"https://distrib.app{downloadPath}";
                log?.Invoke($"Downloading: {downloadUrl}");

                // Step 3: Download the file
                string tempFile = Path.Combine(Path.GetTempPath(), $"pcsx-redux-{latestBuildId}.zip");
                EditorUtility.DisplayProgressBar("Downloading PCSX-Redux", "Downloading...", 0.1f);

                using (var client = new System.Net.WebClient())
                {
                    client.Headers.Add("User-Agent", "SplashEdit/1.0");
                    
                    client.DownloadProgressChanged += (s, e) =>
                    {
                        float progress = 0.1f + 0.8f * (e.ProgressPercentage / 100f);
                        string sizeMB = $"{e.BytesReceived / (1024 * 1024)}/{e.TotalBytesToReceive / (1024 * 1024)} MB";
                        EditorUtility.DisplayProgressBar("Downloading PCSX-Redux", $"Downloading... {sizeMB}", progress);
                    };

                    await client.DownloadFileTaskAsync(new Uri(downloadUrl), tempFile);
                }

                log?.Invoke($"Downloaded to {tempFile}");
                EditorUtility.DisplayProgressBar("Installing PCSX-Redux", "Extracting...", 0.9f);

                // Step 4: Extract
                string installDir = SplashBuildPaths.PCSXReduxDir;
                if (Directory.Exists(installDir))
                    Directory.Delete(installDir, true);
                Directory.CreateDirectory(installDir);

                if (Application.platform == RuntimePlatform.LinuxEditor && tempFile.EndsWith(".tar.gz"))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "tar",
                        Arguments = $"xzf \"{tempFile}\" -C \"{installDir}\" --strip-components=1",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    var proc = Process.Start(psi);
                    proc?.WaitForExit();

                }
                else
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(tempFile, installDir);
                    log?.Invoke($"Extracted to {installDir}");
                }

                // Make executable

                if(Application.platform == RuntimePlatform.LinuxEditor) {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"+x \"{SplashBuildPaths.PCSXReduxBinary}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    var proc = Process.Start(psi);
                    proc?.WaitForExit();
                }

                // Clean up temp file
                try { File.Delete(tempFile); } catch { }

                // Step 5: Verify
                if (SplashBuildPaths.IsPCSXReduxInstalled())
                {
                    log?.Invoke("PCSX-Redux installed successfully!");
                    EditorUtility.ClearProgressBar();
                    return true;
                }
                else
                {
                    // The zip might have a nested directory — try to find the exe
                    SplashEdit.RuntimeCode.Utils.FixNestedDirectory(installDir);
                    if (SplashBuildPaths.IsPCSXReduxInstalled())
                    {
                        log?.Invoke("PCSX-Redux installed successfully!");
                        EditorUtility.ClearProgressBar();
                        return true;
                    }

                    log?.Invoke("Installation completed but PCSX-Redux binary not found at expected path.");
                    log?.Invoke($"Expected: {SplashBuildPaths.PCSXReduxBinary}");
                    log?.Invoke($"Check: {installDir}");
                    EditorUtility.ClearProgressBar();
                    return false;
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"Download failed: {ex.Message}");
                EditorUtility.ClearProgressBar();
                return false;
            }
        }

        /// <summary>
        /// Parse the latest build ID from the master manifest JSON.
        /// Expected format: {"builds":[{"id":1234,...},...],...}
        /// distrib.app returns builds sorted newest-first, so we take the first.
        /// Falls back to scanning all IDs if the "builds" section isn't found.
        /// </summary>
        private static int ParseLatestBuildId(string json)
        {
            // Fast path: find the first "id" inside "builds" array
            int buildsIdx = json.IndexOf("\"builds\"", StringComparison.Ordinal);
            int startPos = buildsIdx >= 0 ? buildsIdx : 0;

            string searchToken = "\"id\":";
            int idx = json.IndexOf(searchToken, startPos, StringComparison.Ordinal);
            if (idx < 0) return -1;

            int pos = idx + searchToken.Length;
            while (pos < json.Length && char.IsWhiteSpace(json[pos])) pos++;

            int numStart = pos;
            while (pos < json.Length && char.IsDigit(json[pos])) pos++;

            if (pos > numStart && int.TryParse(json.Substring(numStart, pos - numStart), out int id))
                return id;

            return -1;
        }

        /// <summary>
        /// Parse the download path from a build-specific manifest.
        /// Expected format: {...,"path":"/storage/builds/..."}
        /// </summary>
        private static string ParseDownloadPath(string json)
        {
            string searchToken = "\"path\":";
            int idx = json.IndexOf(searchToken, StringComparison.Ordinal);
            if (idx < 0) return null;

            int pos = idx + searchToken.Length;
            while (pos < json.Length && char.IsWhiteSpace(json[pos])) pos++;

            if (pos >= json.Length || json[pos] != '"') return null;
            pos++; // skip opening quote

            int pathStart = pos;
            while (pos < json.Length && json[pos] != '"') pos++;

            return json.Substring(pathStart, pos - pathStart);
        }
    }
}
