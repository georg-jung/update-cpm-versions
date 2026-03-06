using System.Text.Json;

using NuGet.Versioning;

namespace DirectoryPackagesPropsUpdater;

sealed class NuGetClient : IDisposable
{
    private static readonly Uri NuGetOrgBaseUrl = new("https://api.nuget.org/v3-flatcontainer/");
    private readonly HttpClient _http;
    private readonly Uri _baseUrl;

    public NuGetClient(Uri? baseUrl = null, HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
        _baseUrl = baseUrl ?? NuGetOrgBaseUrl;
    }

    public static async Task<Uri> ResolveBaseUrlAsync(
        string serviceIndexUrl, HttpClient? http = null, CancellationToken ct = default)
    {
        var client = http ?? new HttpClient();
        try
        {
            using var response = await client.GetAsync(serviceIndexUrl, ct);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            foreach (var resource in doc.RootElement.GetProperty("resources").EnumerateArray())
            {
                var type = resource.GetProperty("@type").GetString();
                if (type is "PackageBaseAddress/3.0.0")
                {
                    return new Uri(resource.GetProperty("@id").GetString()!);
                }
            }

            throw new InvalidOperationException(
                "Could not find PackageBaseAddress/3.0.0 in service index.");
        }
        finally
        {
            if (http is null)
            {
                client.Dispose();
            }
        }
    }

    public async Task<List<NuGetVersion>> GetVersionsAsync(
        string packageId, CancellationToken ct = default)
    {
        var url = new Uri(_baseUrl, $"{packageId.ToLowerInvariant()}/index.json");

        using var response = await _http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var versions = new List<NuGetVersion>();
        if (doc.RootElement.TryGetProperty("versions", out var versionsArray))
        {
            foreach (var v in versionsArray.EnumerateArray())
            {
                if (NuGetVersion.TryParse(v.GetString(), out var version))
                {
                    versions.Add(version);
                }
            }
        }

        return versions;
    }

    public async Task<Dictionary<string, List<NuGetVersion>>> GetAllVersionsAsync(
        IReadOnlyList<string> packageIds,
        int maxConcurrency = 16,
        CancellationToken ct = default)
    {
        var result = new Dictionary<string, List<NuGetVersion>>(StringComparer.OrdinalIgnoreCase);

        await Parallel.ForEachAsync(
            packageIds,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = maxConcurrency,
                CancellationToken = ct,
            },
            async (id, token) =>
            {
                var versions = await GetVersionsAsync(id, token);
                lock (result)
                {
                    result[id] = versions;
                }
            });

        return result;
    }

    public void Dispose() => _http.Dispose();
}
