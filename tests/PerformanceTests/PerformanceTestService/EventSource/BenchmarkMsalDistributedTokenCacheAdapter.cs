﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web.TokenCacheProviders.Distributed;

namespace PerformanceTestService.EventSource
{
    /// <summary>
    /// Adds benchmarking counters on top of <see cref="MsalDistributedTokenCacheAdapter"/>.
    /// </summary>
    public class BenchmarkMsalDistributedTokenCacheAdapter : MsalDistributedTokenCacheAdapter
    {
        public BenchmarkMsalDistributedTokenCacheAdapter(
            IDistributedCache memoryCache,
            IOptions<MsalDistributedTokenCacheAdapterOptions> cacheOptions) : base(memoryCache, cacheOptions)
        {
        }

        /// <summary>
        /// Removes a specific token cache, described by its cache key
        /// from the distributed cache.
        /// </summary>
        /// <param name="cacheKey">Key of the cache to remove.</param>
        /// <returns>A <see cref="Task"/> that completes when key removal has completed.</returns>
        protected override Task RemoveKeyAsync(string cacheKey)
        {
            var bytes = base.ReadCacheBytesAsync(cacheKey).GetAwaiter().GetResult();

            if (bytes != null)
            {
                MemoryCacheEventSource.Log.DecrementSize(bytes.Length);
            }

            MemoryCacheEventSource.Log.IncrementRemoveCount();
            return base.RemoveKeyAsync(cacheKey);
        }

        /// <summary>
        /// Read a specific token cache, described by its cache key, from the
        /// distributed cache.
        /// </summary>
        /// <param name="cacheKey">Key of the cache item to retrieve.</param>
        /// <returns>Read blob representing a token cache for the cache key
        /// (account or app).</returns>
        protected override Task<byte[]> ReadCacheBytesAsync(string cacheKey)
        {
            var stopwatch = Stopwatch.StartNew();
            var bytes = base.ReadCacheBytesAsync(cacheKey).GetAwaiter().GetResult();
            stopwatch.Stop();

            MemoryCacheEventSource.Log.IncrementReadCount();
            MemoryCacheEventSource.Log.AddReadDuration(stopwatch.ElapsedMilliseconds);
            if (bytes == null)
            {
                MemoryCacheEventSource.Log.IncrementReadMissCount();
            }

            return Task.FromResult(bytes);
        }

        /// <summary>
        /// Writes a token cache blob to the serialization cache (by key).
        /// </summary>
        /// <param name="cacheKey">Cache key.</param>
        /// <param name="bytes">blob to write.</param>
        /// <returns>A <see cref="Task"/> that completes when a write operation has completed.</returns>
        protected override Task WriteCacheBytesAsync(string cacheKey, byte[] bytes)
        {
            var stopwatch = Stopwatch.StartNew();
            base.WriteCacheBytesAsync(cacheKey, bytes).GetAwaiter().GetResult();
            stopwatch.Stop();

            MemoryCacheEventSource.Log.IncrementWriteCount();
            MemoryCacheEventSource.Log.AddWriteDuration(stopwatch.ElapsedMilliseconds);
            if (bytes != null)
            {
                MemoryCacheEventSource.Log.IncrementSize(bytes.Length);
            }

            return Task.CompletedTask;
        }
    }
}
