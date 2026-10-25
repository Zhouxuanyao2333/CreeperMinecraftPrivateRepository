using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Project.Launch.Tools.Download
{
    public static class VersionService
    {
        // 复用 HttpClient，避免产生过多 Socket
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        public static async Task<VersionManifest?> GetVersionManifestAsync()
        {
            // 遵循 BMCLAPI 优先，官方源兜底的策略
            string[] urls = 
            {
                "https://bmclapi2.bangbang93.com/mc/game/version_manifest_v2.json",
                "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json"
            };

            foreach (var url in urls)
            {
                try
                {
                    string json = await _httpClient.GetStringAsync(url);
                    var manifest = JsonSerializer.Deserialize<VersionManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (manifest != null && manifest.Versions.Count > 0)
                    {
                        LogHelper.Write(LauncherPaths.AppLog, 
                            $"成功获取版本清单({url})，共 {manifest.Versions.Count} 个版本", 
                            $"Successfully fetched manifest from {url}, count: {manifest.Versions.Count}");
                        return manifest;
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Write(LauncherPaths.AppLog, 
                        $"获取版本清单失败({url}): {ex.Message}", 
                        $"Failed to fetch manifest ({url}): {ex.Message}");
                }
            }
            return null;
        }
    }
}