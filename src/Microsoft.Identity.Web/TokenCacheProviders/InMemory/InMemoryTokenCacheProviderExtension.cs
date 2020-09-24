﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Identity.Web.TokenCacheProviders.InMemory
{
    /// <summary>
    /// Extension class used to add an in-memory token cache serializer to MSAL.
    /// </summary>
    public static class InMemoryTokenCacheProviderExtension
    {
        /// <summary>Adds both the app and per-user in-memory token caches.</summary>
        /// <param name="services">The services collection to add to.</param>
        /// <param name="setupAction">TODO.</param>
        /// <returns>the services (for chaining).</returns>
        internal static IServiceCollection AddInMemoryTokenCaches(
            this IServiceCollection services,
            Action<MemoryCacheOptions>? setupAction = null)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddMemoryCache(setupAction);
            services.AddHttpContextAccessor();
            services.AddSingleton<IMsalTokenCacheProvider, MsalMemoryTokenCacheProvider>();
            return services;
        }
    }
}
