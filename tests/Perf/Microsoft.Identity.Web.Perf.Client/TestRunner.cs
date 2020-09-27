﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web.Test.Common;

namespace Microsoft.Identity.Web.Perf.Client
{
    public class TestRunner
    {
        private const string NamePrefix = "MIWTestUser";
        private readonly IConfiguration _configuration;
        private readonly string[] _userAccountIdentifiers;
        private TimeSpan elapsedTimeInMsalCacheLookup;
        private int userStartIndex;
        private int userEndIndex;

        public TestRunner(IConfiguration configuration)
        {
            _configuration = configuration;            
            userStartIndex = int.Parse(configuration["UsersStartIndex"]);
            userEndIndex = int.Parse(configuration["UsersEndIndex"]);
            _userAccountIdentifiers = new string[userEndIndex + 1];
        }

        public async Task Run()
        {
            Console.WriteLine($"Starting testing with {userEndIndex - userStartIndex} users.");

            // Try loading from cache
            ScalableTokenCacheHelper.LoadCache();
            IDictionary<int, string> accounts = ScalableTokenCacheHelper.GetAccountIdsByUserNumber();
            foreach (var account in accounts)
            {
                if (account.Key < _userAccountIdentifiers.Length)
                {
                    _userAccountIdentifiers[account.Key] = account.Value;
                }
            }

            // Configuring the http client to trust the self-signed certificate
            var httpClientHandler = new HttpClientHandler();
            httpClientHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            var client = new HttpClient(httpClientHandler);
            client.BaseAddress = new Uri(_configuration["IntegrationTestServicesBaseUri"]);

            var durationInMinutes = int.Parse(_configuration["DurationInMinutes"]);
            DateTime startOverall = DateTime.Now;
            var finishTime = DateTime.Now.AddMinutes(durationInMinutes);
            TimeSpan elapsedTime = TimeSpan.Zero;
            int requestsCounter = 0;
            int authRequestFailureCount = 0;
            int catchAllFailureCount = 0;
            int loop = 0;
            int tokenReturnedFromCache = 0;




            while (true) // DateTime.Now < finishTime)
            {
                loop++;
                //Parallel.For(userStartIndex, userEndIndex, async (i, state) =>   
                for (int i = userStartIndex; i <=userEndIndex; i++)
                {
                    bool fromCache = false;
                    try
                    {
                        HttpResponseMessage response;
                        using (HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, _configuration["TestUri"]))
                        {
                            AuthenticationResult authResult = await AcquireTokenAsync(i);
                            if (authResult == null)
                            {
                                authRequestFailureCount++;
                                // continue;
                            }
                            else
                            {
                                httpRequestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
                                httpRequestMessage.Headers.Add(
                                    "Authorization",
                                    string.Format(
                                        CultureInfo.InvariantCulture,
                                        "{0} {1}",
                                        "Bearer",
                                        authResult?.AccessToken));

                                DateTime start = DateTime.Now;
                                response = await client.SendAsync(httpRequestMessage).ConfigureAwait(false);
                                elapsedTime += DateTime.Now - start;
                                requestsCounter++;
                                if (authResult?.AuthenticationResultMetadata.TokenSource == TokenSource.Cache)
                                {
                                    tokenReturnedFromCache++;
                                    fromCache = true;
                                }
                                else
                                {
                                    if (i % 10 == 0)
                                    {
                                        ScalableTokenCacheHelper.PersistCache();
                                    }
                                }

                                Console.WriteLine($"Response received for user {i}. Loop Number {loop}. IsSuccessStatusCode: {response.IsSuccessStatusCode}. MSAL Token cache used: {fromCache}");

                                if (!response.IsSuccessStatusCode)
                                {
                                    Console.WriteLine($"Response was not successful. Status code: {response.StatusCode}. {response.ReasonPhrase}");
                                    Console.WriteLine(response.ReasonPhrase);
                                    Console.WriteLine(await response.Content.ReadAsStringAsync());
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        catchAllFailureCount++;
                        Console.WriteLine($"Exception in TestRunner at {i}/{userEndIndex - userStartIndex}: {ex.Message}");
                        Console.WriteLine($"{ex}");
                    }

                    Console.Title = $"{i} of ({userStartIndex} - {userEndIndex}), Loop: {loop}, " +
                        $"Time: {(DateTime.Now - startOverall).TotalMinutes:0.00}, " +
                        $"Cache: {tokenReturnedFromCache}: {fromCache}, Req: {requestsCounter}, " +
                        $"AuthFail: {authRequestFailureCount}, Fail: {catchAllFailureCount}";
                } //);

                ScalableTokenCacheHelper.PersistCache();

                Console.WriteLine($"Total elapse time calling the web API: {elapsedTime} ");
                Console.WriteLine($"Total number of users: {userEndIndex - userStartIndex}");
                Console.WriteLine($"Total number of AuthRequest Failures: {authRequestFailureCount}");
                Console.WriteLine($"Total number of requests: {requestsCounter} ");
                Console.WriteLine($"Average time per request: {elapsedTime.TotalSeconds / requestsCounter} ");
                Console.WriteLine($"Total number of tokens returned from the MSAL cache based on auth result: {tokenReturnedFromCache}");
                Console.WriteLine($"Time spent in MSAL cache lookup: {elapsedTimeInMsalCacheLookup} ");
                Console.WriteLine($"Average time per lookup: {elapsedTimeInMsalCacheLookup.TotalSeconds / tokenReturnedFromCache}");
                Console.WriteLine($"Start time: {startOverall}");
                Console.WriteLine($"Current time: {DateTime.Now}");

                

                if(Console.KeyAvailable)
                {
                    ConsoleKeyInfo keyInfo = Console.ReadKey();
                    if ((keyInfo.Modifiers == ConsoleModifiers.Control && (keyInfo.Key == ConsoleKey.X || keyInfo.Key == ConsoleKey.C )) || keyInfo.Key == ConsoleKey.Escape)
                    {
                        break;
                    }
                }
            }

            Console.WriteLine($"Total elapse time calling the web API: {elapsedTime} ");
            Console.WriteLine($"Total number of users: {userEndIndex - userStartIndex}");
            Console.WriteLine($"Total number of requests: {requestsCounter} ");
            Console.WriteLine($"Average time per request: {elapsedTime.TotalSeconds / requestsCounter} ");
            Console.WriteLine($"Total number of tokens returned from the MSAL cache based on auth result: {tokenReturnedFromCache}");
            Console.WriteLine($"Time spent in MSAL cache lookup: {elapsedTimeInMsalCacheLookup} ");
            Console.WriteLine($"Average time per lookup: {elapsedTimeInMsalCacheLookup.TotalSeconds / tokenReturnedFromCache}");
            Console.WriteLine($"Start time: {startOverall}");
            Console.WriteLine($"End time: {DateTime.Now}");
        }

        private async Task<AuthenticationResult> AcquireTokenAsync(int userIndex)
        {
            var scopes = new string[] { _configuration["ApiScopes"] };
            var upn = $"{NamePrefix}{userIndex}@{_configuration["TenantDomain"]}";

            var _msalPublicClient = PublicClientApplicationBuilder
                           .Create(_configuration["ClientId"])
                           .WithAuthority(TestConstants.AadInstance, TestConstants.Organizations)
                           .WithLogging(Log, LogLevel.Info, false)
                           .Build();
            ScalableTokenCacheHelper.EnableSerialization(_msalPublicClient.UserTokenCache);

            AuthenticationResult authResult = null;
            try
            {
                try
                {
                    var identifier = _userAccountIdentifiers[userIndex];
                    IAccount account = null;
                    if (identifier != null)
                    {
                        DateTime start = DateTime.Now;
                        account = await _msalPublicClient.GetAccountAsync(identifier).ConfigureAwait(false);
                        elapsedTimeInMsalCacheLookup += DateTime.Now - start;
                    }

                    authResult = await _msalPublicClient.AcquireTokenSilent(scopes, account).ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
                    return authResult;
                }
                catch (MsalUiRequiredException)
                {
                    authResult = await _msalPublicClient.AcquireTokenByUsernamePassword(
                                                        scopes,
                                                        upn,
                                                        new NetworkCredential(
                                                            upn,
                                                            _configuration["UserPassword"]).SecurePassword)
                                                        .ExecuteAsync(CancellationToken.None)
                                                        .ConfigureAwait(false);

                    _userAccountIdentifiers[userIndex] = authResult.Account.HomeAccountId.Identifier;
                    return authResult;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in AcquireTokenAsync: {ex}");
            }
            return authResult;
        }

        private static string s_msallogfile = System.Reflection.Assembly.GetExecutingAssembly().Location + ".msalLogs.txt";
        private static StringBuilder s_log = new StringBuilder();
        private static volatile bool s_isLogging = false;
        private static object s_logLock = new object();

        private static void Log(LogLevel level, string message, bool containsPii)
        {
            StringBuilder tempBuilder = new StringBuilder();
            bool writeToDisk = false;
            lock (s_logLock)
            {
                string logs = ($"{level} {message}");
                if (!s_isLogging)
                {
                    s_isLogging = true;
                    writeToDisk = true;
                    tempBuilder.Append(s_log);
                    tempBuilder.Append(logs);
                    s_log.Clear();
                }
                else
                {
                    s_log.Append(logs);
                }
            }
            
            if(!writeToDisk)
            {
                return;
            }

            s_isLogging = true;
            try
            {
                File.AppendAllText(s_msallogfile, tempBuilder.ToString());
                tempBuilder.Clear();
            }
            finally
            {
                s_isLogging = false;
            }
        }
    }
}
