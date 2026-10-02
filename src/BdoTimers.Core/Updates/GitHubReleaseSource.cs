using System.Text.Json;

namespace BdoTimers.Core.Updates;

/// <summary>Reads public releases only from the project's official GitHub repository.</summary>
public sealed class GitHubReleaseSource(HttpClient http) : IReleaseSource
{
    public const string Repository = "https://github.com/feluminais/BdoTimers";
    const string Api = "https://api.github.com/repos/feluminais/BdoTimers/releases";
    const int PageSize = 100;
    const long MaximumResponseBytes = 4 * 1024 * 1024;

    public async Task<ReleaseInfo?> LatestStableAsync(CancellationToken cancel)
    {
        ReleaseInfo? latest = null;
        for (var page = 1; ; page++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{Api}?per_page={PageSize}&page={page}");
            request.Headers.UserAgent.ParseAdd("BdoTimers");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await response.Content.LoadIntoBufferAsync(MaximumResponseBytes, cancel).ConfigureAwait(false);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false));
            if (body.RootElement.ValueKind != JsonValueKind.Array) throw new JsonException("Expected a release list.");
            foreach (var release in body.RootElement.EnumerateArray())
            {
                if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean()) continue;
                var tag = release.GetProperty("tag_name").GetString();
                if (string.IsNullOrEmpty(tag)) throw new JsonException("Missing release tag.");
                // Non-numeric tags and prerelease suffixes cannot describe a stable installed product version.
                if (!ReleaseVersion.TryParse(tag, out var version)) continue;
                var expected = new Uri($"{Repository}/releases/tag/{Uri.EscapeDataString(tag)}");
                if (!Uri.TryCreate(release.GetProperty("html_url").GetString(), UriKind.Absolute, out var url)
                    || Uri.Compare(url, expected, UriComponents.AbsoluteUri, UriFormat.Unescaped,
                        StringComparison.Ordinal) != 0) throw new JsonException("Unexpected release page.");
                if (latest is null || version > latest.Version) latest = new(version, expected);
            }
            if (body.RootElement.GetArrayLength() < PageSize) return latest;
        }
    }
}
