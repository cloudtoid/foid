﻿namespace Cloudtoid.GatewayCore.FunctionalTests
{
    using System.Linq;
    using FluentAssertions;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Net.Http.Headers;

    [ApiController]
    [Route("[controller]")]
    public class UpstreamController : ControllerBase
    {
        [HttpGet("echo")]
        public string Echo(string message) => message;

        [HttpGet("trace")]
        public string TraceTest(string message)
        {
            HttpContext.Request.Headers.TryGetValue(Constants.CorrelationId, out var values).Should().BeTrue();
            values.Should().HaveCount(1);

            HttpContext.Request.Headers.TryGetValue(Constants.CallId, out values).Should().BeTrue();
            values.Should().HaveCount(1);

            return message;
        }

        [HttpGet("noTrace")]
        public string NoTraceTest(string message)
        {
            HttpContext.Request.Headers.ContainsKey(Constants.CorrelationId).Should().BeFalse();
            HttpContext.Request.Headers.ContainsKey(Constants.CallId).Should().BeFalse();
            return message;
        }

        [HttpGet("customCorrelationId")]
        public string CustomCorrelationIdTest(string message)
        {
            HttpContext.Request.Headers.TryGetValue("x-c-custom", out var values).Should().BeTrue();
            values.Should().HaveCount(1);
            return message;
        }

        [HttpGet("originalCorrelationId")]
        public string OriginalCorrelationIdTest(string message)
        {
            HttpContext.Request.Headers.TryGetValue(Constants.CorrelationId, out var values).Should().BeTrue();
            values.Should().BeEquivalentTo(new[] { "corr-id-1" });

            return message;
        }

        [HttpGet("callId")]
        public string CallIdTest(string message)
        {
            HttpContext.Request.Headers.TryGetValue(Constants.CallId, out var values).Should().BeTrue();
            var callId = values.Single();
            callId.Should().NotBe("call-id-1");

            HttpContext.Response.Headers.Add("x-proxy-call-id", callId);
            HttpContext.Response.Headers.Add(Constants.CallId, "call-id-2");
            return message;
        }

        [HttpGet("via")]
        public string ViaTest(string message)
        {
            HttpContext.Request.Headers.TryGetValue(HeaderNames.Via, out var values).Should().BeTrue();
            values.Should().BeEquivalentTo(new[] { "1.1 gwcore" });
            return message;
        }

        [HttpGet("noVia")]
        public string NoViaTest(string message)
        {
            HttpContext.Request.Headers.ContainsKey(HeaderNames.Via).Should().BeFalse();
            return message;
        }

        [HttpGet("viaCustomProxyName")]
        public string ViaCustomProxyTest(string message)
        {
            HttpContext.Request.Headers.TryGetValue(HeaderNames.Via, out var values).Should().BeTrue();
            values.Should().BeEquivalentTo(new[] { "1.1 custom-proxy" });
            return message;
        }

        [HttpGet("viaTwoProxies")]
        public string ViaTwoProxiesTest(string message)
        {
            var values = HttpContext.Request.Headers.GetCommaSeparatedValues(HeaderNames.Via);
            values.Should().BeEquivalentTo(new[] { "2.0 first-leg", "1.1 gwcore" });

            HttpContext.Response.Headers.Add(HeaderNames.Via, "2.0 first-leg");
            return message;
        }

        [HttpGet("forwarded")]
        public string ForwardedTest(string message)
        {
            var values = HttpContext.Request.Headers.GetCommaSeparatedValues(Constants.Forwarded);
            var forwarded = RemovePortFromHostInForwarded(values.Single());
            forwarded.Should().Be("by=\"[::1]\";for=\"[::1]\";host=localhost;proto=http");

            HttpContext.Request.Headers.ContainsKey(Constants.XForwardedFor).Should().BeFalse();
            HttpContext.Request.Headers.ContainsKey(Constants.XForwardedHost).Should().BeFalse();
            HttpContext.Request.Headers.ContainsKey(Constants.XForwardedProto).Should().BeFalse();

            return message;
        }

        [HttpGet("noForwarded")]
        public string NoForwardedTest(string message)
        {
            HttpContext.Request.Headers.ContainsKey(Constants.Forwarded).Should().BeFalse();
            HttpContext.Request.Headers.ContainsKey(Constants.XForwardedFor).Should().BeFalse();
            HttpContext.Request.Headers.ContainsKey(Constants.XForwardedHost).Should().BeFalse();
            HttpContext.Request.Headers.ContainsKey(Constants.XForwardedProto).Should().BeFalse();

            return message;
        }

        [HttpGet("forwardedMultiProxies")]
        public string ForwardedMultiProxiesTest(string message)
        {
            var values = HttpContext.Request.Headers.GetCommaSeparatedValues(Constants.Forwarded);
            values.Should().HaveCount(4);
            values[0].Should().Be("for=some-for;host=some-host;proto=some-proto");
            values[1].Should().Be("by=203.0.113.43;for=192.0.2.60;host=test;proto=http");
            values[2].Should().Be("by=203.0.113.43;for=192.0.2.12;host=test2;proto=https");
            RemovePortFromHostInForwarded(values[3]).Should().Be("by=\"[::1]\";for=\"[::1]\";host=localhost;proto=http");

            HttpContext.Request.Headers.ContainsKey(Constants.XForwardedFor).Should().BeFalse();
            HttpContext.Request.Headers.ContainsKey(Constants.XForwardedHost).Should().BeFalse();
            HttpContext.Request.Headers.ContainsKey(Constants.XForwardedProto).Should().BeFalse();

            return message;
        }

        [HttpGet("xForwarded")]
        public string XForwardedTest(string message)
        {
            HttpContext.Request.Headers.TryGetValue(Constants.XForwardedFor, out var values).Should().BeTrue();
            values.Single().Should().Be("::1");

            HttpContext.Request.Headers.TryGetValue(Constants.XForwardedHost, out values).Should().BeTrue();
            values.Single().StartsWithOrdinalIgnoreCase("localhost").Should().BeTrue();

            HttpContext.Request.Headers.TryGetValue(Constants.XForwardedProto, out values).Should().BeTrue();
            values.Single().Should().Be("http");

            HttpContext.Request.Headers.ContainsKey(Constants.Forwarded).Should().BeFalse();

            return message;
        }

        [HttpGet("xForwardedMultiProxies")]
        public string XForwardedMultiProxiesTest(string message)
        {
            var values = HttpContext.Request.Headers.GetCommaSeparatedValues(Constants.XForwardedFor);
            values.Should().BeEquivalentTo(new[] { "some-for", "192.0.2.60", "1020:3040:5060:7080:9010:1112:1314:1516", "::1" });

            values = HttpContext.Request.Headers.GetCommaSeparatedValues(Constants.XForwardedHost);
            values.Should().BeEquivalentTo(new[] { "some-host" });

            values = HttpContext.Request.Headers.GetCommaSeparatedValues(Constants.XForwardedProto);
            values.Should().BeEquivalentTo(new[] { "some-proto" });

            HttpContext.Request.Headers.ContainsKey(Constants.Forwarded).Should().BeFalse();

            return message;
        }

        private static string RemovePortFromHostInForwarded(string forwarded)
        {
            var endIndex = forwarded.LastIndexOf(';');
            var startIndex = forwarded.LastIndexOf(':', endIndex);
            return forwarded.Substring(0, startIndex) + forwarded.Substring(endIndex, forwarded.Length - endIndex);
        }
    }
}
