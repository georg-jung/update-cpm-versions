using System.Net;
using System.Net.Sockets;
using System.Text;

using NuGet.Versioning;

using UpdateCpmVersions;

namespace UpdateCpmVersions.Tests;

public class CustomSourceTests
{
    [Test]
    public async Task ResolveBaseUrlAsync_ReadsPackageBaseAddress()
    {
        await using var feed = new StubFeed(includePackageBaseResource: true);

        var result = await NuGetClient.ResolveBaseUrlAsync(feed.ServiceIndexUrl);

        await Assert.That(result).IsEqualTo(new Uri(feed.FlatContainerUrl));
    }

    [Test]
    public async Task ResolveBaseUrlAsync_ThrowsWhenResourceMissing()
    {
        await using var feed = new StubFeed(includePackageBaseResource: false);

        await Assert.That(async () => await NuGetClient.ResolveBaseUrlAsync(feed.ServiceIndexUrl))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task RunAsync_UsesCustomSourceFeed()
    {
        await using var feed = new StubFeed(
            new Dictionary<string, List<string>>
            {
                ["custom.package"] = ["1.0.0", "1.1.0"],
            });

        var path = WriteTempProps("""
            <Project>
              <ItemGroup>
                <PackageVersion Include="Custom.Package" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        var exitCode = await PackageUpdater.RunAsync(path, new UpdateOptions(), feed.ServiceIndexUrl, CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(0);

        var (_, packages) = PackagePropsParser.Parse(path);
        await Assert.That(packages[0].Version).IsEqualTo(NuGetVersion.Parse("1.1.0"));
    }

    private static string WriteTempProps(string content)
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, "Directory.Packages.props");
        File.WriteAllText(filePath, content);
        return filePath;
    }

    private sealed class StubFeed : IAsyncDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loop;
        private readonly Dictionary<string, List<string>> _packages;
        private readonly bool _includePackageBaseResource;

        public string BaseAddress { get; }
        public string ServiceIndexUrl => $"{BaseAddress}index.json";
        public string FlatContainerUrl => $"{BaseAddress}v3/flat/";

        public StubFeed(
            Dictionary<string, List<string>>? packages = null,
            bool includePackageBaseResource = true)
        {
            _packages = (packages ?? new Dictionary<string, List<string>>())
                .ToDictionary(kvp => kvp.Key.ToLowerInvariant(), kvp => kvp.Value);
            _includePackageBaseResource = includePackageBaseResource;

            var port = GetFreePort();
            BaseAddress = $"http://127.0.0.1:{port}/";
            _listener.Prefixes.Add(BaseAddress);
            _listener.Start();
            _loop = Task.Run(HandleRequests);
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private async Task HandleRequests()
        {
            while (!_cts.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (HttpListenerException) when (_cts.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                _ = Task.Run(() => ProcessRequestAsync(context));
            }
        }

        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            try
            {
                var path = context.Request.Url?.AbsolutePath ?? string.Empty;
                if (path.Equals("/index.json", StringComparison.OrdinalIgnoreCase))
                {
                    var resources = _includePackageBaseResource
                        ? $"[{{\"@id\":\"{FlatContainerUrl}\",\"@type\":\"PackageBaseAddress/3.0.0\"}}]"
                        : "[]";
                    var json = $"{{\"resources\":{resources}}}";
                    await WriteJsonAsync(context.Response, json);
                    return;
                }

                if (path.StartsWith("/v3/flat/", StringComparison.OrdinalIgnoreCase))
                {
                    var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length >= 3 && segments[^1].Equals("index.json", StringComparison.OrdinalIgnoreCase))
                    {
                        var id = segments[2];
                        if (_packages.TryGetValue(id.ToLowerInvariant(), out var versions))
                        {
                            var versionList = string.Join("\",\"", versions);
                            var json = $"{{\"versions\":[\"{versionList}\"]}}";
                            await WriteJsonAsync(context.Response, json);
                            return;
                        }
                    }
                }

                context.Response.StatusCode = 404;
                context.Response.Close();
            }
            catch
            {
                if (context.Response.OutputStream.CanWrite)
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
            }
        }

        private static async Task WriteJsonAsync(HttpListenerResponse response, string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            response.ContentType = "application/json";
            response.StatusCode = 200;
            await response.OutputStream.WriteAsync(bytes);
            response.Close();
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _listener.Close();

            try
            {
                await _loop;
            }
            catch
            {
                // Ignore background loop exceptions on shutdown
            }

            _cts.Dispose();
        }
    }
}
