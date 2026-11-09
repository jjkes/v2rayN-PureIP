namespace ServiceLib.Handler;

public static class ConnectionHandler
{
    private static readonly string _tag = "ConnectionHandler";

    /// <summary>
    /// Runs ping and IP checks.
    /// </summary>
    public static async Task<AvailabilityCheckResult> RunAvailabilityCheck()
    {
        var time = await GetRealPingTimeInfo();
        var ip = time > 0 ? await GetIPInfo() : Global.None;

        return new AvailabilityCheckResult(time, ip);
    }

    /// <summary>
    /// Gets IP information using the default local proxy.
    /// </summary>
    private static async Task<string?> GetIPInfo()
    {
        using var downloadHandle = new DownloadService
        {
            Handler = HttpClientHelper.CreateSocketsHttpHandler(true, new()),
            DisposeHandler = true,
        };
        var ipInfo = await GetIPInfo(downloadHandle);
        return ipInfo?.ToString() ?? Global.None;
    }

    /// <summary>
    /// Measures real ping time using configured test URL.
    /// </summary>
    private static async Task<int> GetRealPingTimeInfo()
    {
        var responseTime = -1;
        try
        {
            for (var i = 0; i < 2; i++)
            {
                responseTime = await GetRealPingTime(true);
                if (responseTime > 0)
                {
                    break;
                }
                await Task.Delay(500);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return -1;
        }
        return responseTime;
    }

    /// <summary>
    /// Measures response time by sending HTTP requests through proxy.
    /// </summary>
    public static async Task<int> GetRealPingTime(bool useProxy, CancellationToken cancellationToken = default)
    {
        var responseTime = -1;
        try
        {
            using var client = HttpClientHelper.Instance.GetHttpClient(useProxy);
            responseTime = await GetRealPingTime(client, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Ignore
        }
        return responseTime;
    }

    public static async Task<int> GetRealPingTime(HttpClient client, CancellationToken cancellationToken = default)
    {
        var url = AppManager.Instance.Config.SpeedTestItem.SpeedPingTestUrl;
        var responseTime = -1;
        try
        {
            using var timeoutCts = new CancellationTokenSource();
            timeoutCts.CancelAfter(Global.LocalFetch);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            var linkedToken = linkedCts.Token;

            List<int> oneTime = [];
            for (var i = 0; i < 2; i++)
            {
                var timer = Stopwatch.StartNew();
                await client.GetAsync(url, linkedToken).ConfigureAwait(false);
                timer.Stop();
                oneTime.Add((int)timer.Elapsed.TotalMilliseconds);
                await Task.Delay(100, linkedToken);
            }
            responseTime = oneTime.Where(x => x > 0).OrderBy(x => x).FirstOrDefault();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Ignore
        }
        return responseTime;
    }

    public static async Task<IpInfoResult?> GetIPInfo(DownloadService downloadHandle, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = AppManager.Instance.Config.SpeedTestItem.IPAPIUrl;
            if (url.IsNullOrEmpty())
            {
                return null;
            }

            var result = await downloadHandle.TryDownloadString(url, cancellationToken);
            if (result == null)
            {
                return null;
            }

            var ipInfo = JsonUtils.Deserialize<IPAPIInfo>(result);
            if (ipInfo == null)
            {
                return null;
            }

            var ip = ipInfo.ip ?? ipInfo.clientIp ?? ipInfo.ip_addr ?? ipInfo.query;
            var country = ipInfo.country_code ?? ipInfo.country ?? ipInfo.countryCode ?? ipInfo.location?.country_code ?? "unknown";

            return new IpInfoResult(country, ip);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets IP purity information using the given proxy handler.
    /// Tries the configured endpoint first (ippure by default) and falls back to
    /// ip-api.com when the primary source is blocked or returns an unusable payload.
    /// </summary>
    public static async Task<IpPurityResult?> GetIpPurityInfo(DownloadService downloadHandle,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = AppManager.Instance.Config.SpeedTestItem.IpPurityUrl;
            if (url.IsNullOrEmpty())
            {
                return null;
            }

            var result = await TryGetIpPurityAsync(downloadHandle, url, cancellationToken);
            if (result is not null)
            {
                return result;
            }

            if (url != Global.IpPurityFallbackUrl)
            {
                return await TryGetIpPurityAsync(downloadHandle, Global.IpPurityFallbackUrl, cancellationToken);
            }
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<IpPurityResult?> TryGetIpPurityAsync(DownloadService downloadHandle, string url,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = await downloadHandle.TryDownloadString(url, cancellationToken);
            if (payload.IsNullOrEmpty())
            {
                return null;
            }
            return ParseIpPurity(payload) ?? ParseIpApiRisk(payload);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    public static IpPurityResult? ParseIpPurity(string? json)
    {
        try
        {
            if (json.IsNullOrEmpty())
            {
                return null;
            }
            var info = JsonUtils.Deserialize<IpPurityInfo>(json);
            if (info?.fraudScore is null)
            {
                return null;
            }
            var purity = Math.Clamp(100 - info.fraudScore.Value, 0, 100);
            var type = info.isBroadcast == true
                ? ResUI.IpPurityBroadcast
                : info.isResidential == true ? ResUI.IpPurityResidential : ResUI.IpPurityHosting;
            return new IpPurityResult(purity, type, info.ip);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Heuristic purity from the ip-api.com fallback. It has no fraud score, so the
    /// score only distinguishes hosting/proxy/mobile exits and is deliberately coarse.
    /// </summary>
    public static IpPurityResult? ParseIpApiRisk(string? json)
    {
        try
        {
            if (json.IsNullOrEmpty())
            {
                return null;
            }
            var info = JsonUtils.Deserialize<IpApiRiskInfo>(json);
            if (info is null || !string.Equals(info.status, "success", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            if (info.hosting is null && info.proxy is null && info.mobile is null)
            {
                return null;
            }

            var hosting = info.hosting == true;
            var proxy = info.proxy == true;
            var purity = hosting && proxy ? 25
                : hosting ? 40
                : proxy ? 60
                : 90;
            var type = hosting ? ResUI.IpPurityHosting
                : proxy ? ResUI.IpPurityProxy
                : ResUI.IpPurityResidential;
            return new IpPurityResult(purity, type, info.query);
        }
        catch
        {
            return null;
        }
    }
}
