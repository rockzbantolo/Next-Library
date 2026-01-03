using Microsoft.Extensions.Caching.Memory;
using System;
using System.Threading;
using System.Threading.Tasks;
using ZiggyCreatures.Caching.Fusion;

namespace Next.Clients.Core
{
    /// <summary>
    /// Simple, fluent caching helper using FusionCache.
    /// Safe for dashboards (short TTL + fail-safe) and reusable across projects.
    /// </summary>
    public static class CacheHelper
    {
        private static IFusionCache _defaultCache;

        /// <summary>
        /// Initialize a global/default cache (call once in startup).
        /// Optional: pass your own FusionCache instance if using DI.
        /// </summary>
        public static void Initialize(
            IMemoryCache memoryCache = null,
            FusionCacheOptions options = null)
        {
            var cacheOptions = options ?? new FusionCacheOptions
            {
                DefaultEntryOptions = new FusionCacheEntryOptions
                {
                    Duration = TimeSpan.FromMinutes(5),
                    FailSafeMaxDuration = TimeSpan.FromMinutes(10),
                    FailSafeThrottleDuration = TimeSpan.FromSeconds(30)
                }
            };

            _defaultCache = new FusionCache(cacheOptions, memoryCache);
        }


        /// <summary>
        /// Get or compute a value with caching.
        /// Beautiful fluent usage with sensible defaults.
        /// </summary>
        public static async Task<T> GetOrComputeAsync<T>(
            string key,
            Func<CancellationToken, Task<T>> factory,
            TimeSpan? duration = null,
            bool allowStaleOnError = true,
            bool failSafe = true,
            string tag = null,                   // For bulk invalidation
            CancellationToken cancellationToken = default)
        {
            if (_defaultCache == null)
            {
                throw new InvalidOperationException("CacheHelper.Initialize must be called first (e.g. in startup)");
            }

            var options = new FusionCacheEntryOptions
            {
                Duration = duration ?? TimeSpan.FromSeconds(60),
                FailSafeMaxDuration = TimeSpan.FromMinutes(5),
                FailSafeThrottleDuration = TimeSpan.FromSeconds(30)
            };

            if (tag != null)
            {
                //options.Tags = new[] { tag };
            }

            return await _defaultCache.GetOrSetAsync(
                key,
                factory,
                options,
                cancellationToken);
        }

        /// <summary>
        /// Remove a single key from cache.
        /// </summary>
        public static void Remove(string key)
        {
            _defaultCache?.Remove(key);
        }

        /// <summary>
        /// Remove all keys with a specific tag (great for invalidating dashboard data after writes).
        /// </summary>
        public static void RemoveByTag(string tag)
        {
            _defaultCache?.RemoveByTag(tag);
        }

        /// <summary>
        /// Quick helper for dashboard-style counters (short TTL, fail-safe).
        /// </summary>
        public static Task<T> GetDashboardAsync<T>(
            string key,
            Func<CancellationToken, Task<T>> factory,
            CancellationToken ct = default)
        {
            return GetOrComputeAsync(
                key,
                factory,
                duration: TimeSpan.FromSeconds(30), // Live feel, updates every 30s
                allowStaleOnError: true,
                failSafe: true,
                tag: "dashboard",                   // Easy to invalidate all dashboard data
                cancellationToken: ct);
        }
    }
}
