// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web.TokenCacheProviders.InMemory;

namespace PerformanceTestService
{
    /// <summary>
    /// Adds benchmarking counters on top of <see cref="MsalMemoryTokenCacheProvider"/>.
    /// </summary>
    public class BenchmarkMsalMemoryTokenCacheProvider : MsalMemoryTokenCacheProvider
    {
        public BenchmarkMsalMemoryTokenCacheProvider(
            IMemoryCache memoryCache,
            IOptions<MsalMemoryTokenCacheOptions> cacheOptions) : base (memoryCache, cacheOptions)
        {
        }

        /// <summary>
        /// Removes a token cache identified by its key, from the serialization
        /// cache.
        /// </summary>
        /// <param name="cacheKey">token cache key.</param>
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
        /// Reads a blob from the serialization cache (identified by its key).
        /// </summary>
        /// <param name="cacheKey">Token cache key.</param>
        /// <returns>Read Bytes.</returns>
        protected override Task<byte[]> ReadCacheBytesAsync(string cacheKey)
        {
            var stopwatch = Stopwatch.StartNew();
            var bytes = base.ReadCacheBytesAsync(cacheKey).GetAwaiter().GetResult();
            stopwatch.Stop();
         
            MemoryCacheEventSource.Log.IncrementReadCount();
            MemoryCacheEventSource.Log.AddReadDuration(stopwatch.Elapsed.TotalMilliseconds);        
            if (bytes == null)
            {
                MemoryCacheEventSource.Log.IncrementReadMissCount();
            }

            return Task.FromResult(bytes);
        }

        /// <summary>
        /// Writes a token cache blob to the serialization cache (identified by its key).
        /// </summary>
        /// <param name="cacheKey">Token cache key.</param>
        /// <param name="bytes">Bytes to write.</param>
        /// <returns>A <see cref="Task"/> that completes when a write operation has completed.</returns>
        protected override Task WriteCacheBytesAsync(string cacheKey, byte[] bytes)
        {
            var stopwatch = Stopwatch.StartNew();
            base.WriteCacheBytesAsync(cacheKey, bytes).GetAwaiter().GetResult();
            stopwatch.Stop();

            MemoryCacheEventSource.Log.IncrementWriteCount();
            MemoryCacheEventSource.Log.AddWriteDuration(stopwatch.Elapsed.TotalMilliseconds);
            if (bytes != null)
            {
                MemoryCacheEventSource.Log.IncrementSize(bytes.Length);
            }

            return Task.CompletedTask;
        }
    }
}
