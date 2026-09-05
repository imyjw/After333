using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Project333.PvpServer.Security;

public sealed record Project333RateLimitSettings(
    bool Enabled,
    int WindowSeconds,
    int AuthPermitLimit,
    int AccountApiPermitLimit,
    int BattleConnectPermitLimit)
{
    public const int DefaultWindowSeconds = 60;
    public const int DefaultAuthPermitLimit = 12;
    public const int DefaultAccountApiPermitLimit = 120;
    public const int DefaultBattleConnectPermitLimit = 20;

    public static Project333RateLimitSettings FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new Project333RateLimitSettings(
            Enabled: ResolveBoolean(configuration, "PROJECT333_RATE_LIMIT_ENABLED", true),
            WindowSeconds: ResolveBoundedInt(
                configuration,
                "PROJECT333_RATE_LIMIT_WINDOW_SECONDS",
                DefaultWindowSeconds,
                1,
                3600),
            AuthPermitLimit: ResolveBoundedInt(
                configuration,
                "PROJECT333_AUTH_RATE_LIMIT_PER_WINDOW",
                DefaultAuthPermitLimit,
                1,
                100000),
            AccountApiPermitLimit: ResolveBoundedInt(
                configuration,
                "PROJECT333_ACCOUNT_RATE_LIMIT_PER_WINDOW",
                DefaultAccountApiPermitLimit,
                1,
                100000),
            BattleConnectPermitLimit: ResolveBoundedInt(
                configuration,
                "PROJECT333_BATTLE_CONNECT_RATE_LIMIT_PER_WINDOW",
                DefaultBattleConnectPermitLimit,
                1,
                100000));
    }

    private static bool ResolveBoolean(
        IConfiguration configuration,
        string key,
        bool fallback)
    {
        var rawValue = configuration[key];
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return fallback;
        }

        if (bool.TryParse(rawValue, out var booleanValue))
        {
            return booleanValue;
        }

        return rawValue.Trim() switch
        {
            "1" => true,
            "0" => false,
            _ => fallback
        };
    }

    private static int ResolveBoundedInt(
        IConfiguration configuration,
        string key,
        int fallback,
        int minimum,
        int maximum)
    {
        var rawValue = configuration[key];
        if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return fallback;
        }

        return Math.Clamp(value, minimum, maximum);
    }
}

public static class Project333RateLimiting
{
    private const string UnlimitedPartition = "unlimited";

    public static void Configure(
        RateLimiterOptions options,
        Project333RateLimitSettings settings)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(settings);

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            CreatePartition(context, settings));
        options.OnRejected = async (rejectedContext, cancellationToken) =>
        {
            var retryAfter = TimeSpan.FromSeconds(settings.WindowSeconds);
            if (rejectedContext.Lease.TryGetMetadata(MetadataName.RetryAfter, out var leaseRetryAfter))
            {
                retryAfter = leaseRetryAfter;
            }

            var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
            rejectedContext.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            rejectedContext.HttpContext.Response.Headers.RetryAfter =
                retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

            await rejectedContext.HttpContext.Response.WriteAsJsonAsync(
                new
                {
                    Error = new
                    {
                        Code = "rate_limited",
                        Message = "Too many requests. Please try again shortly.",
                        RetryAfterSeconds = retryAfterSeconds
                    }
                },
                cancellationToken);
        };
    }

    private static RateLimitPartition<string> CreatePartition(
        HttpContext context,
        Project333RateLimitSettings settings)
    {
        if (!settings.Enabled)
        {
            return RateLimitPartition.GetNoLimiter(UnlimitedPartition);
        }

        var category = ResolveCategory(context.Request.Path);
        if (category == RateLimitCategory.None)
        {
            return RateLimitPartition.GetNoLimiter(UnlimitedPartition);
        }

        var clientAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var permitLimit = category switch
        {
            RateLimitCategory.Auth => settings.AuthPermitLimit,
            RateLimitCategory.AccountApi => settings.AccountApiPermitLimit,
            RateLimitCategory.BattleConnect => settings.BattleConnectPermitLimit,
            _ => settings.AccountApiPermitLimit
        };

        var partitionKey = $"{category}:{clientAddress}";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = permitLimit,
                QueueLimit = 0,
                Window = TimeSpan.FromSeconds(settings.WindowSeconds)
            });
    }

    private static RateLimitCategory ResolveCategory(PathString path)
    {
        if (path.StartsWithSegments("/auth"))
        {
            return RateLimitCategory.Auth;
        }

        if (path.Equals("/battle", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitCategory.BattleConnect;
        }

        if (path.Equals("/me", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/pvp/reconnect-status") ||
            path.StartsWithSegments("/wallet") ||
            (path.StartsWithSegments("/ads") &&
             !path.StartsWithSegments("/ads/levelplay/rewarded-callback")) ||
            path.StartsWithSegments("/cards") ||
            path.StartsWithSegments("/runs"))
        {
            return RateLimitCategory.AccountApi;
        }

        return RateLimitCategory.None;
    }

    private enum RateLimitCategory
    {
        None = 0,
        Auth = 1,
        AccountApi = 2,
        BattleConnect = 3
    }
}
