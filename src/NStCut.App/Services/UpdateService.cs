using System.Net.Http;
using System.Text.Json;
using System.Reflection;

namespace NStCut.App.Services;

public sealed class UpdateService
{
    private const string ReleaseApiUrl = "https://api.github.com/repos/eanord20/windows-tool/releases/latest";
    private const string AssetName = "NStCut_Setup.exe";

    private readonly HttpClient _httpClient = new();

    public UpdateService()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "NStCut");
    }

    public async Task<bool> IsUpdateAvailableAsync(Version currentVersion)
    {
        try
        {
            var response = await _httpClient.GetStringAsync(ReleaseApiUrl);
            using var doc = JsonDocument.Parse(response);
            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? string.Empty;
            if (tag.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                tag = tag[1..];
            if (Version.TryParse(tag, out var latest))
                return latest > currentVersion;
        }
        catch
        {
            // ignore network/parsing errors for MVP skeleton
        }
        return false;
    }

    public Version CurrentVersion =>
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0, 0);
}
