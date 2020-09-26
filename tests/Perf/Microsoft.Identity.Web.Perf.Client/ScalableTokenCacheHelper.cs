﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Identity.Client;

namespace Microsoft.Identity.Web.Perf.Client
{
    /// <summary>
    /// Token cache writing on disk one cache per account
    /// WARNING: this version is not encrypted
    /// </summary>
    static class ScalableTokenCacheHelper
    {
        /// <summary>
        /// Path to the token cache
        /// </summary>
        public static readonly string s_cacheFileFolder = 
            Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
            "TokenCaches");

        private static readonly Dictionary<string, byte[]> s_tokenCache = new Dictionary<string, byte[]>();
        private static readonly Dictionary<string, string> s_tokenCacheKeys = new Dictionary<string, string>();
        private static string s_emptyContent = " ";

        /// <summary>
        /// Path to the mapping between upn and home account identifier
        /// </summary>
        public static readonly string s_cacheKeysFolder = s_cacheFileFolder + "Keys";

        /// <summary>
        /// Creating the folders for the token cache and its key, if needed
        /// </summary>
        static ScalableTokenCacheHelper()
        {
            //if (!Directory.Exists(s_cacheFileFolder))
            //{
            //    Directory.CreateDirectory(s_cacheFileFolder);
            //}
            //if (!Directory.Exists(s_cacheKeysFolder))
            //{
            //    Directory.CreateDirectory(s_cacheKeysFolder);
            //}
        }

        /// <summary>
        /// Gets the mapping between a user number and its own home identifier (tid.oid)
        /// </summary>
        /// <remarks>this is encoded in the file names of the cache key folder</remarks>
        /// <returns></returns>
        public static Dictionary<int, string> GetAccountIdsByUserNumber()
        {
            int start = "MIWTestUser".Length;
            Dictionary<int, string> accountIdByUserNumber = new Dictionary<int, string>();

            //foreach(string filePath in Directory.EnumerateFiles(s_cacheKeysFolder))
            foreach(string filePath in s_tokenCacheKeys.Keys)
            {
                string fileName = Path.GetFileName(filePath);
                string[] segments = fileName.Split('-');
                string userUpn = segments[0];
                string number = userUpn.Substring(start, userUpn.IndexOf('@')-start);

                accountIdByUserNumber.Add(int.Parse(number), string.Join("-", segments.Skip(1)));
            }
            return accountIdByUserNumber;
        }


        public static void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            string cacheFilePath = GetCacheFilePath(args);
            args.TokenCache.DeserializeMsalV3(GetCacheContent(cacheFilePath));
            //args.TokenCache.DeserializeMsalV3(File.Exists(cacheFilePath)
            //        ? File.ReadAllBytes(cacheFilePath)
            //        : null);
        }

        private static byte[] GetCacheContent(string cacheFilePath)
        {
            s_tokenCache.TryGetValue(cacheFilePath, out byte[] value);
            return value;
        }

        private static void SetCacheContent(string cacheFilePath, byte[] content)
        {
            if (s_tokenCache.ContainsKey(cacheFilePath))
            {
                if (s_tokenCache[cacheFilePath] != content)
                {
                    s_tokenCache[cacheFilePath] = content;
                }
            }
            else
            {
                s_tokenCache.TryAdd(cacheFilePath, content);
            }
        }

        private static string GetCacheFilePath(TokenCacheNotificationArgs args)
        {
            // TODO
            // Here there is a bug in MSAL that sometimes we have the SuggestedCacheKey which is the
            // home account identifier, but we don't have the Account ?? (in AcquireTokenForUsernamePassword)
            // whereas we have passed-in an account
            string suggestedKey = args.SuggestedCacheKey ?? args.Account.HomeAccountId.Identifier;
            if (suggestedKey == null)
            {
                return null;
            }

            return suggestedKey;
            // return Path.Combine(s_cacheFileFolder, suggestedKey);
        }

        public static void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            // if the access operation resulted in a cache update
            if (args.HasStateChanged)
            {
                string cacheFilePath = GetCacheFilePath(args);

                // reflect changesgs in the persistent store
                SetCacheContent(cacheFilePath, args.TokenCache.SerializeMsalV3());
                //File.WriteAllBytes(cacheFilePath, args.TokenCache.SerializeMsalV3());

                WriteKey(args);
            }
        }


        /// <summary>
        /// Writes (if not already there) a file which names is the concatenation of the
        /// upn and the home account identifier. This is useful to map a user number to
        /// its home account id
        /// </summary>
        /// <param name="args"></param>
        private static void WriteKey(TokenCacheNotificationArgs args)
        {
            if (args.Account != null)
            {
                string keyPath = args.Account.Username + "-" + args.Account.HomeAccountId.Identifier;
                
                // Path.Combine(s_cacheKeysFolder, 
                // args.Account.Username + "-" + args.Account.HomeAccountId.Identifier);

                if (!s_tokenCacheKeys.ContainsKey(keyPath))
                {
                    s_tokenCacheKeys.TryAdd(keyPath, s_emptyContent);
                }
                
                //if (!File.Exists(keyPath))
                //{
                //    File.WriteAllText(keyPath, " ");
                //}
            }
        }

        internal static void EnableSerialization(ITokenCache tokenCache)
        {
            tokenCache.SetBeforeAccess(BeforeAccessNotification);
            tokenCache.SetAfterAccess(AfterAccessNotification);
        }
    }
}
