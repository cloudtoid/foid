﻿using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Net.Http.Headers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Method = System.Net.Http.HttpMethod;

namespace Cloudtoid.GatewayCore.FunctionalTests
{
    [TestClass]
    public sealed class HeaderTests
    {
        [TestMethod("Basic header plumbing test")]
        public async Task BasicPlumbingTestAsync()
        {
            var request = new HttpRequestMessage(Method.Get, "echo?message=test");
            await ExecuteAsync(
                "DefaultTestOptions.json",
                request,
                async response =>
                {
                    await EnsureResponseSucceededAsync(response);

                    var headers = response.Headers;
                    headers.Should().ContainSingle();
                    headers.Contains(HeaderNames.Via).Should().BeFalse();

                    var contentHeaders = response.Content.Headers;
                    contentHeaders.Should().HaveCount(2);
                    contentHeaders.ContentType.Should().NotBeNull();
                    contentHeaders.ContentType!.MediaType.Should().Be("text/plain");
                    contentHeaders.ContentType.CharSet.Should().Be("utf-8");
                    contentHeaders.ContentLength.Should().Be(4);
                });
        }

        [TestMethod("Should propagate W3C trace context upstream")]
        public async Task TraceContextTestAsync()
        {
            using var activity = new Activity("TraceContextTestActivity").Start();
            activity.TraceStateString = "some-state";

            var request = new HttpRequestMessage(Method.Get, "traceContext?message=test");
            await ExecuteAsync(
                "DefaultTestOptions.json",
                request,
                async response =>
                {
                    await EnsureResponseSucceededAsync(response);

                    var headers = response.Headers;

                    headers.TryGetValues(HeaderNames.TraceParent, out var _).Should().BeFalse();
                    headers.TryGetValues(HeaderNames.TraceState, out var _).Should().BeFalse();

                    headers.TryGetValues(Constants.OneValue, out var traceparent).Should().BeTrue();
                    traceparent.Should().ContainSingle().And.ContainMatch($"??-{Activity.Current!.TraceId}-*");

                    headers.TryGetValues(Constants.TwoValues, out var tracestate).Should().BeTrue();
                    tracestate.Should().BeEquivalentTo(new[] { "some-state" });
                });
        }

        [TestMethod("Should not have W3C trace context")]
        public async Task NoTraceContextTestAsync()
        {
            Activity.Current = null;

            var request = new HttpRequestMessage(Method.Get, "noTraceContext?message=test");
            await ExecuteAsync(
                "DefaultTestOptions.json",
                request,
                async response =>
                {
                    await EnsureResponseSucceededAsync(response);

                    var headers = response.Headers;
                    headers.TryGetValues(HeaderNames.TraceParent, out var _).Should().BeFalse();
                    headers.TryGetValues(HeaderNames.TraceState, out var _).Should().BeFalse();
                });
        }

        [TestMethod("Should have server header that ignores the upstream server header")]
        public async Task ServerTestAsync()
        {
            var request = new HttpRequestMessage(Method.Get, "server?message=test");
            await ExecuteAsync(
                "ServerTestOptions.json",
                request,
                async response =>
                {
                    await EnsureResponseSucceededAsync(response);

                    var headers = response.Headers;
                    headers.TryGetValues(HeaderNames.Server, out var values).Should().BeTrue();
                    values.Should().BeEquivalentTo(new[] { GatewayCore.Constants.ServerName });
                });
        }

        [TestMethod("Should not have a server header")]
        public async Task NoServerTestAsync()
        {
            var request = new HttpRequestMessage(Method.Get, "server?message=test");
            await ExecuteAsync(
                "DefaultTestOptions.json",
                request,
                async response =>
                {
                    await EnsureResponseSucceededAsync(response);

                    var headers = response.Headers;
                    headers.TryGetValues(HeaderNames.Server, out var values).Should().BeFalse();
                });
        }

        [TestMethod("Should have an external address header (x-gwcore-external-address)")]
        public async Task ExternalAddressTestAsync()
        {
            var request = new HttpRequestMessage(Method.Get, "externalAddress?message=test");
            await ExecuteAsync(
                "ExternalAddressTestOptions.json",
                request,
                response => EnsureResponseSucceededAsync(response));
        }

        [TestMethod("Should not have an external address header (x-gwcore-external-address)")]
        public async Task NoExternalAddressTestAsync()
        {
            var request = new HttpRequestMessage(Method.Get, "noExternalAddress?message=test");
            await ExecuteAsync(
                "DefaultTestOptions.json",
                request,
                response => EnsureResponseSucceededAsync(response));
        }

        [TestMethod("Should have a via header on both request and response")]
        public async Task ViaTestAsync()
        {
            var request = new HttpRequestMessage(Method.Get, "via?message=test");
            await ExecuteAsync(
                "ViaTestOptions.json",
                request,
                async response =>
                {
                    await EnsureResponseSucceededAsync(response);

                    var headers = response.Headers;
                    headers.GetValues(HeaderNames.Via).Should().ContainSingle().And.ContainMatch("?.? " + GatewayCore.Constants.ServerName);
                });
        }

        [TestMethod("Should not have a via header on both request and response")]
        public async Task NoViaTestAsync()
        {
            var request = new HttpRequestMessage(Method.Get, "noVia?message=test");
            await ExecuteAsync(
                "NoViaTestOptions.json",
                request,
                async response =>
                {
                    await EnsureResponseSucceededAsync(response);

                    var headers = response.Headers;
                    headers.Contains(HeaderNames.Via).Should().BeFalse();
                });
        }

        [TestMethod("Should have a via header on both request and response with custom proxy name")]
        public async Task ViaCustomProxyNameTestAsync()
        {
            var request = new HttpRequestMessage(Method.Get, "viaCustomProxyName?message=test");
            await ExecuteAsync(
                "ViaCustomProxyNameTestOptions.json",
                request,
                async response =>
                {
                    await EnsureResponseSucceededAsync(response);

                    var headers = response.Headers;
                    headers.GetValues(HeaderNames.Via).Should().ContainMatch("?.? custom-proxy");
                });
        }

        [TestMethod("Should have a via header with two values")]
        public async Task ViaTwoProxiesTestAsync()
        {
            var request = new HttpRequestMessage(Method.Get, "viaTwoProxies?message=test");
            request.Headers.Via.Add(new ViaHeaderValue("1.1", "first-leg"));
            await ExecuteAsync(
                "ViaTestOptions.json",
                request,
                async response =>
                {
                    await EnsureResponseSucceededAsync(response);

                    response.Headers.GetValues(HeaderNames.Via).Should()
                        .HaveCount(2)
                        .And.Contain("1.1 first-leg")
                        .And.ContainMatch("?.? " + GatewayCore.Constants.ServerName);
                });
        }

        [TestMethod("Should have a forwarded header")]
        public async Task ForwardedTestAsync()
        {
            var request = new HttpRequestMessage(Method.Get, "forwarded?message=test");
            await ExecuteAsync(
                "DefaultTestOptions.json",
                request,
                response => EnsureResponseSucceededAsync(response));
        }

        [TestMethod("Should not have forwarded or x-forwarded headers")]
        public async Task NoForwardedTestAsync()
        {
            var request = new HttpRequestMessage(Method.Get, "noForwarded?message=test");
            request.Headers.Add(Constants.Forwarded, "for=192.0.2.60;proto=http;by=203.0.113.43;host=test");
            request.Headers.Add(Constants.XForwardedFor, "some-for");
            request.Headers.Add(Constants.XForwardedHost, "some-host");
            request.Headers.Add(Constants.XForwardedProto, "some-proto");

            await ExecuteAsync(
                "NoXForwardedTestOptions.json",
                request,
                response => EnsureResponseSucceededAsync(response));

            request = new HttpRequestMessage(Method.Get, "noForwarded?message=test");
            request.Headers.Add(Constants.Forwarded, "for=192.0.2.60;proto=http;by=203.0.113.43;host=test");
            request.Headers.Add(Constants.XForwardedFor, "some-for");
            request.Headers.Add(Constants.XForwardedHost, "some-host");
            request.Headers.Add(Constants.XForwardedProto, "some-proto");

            await ExecuteAsync(
                "NoForwardedTestOptions.json",
                request,
                response => EnsureResponseSucceededAsync(response));
        }

        [TestMethod("Should have a Forwarded header that includes earlier forwarded and x-forwarded header values")]
        public async Task ForwardedMultiProxiesTestAsync()
        {
            var request = new HttpRequestMessage(Method.Get, "forwardedMultiProxies?message=test");
            request.Headers.Add(Constants.Forwarded, "for=192.0.2.60;proto=http;by=203.0.113.43;host=test, for=192.0.2.12;proto=https;by=203.0.113.43;host=test2");
            request.Headers.Add(Constants.XForwardedFor, "some-for");
            request.Headers.Add(Constants.XForwardedHost, "some-host");
            request.Headers.Add(Constants.XForwardedProto, "some-proto");

            await ExecuteAsync(
                "DefaultTestOptions.json",
                request,
                response => EnsureResponseSucceededAsync(response));
        }

        [TestMethod("Should have x-forwarded headers")]
        public async Task XForwardedTestAsync()
        {
            var request = new HttpRequestMessage(Method.Get, "xForwarded?message=test");
            await ExecuteAsync(
                "XForwardedTestOptions.json",
                request,
                response => EnsureResponseSucceededAsync(response));
        }

        [TestMethod("Should have x-forwarded headers that include earlier forwarded and x-forwarded header values")]
        public async Task XForwardedMultiProxiesTestAsync()
        {
            var request = new HttpRequestMessage(Method.Get, "xForwardedMultiProxies?message=test");
            request.Headers.Add(Constants.Forwarded, "for=192.0.2.60;proto=http;by=203.0.113.43;host-test, for=[1020:3040:5060:7080:9010:1112:1314:1516]:10;proto=https;by=203.0.113.43;host=test2");
            request.Headers.Add(Constants.XForwardedFor, "some-for");
            request.Headers.Add(Constants.XForwardedHost, "some-host");
            request.Headers.Add(Constants.XForwardedProto, "some-proto");

            await ExecuteAsync(
                "XForwardedTestOptions.json",
                request,
                response => EnsureResponseSucceededAsync(response));
        }

        [TestMethod("Should have set-cookie headers with modified values")]
        public async Task SetCookieTestAsync()
        {
            var request = new HttpRequestMessage(Method.Get, "setCookie?message=test");

            await ExecuteAsync(
                "SetCookieTestOptions.json",
                request,
                async response =>
                {
                    await EnsureResponseSucceededAsync(response);

                    var headers = response.Headers;
                    headers.GetValues(HeaderNames.SetCookie)
                        .Should()
                        .BeEquivalentTo(
                            new[]
                            {
                                "sessionId=1234; expires=Tue, 01 Jan 2030 01:01:01 GMT; domain=new.com; path=/; secure; samesite=lax; httponly",
                                "pxeId=exp12; domain=default.com; path=/; samesite=none",
                                "emptyOut=empty; path=/; samesite=lax",
                            });
                });
        }

        [TestMethod("Should append headers")]
        public async Task AppendHeadersTestAsync()
        {
            var request = new HttpRequestMessage(Method.Get, "append?message=test");
            request.Headers.Add(Constants.TwoValues, new[] { "one" });

            await ExecuteAsync(
                "AppendHeaderTestOptions.json",
                request,
                async response =>
                {
                    await EnsureResponseSucceededAsync(response);

                    var headers = response.Headers;
                    headers.GetValues(Constants.OneValue).Should().BeEquivalentTo(new[] { "one" });
                    headers.GetValues(Constants.TwoValues).Should().BeEquivalentTo(new[] { "one", "two" });
                });
        }

        [TestMethod("Should add new headers that do not exist")]
        public async Task AddOverrideTestAsync()
        {
            var request = new HttpRequestMessage(Method.Get, "addOverride?message=test");

            await ExecuteAsync(
                "AddOverridesTestOptions.json",
                request,
                async response =>
                {
                    await EnsureResponseSucceededAsync(response);

                    var headers = response.Headers;
                    headers.GetValues(Constants.OneValue).Should().BeEquivalentTo(new[] { "one" });
                    headers.GetValues(Constants.TwoValues).Should().BeEquivalentTo(new[] { "one", "two" });
                    headers.GetValues(Constants.Expression).Should().BeEquivalentTo(new[] { Environment.MachineName + "/gwcore", "m:GET" });
                });
        }

        [TestMethod("Should override existing headers")]
        public async Task UpdateOverrideTestAsync()
        {
            var request = new HttpRequestMessage(Method.Get, "updateOverride?message=test");
            request.Headers.Add(Constants.OneValue, "one");
            request.Headers.Add(Constants.TwoValues, new[] { "one", "two" });
            request.Headers.Add(Constants.Expression, new[] { "one", "two" });

            await ExecuteAsync(
                "UpdateOverridesTestOptions.json",
                request,
                async response =>
                {
                    await EnsureResponseSucceededAsync(response);

                    var headers = response.Headers;
                    headers.GetValues(Constants.OneValue).Should().BeEquivalentTo(new[] { "one-updated" });
                    headers.GetValues(Constants.TwoValues).Should().BeEquivalentTo(new[] { "one-updated", "two-updated" });
                    headers.GetValues(Constants.Expression).Should().BeEquivalentTo(new[] { Environment.MachineName + "/gwcore", "m:GET" });
                });
        }

        [TestMethod("Should not discard, empty, or headers with underscore")]
        public async Task DiscardTestAsync()
        {
            var request = new HttpRequestMessage(Method.Get, "discard?message=test");
            request.Headers.Add(Constants.OneValue, "one");
            request.Headers.Add(Constants.TwoValues, new[] { "one", "two" });
            request.Headers.Add(Constants.ThreeValues, new[] { "one", "two", "three" });
            request.Headers.Add(Constants.Underscore, "one");
            request.Headers.Add(Constants.Expression, string.Empty);

            await ExecuteAsync(
                "DiscardTestOptions.json",
                request,
                async response =>
                {
                    await EnsureResponseSucceededAsync(response);

                    var headers = response.Headers;
                    headers.Contains(Constants.OneValue).Should().BeFalse();
                    headers.Contains(Constants.TwoValues).Should().BeFalse();
                    headers.Contains(Constants.ThreeValues).Should().BeTrue();
                    headers.Contains(Constants.Underscore).Should().BeFalse();
                    headers.Contains(Constants.Expression).Should().BeFalse();
                });
        }

        [TestMethod("Should not keep inbound headers")]
        public async Task DiscardInboundTestAsync()
        {
            var request = new HttpRequestMessage(Method.Get, "discardInbound?message=test");
            request.Headers.Add(Constants.OneValue, "one");
            request.Headers.Add(Constants.TwoValues, new[] { "one", "two" });
            request.Headers.Add(Constants.ThreeValues, new[] { "one", "two", "three" });

            await ExecuteAsync(
                "DiscardInboundTestOptions.json",
                request,
                async response =>
                {
                    await EnsureResponseSucceededAsync(response);

                    var headers = response.Headers;
                    headers.Contains(Constants.OneValue).Should().BeFalse();
                    headers.Contains(Constants.TwoValues).Should().BeFalse();
                    headers.Contains(Constants.ThreeValues).Should().BeFalse();
                });
        }

        private static async Task EnsureResponseSucceededAsync(HttpResponseMessage response)
        {
            response.IsSuccessStatusCode.Should().BeTrue();
            var result = await response.Content.ReadAsStringAsync();
            result.Should().Be("test");
        }

        private static async Task ExecuteAsync(
            string config,
            HttpRequestMessage request,
            Func<HttpResponseMessage, Task> responseValidator)
        {
            await using var pipeline = new Pipeline("Tests/Header/GatewayCoreOptions/" + config);
            await pipeline.StartAsync();
            using var httpClient = pipeline.CreateGatewayCoreClient();
            using var response = await httpClient.SendAsync(request);
            await responseValidator(response);
        }
    }
}
