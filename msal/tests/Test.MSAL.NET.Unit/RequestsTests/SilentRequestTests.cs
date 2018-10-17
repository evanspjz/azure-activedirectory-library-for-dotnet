﻿// ------------------------------------------------------------------------------
//
// Copyright (c) Microsoft Corporation.
// All rights reserved.
//
// This code is licensed under the MIT License.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files(the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions :
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
// ------------------------------------------------------------------------------

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Internal;
using Microsoft.Identity.Client.Internal.Requests;
using Microsoft.Identity.Core;
using Microsoft.Identity.Core.Helpers;
using Microsoft.Identity.Core.Instance;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Test.Microsoft.Identity.Core.Unit;
using Test.Microsoft.Identity.Core.Unit.Mocks;

namespace Test.MSAL.NET.Unit.RequestsTests
{
    [TestClass]
    public class SilentRequestTests
    {
        private TokenCache _cache;

        [TestInitialize]
        public void TestInitialize()
        {
            RequestTestsCommon.InitializeRequestTests();
            _cache = new TokenCache();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (_cache != null)
            {
                _cache.tokenCacheAccessor.ClearAccessTokens();
                _cache.tokenCacheAccessor.ClearRefreshTokens();
            }
        }

        [TestMethod]
        [TestCategory("SilentRequestTests")]
        public void ConstructorTests()
        {
            using (var httpManager = new MockHttpManager())
            {
                var authority = Authority.CreateAuthority(TestConstants.AuthorityHomeTenant, false);
                var cache = new TokenCache()
                {
                    ClientId = TestConstants.ClientId
                };
                var parameters = new AuthenticationRequestParameters()
                {
                    Authority = authority,
                    ClientId = TestConstants.ClientId,
                    Scope = TestConstants.Scope,
                    TokenCache = cache,
                    Account = new Account(TestConstants.UserIdentifier, TestConstants.DisplayableId, null),
                    RequestContext = new RequestContext(new MsalLogger(Guid.NewGuid(), null))
                };

                var crypto = PlatformProxyFactory.GetPlatformProxy().CryptographyManager;

                var request = new SilentRequest(httpManager, crypto, parameters, false);
                Assert.IsNotNull(request);

                parameters.Account = new Account(TestConstants.UserIdentifier, TestConstants.DisplayableId, null);

                request = new SilentRequest(httpManager, crypto, parameters, false);
                Assert.IsNotNull(request);

                request = new SilentRequest(httpManager, crypto, parameters, false);
                Assert.IsNotNull(request);
            }
        }

        [TestMethod]
        [TestCategory("SilentRequestTests")]
        public void ExpiredTokenRefreshFlowTest()
        {
            using (var httpManager = new MockHttpManager())
            {
                var authority = Authority.CreateAuthority(TestConstants.AuthorityHomeTenant, false);
                var cache = new TokenCache()
                {
                    ClientId = TestConstants.ClientId,
                    HttpManager = httpManager
                };
                TokenCacheHelper.PopulateCache(cache.tokenCacheAccessor);

                var parameters = new AuthenticationRequestParameters()
                {
                    Authority = authority,
                    ClientId = TestConstants.ClientId,
                    Scope = ScopeHelper.CreateSortedSetFromEnumerable(
                        new[]
                        {
                            "some-scope1",
                            "some-scope2"
                        }),
                    TokenCache = cache,
                    RequestContext = new RequestContext(new MsalLogger(Guid.Empty, null)),
                    Account = new Account(TestConstants.UserIdentifier, TestConstants.DisplayableId, null)
                };

                RequestTestsCommon.MockInstanceDiscoveryAndOpenIdRequest(httpManager);

                httpManager.AddSuccessTokenResponseMockHandlerForPost();

                var crypto = PlatformProxyFactory.GetPlatformProxy().CryptographyManager;

                var request = new SilentRequest(httpManager, crypto, parameters, false);
                Task<AuthenticationResult> task = request.RunAsync(CancellationToken.None);
                var result = task.Result;
                Assert.IsNotNull(result);
                Assert.AreEqual("some-access-token", result.AccessToken);
                Assert.AreEqual("some-scope1 some-scope2", result.Scopes.AsSingleString());
            }
        }

        [TestMethod]
        [TestCategory("SilentRequestTests")]
        public void SilentRefreshFailedNullCacheTest()
        {
            using (var httpManager = new MockHttpManager())
            {
                var authority = Authority.CreateAuthority(TestConstants.AuthorityHomeTenant, false);
                _cache = null;

                var parameters = new AuthenticationRequestParameters()
                {
                    Authority = authority,
                    ClientId = TestConstants.ClientId,
                    Scope = ScopeHelper.CreateSortedSetFromEnumerable(
                        new[]
                        {
                            "some-scope1",
                            "some-scope2"
                        }),
                    TokenCache = _cache,
                    Account = new Account(TestConstants.UserIdentifier, TestConstants.DisplayableId, null),
                    RequestContext = new RequestContext(new MsalLogger(Guid.NewGuid(), null))
                };

                var crypto = PlatformProxyFactory.GetPlatformProxy().CryptographyManager;

                try
                {
                    var request = new SilentRequest(httpManager, crypto, parameters, false);
                    Task<AuthenticationResult> task = request.RunAsync(CancellationToken.None);
                    var authenticationResult = task.Result;
                    Assert.Fail("MsalUiRequiredException should be thrown here");
                }
                catch (AggregateException ae)
                {
                    var exc = ae.InnerException as MsalUiRequiredException;
                    Assert.IsNotNull(exc);
                    Assert.AreEqual(MsalUiRequiredException.TokenCacheNullError, exc.ErrorCode);
                }
            }
        }

        [TestMethod]
        [TestCategory("SilentRequestTests")]
        public void SilentRefreshFailedNoCacheItemFoundTest()
        {
            using (var httpManager = new MockHttpManager())
            {
                var authority = Authority.CreateAuthority(TestConstants.AuthorityHomeTenant, false);
                _cache = new TokenCache()
                {
                    ClientId = TestConstants.ClientId,
                    HttpManager = httpManager
                };

                httpManager.AddInstanceDiscoveryMockHandler();

                var parameters = new AuthenticationRequestParameters()
                {
                    Authority = authority,
                    ClientId = TestConstants.ClientId,
                    Scope = ScopeHelper.CreateSortedSetFromEnumerable(
                        new[]
                        {
                            "some-scope1",
                            "some-scope2"
                        }),
                    TokenCache = _cache,
                    Account = new Account(TestConstants.UserIdentifier, TestConstants.DisplayableId, null),
                    RequestContext = new RequestContext(new MsalLogger(Guid.NewGuid(), null))
                };

                var crypto = PlatformProxyFactory.GetPlatformProxy().CryptographyManager;

                try
                {
                    var request = new SilentRequest(httpManager, crypto, parameters, false);
                    Task<AuthenticationResult> task = request.RunAsync(CancellationToken.None);
                    var authenticationResult = task.Result;
                    Assert.Fail("MsalUiRequiredException should be thrown here");
                }
                catch (AggregateException ae)
                {
                    var exc = ae.InnerException as MsalUiRequiredException;
                    Assert.IsNotNull(exc);
                    Assert.AreEqual(MsalUiRequiredException.NoTokensFoundError, exc.ErrorCode);
                }
            }
        }
    }
}