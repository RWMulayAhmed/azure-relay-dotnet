﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class HybridRequestTests : HybridConnectionTestBase
    {
        [Theory, DisplayTestMethodName]
        [MemberData(nameof(AuthenticationTestPermutations))]
        async Task SmallRequestSmallResponse(EndpointTestType endpointTestType)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            HybridConnectionListener listener = null;
            try
            {
                listener = this.GetHybridConnectionListener(endpointTestType);
                RelayConnectionStringBuilder connectionString = GetConnectionStringBuilder(endpointTestType);
                Uri endpointUri = connectionString.Endpoint;

                string expectedResponse = "{ \"a\" : true }";
                HttpStatusCode expectedStatusCode = HttpStatusCode.OK;
                listener.RequestHandler = (context) =>
                {
                    TestUtility.Log("HybridConnectionListener.RequestHandler invoked with Request:");
                    TestUtility.Log($"{context.Request.HttpMethod} {context.Request.Url}");
                    context.Request.Headers.AllKeys.ToList().ForEach((k) => TestUtility.Log($"{k}: {context.Request.Headers[k]}"));
                    TestUtility.Log(StreamToString(context.Request.InputStream));

                    context.Response.StatusCode = expectedStatusCode;
                    byte[] responseBytes = Encoding.UTF8.GetBytes(expectedResponse);
                    context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                    context.Response.Close();
                };

                TestUtility.Log($"Opening {listener}");
                await listener.OpenAsync(cts.Token);

                Uri hybridHttpUri = new UriBuilder("https://", endpointUri.Host, endpointUri.Port, connectionString.EntityPath).Uri;
                using (var client = new HttpClient { BaseAddress = hybridHttpUri })
                {
                    client.DefaultRequestHeaders.ExpectContinue = false;

                    var getRequest = new HttpRequestMessage();
                    await AddAuthorizationHeader(connectionString, getRequest, hybridHttpUri);
                    getRequest.Method = HttpMethod.Get;
                    LogRequest(getRequest, client);
                    using (HttpResponseMessage response = await client.SendAsync(getRequest, cts.Token))
                    {
                        LogResponse(response);
                        Assert.Equal(expectedStatusCode, response.StatusCode);
                        Assert.Equal("OK", response.ReasonPhrase);
                        Assert.Equal(expectedResponse, await response.Content.ReadAsStringAsync());
                    }

                    var postRequest = new HttpRequestMessage();
                    await AddAuthorizationHeader(connectionString, postRequest, hybridHttpUri);
                    postRequest.Method = HttpMethod.Post;
                    string body = "{  \"a\": 11,   \"b\" :22, \"c\":\"test\",    \"d\":true}";
                    postRequest.Content = new StringContent(body);
                    postRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    LogRequest(postRequest, client);
                    using (HttpResponseMessage response = await client.SendAsync(postRequest, cts.Token))
                    {
                        LogResponse(response);
                        Assert.Equal(expectedStatusCode, response.StatusCode);
                        Assert.Equal("OK", response.ReasonPhrase);
                        Assert.Equal(expectedResponse, await response.Content.ReadAsStringAsync());
                    }
                }

                TestUtility.Log($"Closing {listener}");
                await listener.CloseAsync(cts.Token);
                listener = null;
            }
            finally
            {
                cts.Dispose();
                await this.SafeCloseAsync(listener);
            }
        }

        [Theory, DisplayTestMethodName]
        [MemberData(nameof(AuthenticationTestPermutations))]
        async Task SmallRequestLargeResponse(EndpointTestType endpointTestType)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            HybridConnectionListener listener = null;
            try
            {
                listener = this.GetHybridConnectionListener(endpointTestType);
                RelayConnectionStringBuilder connectionString = GetConnectionStringBuilder(endpointTestType);
                Uri endpointUri = connectionString.Endpoint;

                TestUtility.Log($"Opening {listener}");
                await listener.OpenAsync(cts.Token);

                string expectedResponse = new string('y', 65 * 1024);
                byte[] responseBytes = Encoding.UTF8.GetBytes(expectedResponse);
                HttpStatusCode expectedStatusCode = HttpStatusCode.OK;
                listener.RequestHandler = (context) =>
                {
                    TestUtility.Log("HybridConnectionListener.RequestHandler invoked with Request:");
                    TestUtility.Log($"{context.Request.HttpMethod} {context.Request.Url}");
                    context.Request.Headers.AllKeys.ToList().ForEach((k) => TestUtility.Log($"{k}: {context.Request.Headers[k]}"));
                    TestUtility.Log(StreamToString(context.Request.InputStream));

                    context.Response.StatusCode = expectedStatusCode;
                    context.Response.StatusDescription = "TestStatusDescription";
                    context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                    context.Response.Close();
                };

                Uri hybridHttpUri = new UriBuilder("https://", endpointUri.Host, endpointUri.Port, connectionString.EntityPath).Uri;
                using (var client = new HttpClient { BaseAddress = hybridHttpUri })
                {
                    client.DefaultRequestHeaders.ExpectContinue = false;

                    var getRequest = new HttpRequestMessage();
                    await AddAuthorizationHeader(connectionString, getRequest, hybridHttpUri);
                    getRequest.Method = HttpMethod.Get;
                    LogRequest(getRequest, client);
                    using (HttpResponseMessage response = await client.SendAsync(getRequest, cts.Token))
                    {
                        LogResponse(response, showBody: false);
                        Assert.Equal(expectedStatusCode, response.StatusCode);
                        Assert.Equal("TestStatusDescription", response.ReasonPhrase);
                        string responseContent = await response.Content.ReadAsStringAsync();
                        Assert.Equal(expectedResponse.Length, responseContent.Length);
                        Assert.Equal(expectedResponse, responseContent);
                    }

                    var postRequest = new HttpRequestMessage();
                    await AddAuthorizationHeader(connectionString, postRequest, hybridHttpUri);
                    postRequest.Method = HttpMethod.Post;
                    string body = "{  \"a\": 11,   \"b\" :22, \"c\":\"test\",    \"d\":true}";
                    postRequest.Content = new StringContent(body);
                    postRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    LogRequest(postRequest, client);
                    using (HttpResponseMessage response = await client.SendAsync(postRequest, cts.Token))
                    {
                        LogResponse(response, showBody: false);
                        Assert.Equal(expectedStatusCode, response.StatusCode);
                        Assert.Equal("TestStatusDescription", response.ReasonPhrase);
                        string responseContent = await response.Content.ReadAsStringAsync();
                        Assert.Equal(expectedResponse.Length, responseContent.Length);
                        Assert.Equal(expectedResponse, responseContent);
                    }
                }                

                TestUtility.Log($"Closing {listener}");
                await listener.CloseAsync(cts.Token);
                listener = null;
            }
            finally
            {
                cts.Dispose();
                await this.SafeCloseAsync(listener);
            }
        }

        [Theory, DisplayTestMethodName]
        [MemberData(nameof(AuthenticationTestPermutations))]
        async Task EmptyRequestEmptyResponse(EndpointTestType endpointTestType)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            HybridConnectionListener listener = this.GetHybridConnectionListener(endpointTestType);
            try
            {
                await listener.OpenAsync(cts.Token);
                listener.RequestHandler = async (context) =>
                {
                    TestUtility.Log($"RequestHandler: {context.Request.HttpMethod} {context.Request.Url}");
                    await context.Response.CloseAsync();
                };

                RelayConnectionStringBuilder connectionString = GetConnectionStringBuilder(endpointTestType);
                Uri endpointUri = connectionString.Endpoint;
                Uri hybridHttpUri = new UriBuilder("https://", endpointUri.Host, endpointUri.Port, connectionString.EntityPath).Uri;

                using (var client = new HttpClient { BaseAddress = hybridHttpUri })
                {
                    client.DefaultRequestHeaders.ExpectContinue = false;

                    var getRequest = new HttpRequestMessage();
                    getRequest.Method = HttpMethod.Get;
                    await AddAuthorizationHeader(connectionString, getRequest, hybridHttpUri);
                    LogRequest(getRequest, client);
                    using (HttpResponseMessage response = await client.SendAsync(getRequest))
                    {
                        LogResponse(response);
                        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                        Assert.Equal(0, response.Content.ReadAsStreamAsync().Result.Length);
                    }

                    var postRequest = new HttpRequestMessage();
                    postRequest.Method = HttpMethod.Post;
                    await AddAuthorizationHeader(connectionString, postRequest, hybridHttpUri);
                    LogRequest(postRequest, client);
                    using (HttpResponseMessage response = await client.SendAsync(postRequest))
                    {
                        LogResponse(response);
                        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                        Assert.Equal(0, response.Content.ReadAsStreamAsync().Result.Length);
                    }
                }

                await listener.CloseAsync(cts.Token);
            }
            finally
            {
                await SafeCloseAsync(listener);
            }
        }

        [Theory, DisplayTestMethodName]
        [MemberData(nameof(AuthenticationTestPermutations))]
        async Task LargeRequestEmptyResponse(EndpointTestType endpointTestType)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            HybridConnectionListener listener = this.GetHybridConnectionListener(endpointTestType);
            try
            {
                await listener.OpenAsync(cts.Token);
                string requestBody = null;
                listener.RequestHandler = async (context) =>
                {
                    TestUtility.Log("HybridConnectionListener.RequestHandler invoked with Request:");
                    TestUtility.Log($"{context.Request.HttpMethod} {context.Request.Url}");
                    context.Request.Headers.AllKeys.ToList().ForEach((k) => TestUtility.Log($"{k}: {context.Request.Headers[k]}"));
                    requestBody = StreamToString(context.Request.InputStream);
                    TestUtility.Log($"Body Length: {requestBody.Length}");
                    await context.Response.CloseAsync();
                };

                RelayConnectionStringBuilder connectionString = GetConnectionStringBuilder(endpointTestType);
                Uri endpointUri = connectionString.Endpoint;
                Uri hybridHttpUri = new UriBuilder("https://", endpointUri.Host, endpointUri.Port, connectionString.EntityPath).Uri;

                using (var client = new HttpClient { BaseAddress = hybridHttpUri })
                {
                    client.DefaultRequestHeaders.ExpectContinue = false;
                    var postRequest = new HttpRequestMessage();
                    await AddAuthorizationHeader(connectionString, postRequest, hybridHttpUri);
                    postRequest.Method = HttpMethod.Post;
                    var body = new string('y', 65 * 1024);
                    postRequest.Content = new StringContent(body);
                    LogRequest(postRequest, client, showBody: false);
                    using (HttpResponseMessage response = await client.SendAsync(postRequest, cts.Token))
                    {
                        LogResponse(response);
                        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                        Assert.Equal(0, response.Content.ReadAsStreamAsync().Result.Length);
                        Assert.Equal(body.Length, requestBody.Length);
                    }
                }

                await listener.CloseAsync(cts.Token);
            }
            finally
            {
                await SafeCloseAsync(listener);
            }
        }

        [Fact, DisplayTestMethodName]
        async Task LoadBalancedListeners_WebRequest()
        {
            EndpointTestType endpointTestType = EndpointTestType.Authenticated;

            // Allow more than two connections to a destination when using HttpWebRequests
            ServicePointManager.DefaultConnectionLimit = 200;

            await LoadBalancedListenersCore(
                endpointTestType,
                async (httpUri, tokenProvider, indexes, cancelToken) =>
                {
                    try
                    {
                        foreach (var index in indexes)
                        {
                            var httpRequest = (HttpWebRequest)WebRequest.Create(httpUri);
                            using (var abortRegistration = cancelToken.Register(() => httpRequest.Abort()))
                            {
                                httpRequest.Method = HttpMethod.Get.Method;
                                await AddAuthorizationHeader(tokenProvider, httpRequest.Headers, httpUri);

                                using (var httpResponse = (HttpWebResponse)(await httpRequest.GetResponseAsync()))
                                using (var responseStream = httpResponse.GetResponseStream())
                                {
                                    Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
                                }
                            }
                        }
                    }
                    catch (WebException webException)
                    {
                        HttpStatusCode? status = (webException.Response as HttpWebResponse)?.StatusCode;
                        string messageIndexes = indexes.First() + "-" + indexes.Last();
                        TestUtility.Log($"Messages {messageIndexes} Error: {status} {webException}");
                        throw;
                    }
                });
        }

        [Theory, DisplayTestMethodName]
        [MemberData(nameof(AuthenticationTestPermutations))]
        async Task LoadBalancedListeners_HttpClient(EndpointTestType endpointTestType)
        {
            var httpHandler = new HttpClientHandler() { MaxConnectionsPerServer = 200 };
            using (var client = new HttpClient(httpHandler))
            {
                client.DefaultRequestHeaders.ExpectContinue = false;

                await LoadBalancedListenersCore(
                    endpointTestType,
                    async (httpUri, tokenProvider, indexes, cancelToken) =>
                    {
                        foreach (var index in indexes)
                        {
                            using (var getRequest = new HttpRequestMessage(HttpMethod.Get, httpUri))
                            {
                                if (endpointTestType != EndpointTestType.Unauthenticated)
                                {
                                    await AddAuthorizationHeader(tokenProvider, getRequest, httpUri);
                                }

                                using (HttpResponseMessage response = await client.SendAsync(getRequest, cancelToken))
                                {
                                    Assert.True(response.IsSuccessStatusCode, "Response should have succeeded");
                                }
                            }
                        }
                    });
            }
        }

        async Task LoadBalancedListenersCore(EndpointTestType endpointTestType, Func<Uri, TokenProvider, IEnumerable<int>, CancellationToken, Task> sendBatchFunc)
        {
            HybridConnectionListener listener1 = null;
            HybridConnectionListener listener2 = null;
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(180));
            try
            {
                listener1 = this.GetHybridConnectionListener(endpointTestType);
                listener2 = this.GetHybridConnectionListener(endpointTestType);
                RelayConnectionStringBuilder connectionString = GetConnectionStringBuilder(endpointTestType);
                Uri endpointUri = connectionString.Endpoint;

                TestUtility.Log("Opening HybridConnectionListeners");
                await Task.WhenAll(
                    listener1.OpenAsync(cts.Token),
                    listener2.OpenAsync(cts.Token));

                // ParallelBatches=50; BatchSize=100; Total = 2*ParallelBatches*BatchSize; // Makes for a nice throughput test which takes 10-30 seconds
                // NOTE: Quickly creating ~16K HttpWebRequests on .NET Core can easily run out of ephemeral ports (many sockets in TIME_WAIT state).
                const int ParallelBatches = 5;
                const int BatchSize = 10;
                const int TotalRequestCount = 2 * ParallelBatches * BatchSize;

                int listenerRequestCount1 = 0;
                int listenerRequestCount2 = 0;

                listener1.RequestHandler = async (context) =>
                {
                    Interlocked.Increment(ref listenerRequestCount1);
                    context.Response.OutputStream.WriteByte(1);
                    await context.Response.CloseAsync();
                };

                listener2.RequestHandler = async (context) =>
                {
                    Interlocked.Increment(ref listenerRequestCount2);
                    context.Response.OutputStream.WriteByte(2);
                    await context.Response.CloseAsync();
                };

                Uri hybridHttpUri = new UriBuilder("https://", endpointUri.Host, endpointUri.Port, connectionString.EntityPath).Uri;
                var tokenProvider = endpointTestType == EndpointTestType.Authenticated ? listener1.TokenProvider : null;
                var stopwatch = Stopwatch.StartNew();
                await Enumerable.Range(0, TotalRequestCount).ParallelBatchAsync(
                    BatchSize,
                    ParallelBatches,
                    asyncTask: async (indexes) =>
                    {
                        // Yield the thread starting each batch to allow more parallelism
                        await Task.Yield();

                        // Verbose Tracing if troubleshooting is needed
                        //string messageIndexes = indexes.First() + "-" + indexes.Last();
                        //TestUtility.Log($"Messages {messageIndexes} starting");
                        await sendBatchFunc(hybridHttpUri, tokenProvider, indexes, cts.Token);
                        //TestUtility.Log($"Messages {messageIndexes} complete");
                    });

                stopwatch.Stop();
                int totalRequests = listenerRequestCount1 + listenerRequestCount2;
                double sendRate = totalRequests / stopwatch.Elapsed.TotalSeconds;
                TestUtility.Log($"===== Total Request Count: {totalRequests} in {stopwatch.Elapsed} ({sendRate:N2}/sec, {connectionString.EntityPath}, {TestUtility.RuntimeFramework})");
                TestUtility.Log($"Listener1 Request Count: {listenerRequestCount1}");
                TestUtility.Log($"Listener2 Request Count: {listenerRequestCount2}");

                Assert.Equal(TotalRequestCount, listenerRequestCount1 + listenerRequestCount2);
                Assert.True(listenerRequestCount1 > 0, "Listener 1 should have received some of the events.");
                Assert.True(listenerRequestCount2 > 0, "Listener 2 should have received some of the events.");

                await Task.WhenAll(
                    listener1.CloseAsync(cts.Token),
                    listener2.CloseAsync(cts.Token));
            }
            finally
            {
                TestUtility.Log("Shutting down");
                cts.Dispose();
                await Task.WhenAll(
                    SafeCloseAsync(listener1),
                    SafeCloseAsync(listener2));
            }
        }

        [Theory, DisplayTestMethodName]
        [MemberData(nameof(AuthenticationTestPermutations))]
        async Task MultiValueHeader(EndpointTestType endpointTestType)
        {
            HybridConnectionListener listener = this.GetHybridConnectionListener(endpointTestType);
            try
            {
                RelayConnectionStringBuilder connectionString = GetConnectionStringBuilder(endpointTestType);
                Uri endpointUri = connectionString.Endpoint;

                TestUtility.Log("Calling HybridConnectionListener.Open");
                await listener.OpenAsync(TimeSpan.FromSeconds(30));

                const string CustomHeaderName = "X-CustomHeader";
                string requestHeaderValue = string.Empty;
                listener.RequestHandler = (context) =>
                {
                    TestUtility.Log("HybridConnectionListener.RequestHandler invoked with Request:");
                    TestUtility.Log($"{context.Request.HttpMethod} {context.Request.Url}");
                    context.Request.Headers.AllKeys.ToList().ForEach((k) => TestUtility.Log($"{k}: {context.Request.Headers[k]}"));
                    TestUtility.Log(StreamToString(context.Request.InputStream));
                    requestHeaderValue = context.Request.Headers[CustomHeaderName];

                    context.Response.Headers.Add(CustomHeaderName, "responseValue1");
                    context.Response.Headers.Add(CustomHeaderName, "responseValue2");
                    context.Response.OutputStream.Close();
                };

                Uri hybridHttpUri = new UriBuilder("https://", endpointUri.Host, endpointUri.Port, connectionString.EntityPath).Uri;
                using (var client = new HttpClient { BaseAddress = hybridHttpUri })
                {
                    client.DefaultRequestHeaders.ExpectContinue = false;

                    var getRequest = new HttpRequestMessage();
                    getRequest.Method = HttpMethod.Get;
                    await AddAuthorizationHeader(connectionString, getRequest, hybridHttpUri);
                    getRequest.Headers.Add(CustomHeaderName, "requestValue1");
                    getRequest.Headers.Add(CustomHeaderName, "requestValue2");

                    LogRequest(getRequest, client);
                    using (HttpResponseMessage response = await client.SendAsync(getRequest))
                    {
                        LogResponse(response);
                        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                        Assert.Equal("requestValue1, requestValue2", requestHeaderValue);
                        string[] responseHeaders = string.Join(",", response.Headers.GetValues(CustomHeaderName)).Split(new[] { ',' });
                        for (int i = 0; i < responseHeaders.Length; i++)
                        {
                            responseHeaders[i] = responseHeaders[i].Trim(' ', '\t');
                        }

                        Assert.Equal(new [] {"responseValue1", "responseValue2" }, responseHeaders);
                    }
                }

                await listener.CloseAsync(TimeSpan.FromSeconds(10));
            }
            finally
            {
                await SafeCloseAsync(listener);
            }
        }

        [Theory, DisplayTestMethodName]
        [MemberData(nameof(AuthenticationTestPermutations))]
        async Task QueryString(EndpointTestType endpointTestType)
        {
            HybridConnectionListener listener = this.GetHybridConnectionListener(endpointTestType);
            try
            {
                RelayConnectionStringBuilder connectionString = GetConnectionStringBuilder(endpointTestType);
                Uri endpointUri = connectionString.Endpoint;

                TestUtility.Log("Calling HybridConnectionListener.Open");
                await listener.OpenAsync(TimeSpan.FromSeconds(30));

                var queryStringTests = new[]
                {
                    new { Original = "?foo=bar", Output = "?foo=bar" },
                    new { Original = "?sb-hc-id=1", Output = "" },
                    new { Original = "?sb-hc-id=1&custom=value", Output = "?custom=value" },
                    new { Original = "?custom=value&sb-hc-id=1", Output = "?custom=value" },
                    new { Original = "?sb-hc-undefined=true", Output = "" },                    
                    new { Original = "? Key =  Value With Space ", Output = "?+Key+=++Value+With+Space" }, // HttpUtility.ParseQueryString.ToString() in the cloud service changes this
                    new { Original = "?key='value'", Output = "?key=%27value%27" }, // HttpUtility.ParseQueryString.ToString() in the cloud service changes this
                    new { Original = "?key=\"value\"", Output = "?key=%22value%22" }, // HttpUtility.ParseQueryString.ToString() in the cloud service changes this
                    new { Original = "?key=<value>", Output = "?key=%3cvalue%3e" },
#if PENDING_SERVICE_UPDATE
                    new { Original = "?foo=bar&", Output = "?foo=bar&" },
                    new { Original = "?&foo=bar", Output = "?foo=bar" }, // HttpUtility.ParseQueryString.ToString() in the cloud service changes this
                    new { Original = "?sb-hc-undefined", Output = "?sb-hc-undefined" },
                    new { Original = "?CustomValue", Output = "?CustomValue" },
                    new { Original = "?custom-Value", Output = "?custom-Value" },
                    new { Original = "?custom&value", Output = "?custom&value" },
                    new { Original = "?&custom&value&", Output = "?&custom&value&" },
                    new { Original = "?&&value&&", Output = "?&&value&&" },
                    new { Original = "?+", Output = "?+" },
                    new { Original = "?%20", Output = "?+" }, // HttpUtility.ParseQueryString.ToString() in the cloud service changes this
                    new { Original = "? Not a key value pair ", Output = "?+Not+a+key+value+pair" }, // HttpUtility.ParseQueryString.ToString() in the cloud service changes this
                    new { Original = "?&+&", Output = "?&+&" },
                    new { Original = "?%2f%3a%3d%26", Output = "?%2f%3a%3d%26" },
#endif
                };

                RelayedHttpListenerContext actualRequestContext = null;
                listener.RequestHandler = async (context) =>
                {
                    TestUtility.Log($"RequestHandler {context.Request.HttpMethod} {context.Request.Url}");
                    actualRequestContext = context;
                    await context.Response.CloseAsync();
                };

                foreach (var queryStringTest in queryStringTests)
                {
                    string originalQueryString = queryStringTest.Original;
                    string expectedQueryString = queryStringTest.Output;
                    actualRequestContext = null;

                    TestUtility.Log($"Testing Query String '{originalQueryString}'");
                    Uri hybridHttpUri = new UriBuilder("https://", endpointUri.Host, endpointUri.Port, connectionString.EntityPath, originalQueryString).Uri;
                    using (var client = new HttpClient { BaseAddress = hybridHttpUri })
                    {
                        client.DefaultRequestHeaders.ExpectContinue = false;

                        var getRequest = new HttpRequestMessage();
                        getRequest.Method = HttpMethod.Get;
                        await AddAuthorizationHeader(connectionString, getRequest, hybridHttpUri);
                        LogRequestLine(getRequest, client);
                        using (HttpResponseMessage response = await client.SendAsync(getRequest))
                        {
                            LogResponseLine(response);
                            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                            Assert.Equal(expectedQueryString, actualRequestContext.Request.Url.Query);
                        }
                    }
                }

                await listener.CloseAsync(TimeSpan.FromSeconds(10));
            }
            finally
            {
                await SafeCloseAsync(listener);
            }
        }

        [Theory, DisplayTestMethodName]
        [MemberData(nameof(AuthenticationTestPermutations))]
        async Task RequestHandlerErrors(EndpointTestType endpointTestType)
        {
            HybridConnectionListener listener = this.GetHybridConnectionListener(endpointTestType);
            try
            {
                RelayConnectionStringBuilder connectionString = GetConnectionStringBuilder(endpointTestType);
                Uri endpointUri = connectionString.Endpoint;
                await listener.OpenAsync(TimeSpan.FromSeconds(30));

                Uri hybridHttpUri = new UriBuilder("https://", endpointUri.Host, endpointUri.Port, connectionString.EntityPath).Uri;
                using (var client = new HttpClient { BaseAddress = hybridHttpUri })
                {
                    client.DefaultRequestHeaders.ExpectContinue = false;

                    var request = new HttpRequestMessage();
                    request.Method = HttpMethod.Get;
                    await AddAuthorizationHeader(connectionString, request, hybridHttpUri);
                    LogRequestLine(request, client);
                    using (HttpResponseMessage response = await client.SendAsync(request))
                    {
                        LogResponse(response);
                        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
                        Assert.Contains("RequestHandler has not been configured", response.ReasonPhrase);
                        Assert.Contains("TrackingId", response.ReasonPhrase);
                        Assert.Contains(hybridHttpUri.Host, response.ReasonPhrase);
                        Assert.Contains(connectionString.EntityPath, response.ReasonPhrase);
                        string viaHeader = response.Headers.Via.ToString();
                        Assert.Contains(hybridHttpUri.Host, viaHeader);
                    }

                    string userExceptionMessage = "User Error";
                    listener.RequestHandler = (context) =>
                    {
                        throw new InvalidOperationException(userExceptionMessage);
                    };

                    request = new HttpRequestMessage();
                    request.Method = HttpMethod.Get;
                    await AddAuthorizationHeader(connectionString, request, hybridHttpUri);
                    LogRequestLine(request, client);
                    using (HttpResponseMessage response = await client.SendAsync(request))
                    {
                        LogResponse(response);
                        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
                        Assert.Contains("RequestHandler", response.ReasonPhrase);
                        Assert.Contains("exception", response.ReasonPhrase, StringComparison.OrdinalIgnoreCase);
                        Assert.Contains("TrackingId", response.ReasonPhrase);
                        Assert.Contains(hybridHttpUri.Host, response.ReasonPhrase);
                        Assert.Contains(connectionString.EntityPath, response.ReasonPhrase);
                        string viaHeader = response.Headers.Via.ToString();
                        Assert.Contains(hybridHttpUri.Host, viaHeader);

                        // Details of the Exception in the Listener must not be sent
                        Assert.DoesNotContain("InvalidOperationException", response.ReasonPhrase);
                        Assert.DoesNotContain(userExceptionMessage, response.ReasonPhrase);
                    }
                }

                await listener.CloseAsync(TimeSpan.FromSeconds(10));
            }
            finally
            {
                await SafeCloseAsync(listener);
            }
        }

        [Theory, DisplayTestMethodName]
        [MemberData(nameof(AuthenticationTestPermutations))]
        async Task ResponseHeadersAfterBody(EndpointTestType endpointTestType)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var listener = this.GetHybridConnectionListener(endpointTestType);
            try
            {
                await listener.OpenAsync(cts.Token);

                var requestHandlerComplete = new TaskCompletionSource<object>();
                listener.RequestHandler = (context) =>
                {
                    TestUtility.Log($"RequestHandler: {context.Request.HttpMethod} {context.Request.Url}");
                    try
                    {
                        // Begin writing to the output stream
                        context.Response.OutputStream.WriteByte((byte)'X');

                        // Now try to change some things which are no longer mutable
                        var exception = Assert.Throws<InvalidOperationException>(() => context.Response.StatusCode = HttpStatusCode.Found);
                        Assert.Contains("TrackingId", exception.Message, StringComparison.OrdinalIgnoreCase);
                        exception = Assert.Throws<InvalidOperationException>(() => context.Response.StatusDescription = "Test Description");
                        Assert.Contains("TrackingId", exception.Message, StringComparison.OrdinalIgnoreCase);
                        exception = Assert.Throws<InvalidOperationException>(() => context.Response.Headers["CustomHeader"] = "Header value");
                        Assert.Contains("TrackingId", exception.Message, StringComparison.OrdinalIgnoreCase);

                        context.Response.OutputStream.Close();
                        requestHandlerComplete.TrySetResult(null);
                    }
                    catch (Exception e)
                    {
                        requestHandlerComplete.TrySetException(e);
                    }
                };

                RelayConnectionStringBuilder connectionString = GetConnectionStringBuilder(endpointTestType);
                Uri endpointUri = connectionString.Endpoint;
                Uri hybridHttpUri = new UriBuilder("https://", endpointUri.Host, endpointUri.Port, connectionString.EntityPath).Uri;

                using (var client = new HttpClient { BaseAddress = hybridHttpUri })
                {
                    client.DefaultRequestHeaders.ExpectContinue = false;

                    var getRequest = new HttpRequestMessage();
                    await AddAuthorizationHeader(connectionString, getRequest, hybridHttpUri);
                    LogRequest(getRequest, client);
                    var sendTask = client.SendAsync(getRequest);

                    // This will throw if anything failed in the RequestHandler
                    await requestHandlerComplete.Task;

                    using (HttpResponseMessage response = await sendTask)
                    {
                        LogResponse(response);
                        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                        Assert.Equal(1, (await response.Content.ReadAsStreamAsync()).Length);
                    }
                }

                await listener.CloseAsync(cts.Token);
            }
            finally
            {
                await SafeCloseAsync(listener);
            }
        }

        [Fact, DisplayTestMethodName]
        async Task StatusCodes()
        {
            var endpointTestType = EndpointTestType.Authenticated;
            HybridConnectionListener listener = this.GetHybridConnectionListener(endpointTestType);
            try
            {
                RelayConnectionStringBuilder connectionString = GetConnectionStringBuilder(endpointTestType);
                Uri endpointUri = connectionString.Endpoint;

                TestUtility.Log("Calling HybridConnectionListener.Open");
                await listener.OpenAsync(TimeSpan.FromSeconds(30));

                var httpHandler = new HttpClientHandler { AllowAutoRedirect = false };
                Uri hybridHttpUri = new UriBuilder("https://", endpointUri.Host, endpointUri.Port, connectionString.EntityPath).Uri;
                using (var client = new HttpClient(httpHandler) { BaseAddress = hybridHttpUri })
                {
                    client.DefaultRequestHeaders.ExpectContinue = false;

                    HttpStatusCode[] expectedStatusCodes = new HttpStatusCode[]
                    {
                        HttpStatusCode.Accepted, HttpStatusCode.Ambiguous, HttpStatusCode.BadGateway, HttpStatusCode.BadRequest, HttpStatusCode.Conflict,
                        /*HttpStatusCode.Continue,*/ HttpStatusCode.Created, HttpStatusCode.ExpectationFailed, HttpStatusCode.Forbidden,
                        HttpStatusCode.GatewayTimeout, HttpStatusCode.Gone, HttpStatusCode.HttpVersionNotSupported, HttpStatusCode.InternalServerError,
                        HttpStatusCode.LengthRequired, HttpStatusCode.MethodNotAllowed, HttpStatusCode.MovedPermanently, HttpStatusCode.MultipleChoices,
                        HttpStatusCode.NoContent, HttpStatusCode.NonAuthoritativeInformation, HttpStatusCode.NotAcceptable, HttpStatusCode.NotFound,
                        HttpStatusCode.NotImplemented, HttpStatusCode.NotModified, HttpStatusCode.PartialContent, HttpStatusCode.PaymentRequired,
                        HttpStatusCode.PreconditionFailed, HttpStatusCode.ProxyAuthenticationRequired, HttpStatusCode.Redirect, HttpStatusCode.TemporaryRedirect,
                        HttpStatusCode.RedirectMethod, HttpStatusCode.RequestedRangeNotSatisfiable, HttpStatusCode.RequestEntityTooLarge, HttpStatusCode.RequestTimeout,
                        HttpStatusCode.RequestUriTooLong, HttpStatusCode.ResetContent, HttpStatusCode.ServiceUnavailable,
                        /*HttpStatusCode.SwitchingProtocols,*/ HttpStatusCode.Unauthorized, HttpStatusCode.UnsupportedMediaType,
                        HttpStatusCode.Unused, HttpStatusCode.UpgradeRequired, HttpStatusCode.UseProxy, (HttpStatusCode)418, (HttpStatusCode)450
                    };

                    foreach (HttpStatusCode expectedStatusCode in expectedStatusCodes)
                    {
                        TestUtility.Log($"Testing HTTP Status Code: {(int)expectedStatusCode} {expectedStatusCode}");
                        listener.RequestHandler = (context) =>
                        {
                            TestUtility.Log($"RequestHandler: {context.Request.HttpMethod} {context.Request.Url}");
                            context.Response.StatusCode = expectedStatusCode;
                            context.Response.Close();
                        };

                        var getRequest = new HttpRequestMessage();
                        getRequest.Method = HttpMethod.Get;
                        await AddAuthorizationHeader(connectionString, getRequest, hybridHttpUri);
                        LogRequestLine(getRequest, client);
                        using (HttpResponseMessage response = await client.SendAsync(getRequest))
                        {
                            TestUtility.Log($"Response: HTTP/{response.Version} {(int)response.StatusCode} {response.ReasonPhrase}");
                            Assert.Equal(expectedStatusCode, response.StatusCode);
                        }

                        var postRequest = new HttpRequestMessage();
                        postRequest.Method = HttpMethod.Post;
                        await AddAuthorizationHeader(connectionString, postRequest, hybridHttpUri);
                        string body = "{  \"a\": 11,   \"b\" :22, \"c\":\"test\",    \"d\":true}";
                        postRequest.Content = new StringContent(body);
                        postRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                        LogRequestLine(postRequest, client);
                        using (HttpResponseMessage response = await client.SendAsync(postRequest))
                        {
                            TestUtility.Log($"Response: HTTP/{response.Version} {(int)response.StatusCode} {response.ReasonPhrase}");
                            Assert.Equal(expectedStatusCode, response.StatusCode);
                        }
                    }
                }

                await listener.CloseAsync(TimeSpan.FromSeconds(10));
            }
            finally
            {
                await SafeCloseAsync(listener);
            }
        }

        [Fact, DisplayTestMethodName]
        async Task Verbs()
        {
            var endpointTestType = EndpointTestType.Authenticated;
            HybridConnectionListener listener = this.GetHybridConnectionListener(endpointTestType);
            try
            {
                RelayConnectionStringBuilder connectionString = GetConnectionStringBuilder(endpointTestType);
                Uri endpointUri = connectionString.Endpoint;

                TestUtility.Log("Calling HybridConnectionListener.Open");
                await listener.OpenAsync(TimeSpan.FromSeconds(30));

                Uri hybridHttpUri = new UriBuilder("https://", endpointUri.Host, endpointUri.Port, connectionString.EntityPath).Uri;
                using (var client = new HttpClient { BaseAddress = hybridHttpUri })
                {
                    client.DefaultRequestHeaders.ExpectContinue = false;

                    HttpMethod[] methods = new HttpMethod[]
                    {
                        HttpMethod.Delete, HttpMethod.Get, HttpMethod.Head, HttpMethod.Options, HttpMethod.Post, HttpMethod.Put, HttpMethod.Trace
                    };

                    foreach (HttpMethod method in methods)
                    {
                        TestUtility.Log($"Testing HTTP Verb: {method}");
                        string actualMethod = string.Empty;
                        listener.RequestHandler = (context) =>
                        {
                            TestUtility.Log($"RequestHandler: {context.Request.HttpMethod} {context.Request.Url}");
                            actualMethod = context.Request.HttpMethod;
                            context.Response.Close();
                        };

                        var request = new HttpRequestMessage();
                        await AddAuthorizationHeader(connectionString, request, hybridHttpUri);
                        request.Method = method;
                        LogRequestLine(request, client);
                        using (HttpResponseMessage response = await client.SendAsync(request))
                        {
                            TestUtility.Log($"Response: HTTP/{response.Version} {(int)response.StatusCode} {response.ReasonPhrase}");
                            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                            Assert.Equal(method.Method, actualMethod);
                        }
                    }
                }

                await listener.CloseAsync(TimeSpan.FromSeconds(10));
            }
            finally
            {
                await SafeCloseAsync(listener);
            }
        }

        static Task AddAuthorizationHeader(RelayConnectionStringBuilder connectionString, HttpRequestMessage request, Uri resource)
        {
            var tokenProvider = CreateTokenProvider(connectionString);
            return AddAuthorizationHeader(tokenProvider, request, resource);
        }

        static Task AddAuthorizationHeader(TokenProvider tokenProvider, HttpRequestMessage request, Uri resource)
        {
            return AddAuthorizationHeader(tokenProvider, resource, (req, t) => req.Headers.Add("ServiceBusAuthorization", t), request);
        }

        static Task AddAuthorizationHeader(TokenProvider tokenProvider, WebHeaderCollection headers, Uri resource)
        {
            return AddAuthorizationHeader(tokenProvider, resource, (h, t) => h.Add("ServiceBusAuthorization", t), headers);
        }

        static async Task AddAuthorizationHeader<T>(TokenProvider tokenProvider, Uri resource, Action<T, string> addHeader, T value)
        {
            if (tokenProvider != null)
            {
                var token = await tokenProvider.GetTokenAsync(resource.AbsoluteUri, TimeSpan.FromMinutes(20));
                addHeader(value, token.TokenString);
            }
        }

        static TokenProvider CreateTokenProvider(RelayConnectionStringBuilder connectionString)
        {
            TokenProvider tokenProvider = null;
            if (!string.IsNullOrEmpty(connectionString.SharedAccessKeyName) && !string.IsNullOrEmpty(connectionString.SharedAccessKey))
            {
                tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(connectionString.SharedAccessKeyName, connectionString.SharedAccessKey);
            }
            else if (!string.IsNullOrEmpty(connectionString.SharedAccessSignature))
            {
                tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(connectionString.SharedAccessSignature);
            }

            return tokenProvider;
        }
    }
}
