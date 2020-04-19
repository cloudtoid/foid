﻿namespace Cloudtoid.GatewayCore.UnitTests
{
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Primitives;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Cloudtoid.GatewayCore.Upstream.RequestHeaderSetter;

    public sealed partial class RequestHeaderTests
    {
        private const string ForwardedHeader = "Forwarded";
        private const string XForwardedForHeader = "x-forwarded-for";
        private const string XForwardedHostHeader = "x-forwarded-host";
        private const string XForwardedProtoHeader = "x-forwarded-proto";
        private static readonly IPAddress IpV4Sample = new IPAddress(new byte[] { 0, 1, 2, 3 });
        private static readonly IPAddress IpV4Sample2 = new IPAddress(new byte[] { 4, 5, 6, 7 });
        private static readonly IPAddress IpV6Sample = new IPAddress(new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80, 0x90, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16 });

        [TestMethod]
        public void GetCurrentForwardedHeaderValues_MixedTests()
        {
            var headers = new HeaderDictionary
            {
                { XForwardedForHeader, IpV4Sample.ToString() },
                { XForwardedProtoHeader, "http" },
                { XForwardedHostHeader, "some-host" }
            };

            GetCurrentForwardedHeaderValues(headers).Should().BeEquivalentTo(new[]
            {
                new ForwardedHeaderValue(@for: IpV4Sample.ToString(), proto: "http", host: "some-host")
            });

            headers = new HeaderDictionary
            {
                { XForwardedForHeader, new StringValues(new[] { IpV4Sample.ToString(), IpV4Sample2.ToString() }) },
                { XForwardedProtoHeader, "http" },
                { XForwardedHostHeader, "some-host" }
            };

            GetCurrentForwardedHeaderValues(headers).Should().BeEquivalentTo(new[]
            {
                new ForwardedHeaderValue(@for: IpV4Sample.ToString(), proto: "http", host: "some-host"),
                new ForwardedHeaderValue(@for: IpV4Sample2.ToString(), proto: "http", host: "some-host"),
            });

            headers = new HeaderDictionary
            {
                { XForwardedForHeader, IpV4Sample.ToString() },
            };

            GetCurrentForwardedHeaderValues(headers).Should().BeEquivalentTo(new[]
            {
                new ForwardedHeaderValue(@for: IpV4Sample.ToString()),
            });

            headers = new HeaderDictionary
            {
                { XForwardedForHeader, new StringValues(new[] { IpV4Sample.ToString(), IpV4Sample2.ToString() }) },
                { XForwardedProtoHeader, "http" },
                { XForwardedHostHeader, "some-host" },
                { ForwardedHeader, "for=9.8.7.6;host=new-host;proto=https;by=6.5.4.3" }
            };

            GetCurrentForwardedHeaderValues(headers).Should().BeEquivalentTo(new[]
            {
                new ForwardedHeaderValue(@for: IpV4Sample.ToString(), proto: "http", host: "some-host"),
                new ForwardedHeaderValue(@for: IpV4Sample2.ToString(), proto: "http", host: "some-host"),
                new ForwardedHeaderValue(by: "6.5.4.3", @for: "9.8.7.6", proto: "https", host: "new-host"),
            });

            headers = new HeaderDictionary
            {
                { XForwardedForHeader, new StringValues(new[] { IpV4Sample.ToString(), IpV4Sample2.ToString() }) },
                { XForwardedProtoHeader, "http" },
                { XForwardedHostHeader, "some-host" },
                { ForwardedHeader, "for=192.0.2.60;proto=http;by=203.0.113.43;host=abc,for=192.0.2.60;proto=https;by=203.0.113.43;host=efg" }
            };

            GetCurrentForwardedHeaderValues(headers).Should().BeEquivalentTo(new[]
            {
                new ForwardedHeaderValue(@for: IpV4Sample.ToString(), proto: "http", host: "some-host"),
                new ForwardedHeaderValue(@for: IpV4Sample2.ToString(), proto: "http", host: "some-host"),
                new ForwardedHeaderValue("203.0.113.43", "192.0.2.60", "abc", "http"),
                new ForwardedHeaderValue("203.0.113.43", "192.0.2.60", "efg", "https")
            });

            headers = new HeaderDictionary
            {
                { XForwardedForHeader, IpV4Sample.ToString() },
                { XForwardedProtoHeader, new StringValues(new[] { "http", "https" }) },
                { XForwardedHostHeader, new StringValues(new[] { "some-host", "some-host-2" }) }
            };

            GetCurrentForwardedHeaderValues(headers).Should().BeEquivalentTo(new[]
            {
                new ForwardedHeaderValue(@for: IpV4Sample.ToString(), proto: "http", host: "some-host")
            });
        }

        [TestMethod]
        public void TryParseIpV6Address_MixedTests()
        {
            TryParseIpV6Address("\"[2001:db8:cafe::17]:4711\"", out var ip).Should().BeTrue();
            ip!.ToString().Should().Be("2001:db8:cafe::17");

            TryParseIpV6Address("\"[2001:db8:cafe::17]\"", out ip).Should().BeTrue();
            ip!.ToString().Should().Be("2001:db8:cafe::17");

            TryParseIpV6Address("\"[2001:]\"", out ip).Should().BeFalse();
            ip.Should().BeNull();

            TryParseIpV6Address("\"[sdf]\"", out ip).Should().BeFalse();
            ip.Should().BeNull();

            TryParseIpV6Address("\"[]\"", out ip).Should().BeFalse();
            ip.Should().BeNull();

            TryParseIpV6Address("\"[]:987\"", out ip).Should().BeFalse();
            ip.Should().BeNull();

            TryParseIpV6Address("\"[]:987", out ip).Should().BeFalse();
            ip.Should().BeNull();

            TryParseIpV6Address(string.Empty, out ip).Should().BeFalse();
            ip.Should().BeNull();
        }

        [TestMethod]
        public void TryParseForwardedHeaderValuesTests()
        {
            TryParseForwardedHeaderValues(string.Empty, out var values).Should().BeFalse();
            values.Should().BeNull();

            TryParseForwardedHeaderValues(" ", out values).Should().BeFalse();
            values.Should().BeNull();

            TryParseForwardedHeaderValues(",", out values).Should().BeFalse();
            values.Should().BeNull();

            TryParseForwardedHeaderValues(", , ,    ,", out values).Should().BeFalse();
            values.Should().BeNull();

            TryParseForwardedHeaderValues(";, , ;,  ;  ,", out values).Should().BeFalse();
            values.Should().BeNull();

            TryParseForwardedHeaderValues("abc", out values).Should().BeFalse();
            values.Should().BeNull();

            TryParseForwardedHeaderValues("host=abc", out values).Should().BeTrue();
            values.Should().HaveCount(1);
            values![0].Should().Be(new ForwardedHeaderValue(host: "abc"));

            TryParseForwardedHeaderValues("for=192.0.2.60", out values).Should().BeTrue();
            values.Should().HaveCount(1);
            values![0].Should().Be(new ForwardedHeaderValue(@for: "192.0.2.60"));

            TryParseForwardedHeaderValues("proto=http", out values).Should().BeTrue();
            values.Should().HaveCount(1);
            values![0].Should().Be(new ForwardedHeaderValue(proto: "http"));

            TryParseForwardedHeaderValues("by=203.0.113.43", out values).Should().BeTrue();
            values.Should().HaveCount(1);
            values![0].Should().Be(new ForwardedHeaderValue(by: "203.0.113.43"));

            TryParseForwardedHeaderValues("for=192.0.2.60;proto=http;by=203.0.113.43;host=abc", out values).Should().BeTrue();
            values.Should().HaveCount(1);
            values![0].Should().Be(new ForwardedHeaderValue("203.0.113.43", "192.0.2.60", "abc", "http"));

            TryParseForwardedHeaderValues("FOR=192.0.2.60;PROTO=http;BY=203.0.113.43;HOST=abc", out values).Should().BeTrue();
            values.Should().HaveCount(1);
            values![0].Should().Be(new ForwardedHeaderValue("203.0.113.43", "192.0.2.60", "abc", "http"));

            TryParseForwardedHeaderValues(";;;for=192.0.2.60;proto=http;by=203.0.113.43;host=abc;;;;", out values).Should().BeTrue();
            values.Should().HaveCount(1);
            values![0].Should().Be(new ForwardedHeaderValue("203.0.113.43", "192.0.2.60", "abc", "http"));

            TryParseForwardedHeaderValues(",,,for=192.0.2.60;;proto=http;by=203.0.113.43;;host=abc,,,,", out values).Should().BeTrue();
            values.Should().HaveCount(1);
            values![0].Should().Be(new ForwardedHeaderValue("203.0.113.43", "192.0.2.60", "abc", "http"));

            TryParseForwardedHeaderValues(" for= 192.0.2.60   ; proto= http ; by= 203.0.113.43 ; host= abc ", out values).Should().BeTrue();
            values.Should().HaveCount(1);
            values![0].Should().Be(new ForwardedHeaderValue("203.0.113.43", "192.0.2.60", "abc", "http"));

            TryParseForwardedHeaderValues("host=", out values).Should().BeFalse();
            values.Should().BeNull();

            TryParseForwardedHeaderValues("for=", out values).Should().BeFalse();
            values.Should().BeNull();

            TryParseForwardedHeaderValues("proto=", out values).Should().BeFalse();
            values.Should().BeNull();

            TryParseForwardedHeaderValues("by=", out values).Should().BeFalse();
            values.Should().BeNull();

            TryParseForwardedHeaderValues("host= ", out values).Should().BeFalse();
            values.Should().BeNull();

            TryParseForwardedHeaderValues("for=  ", out values).Should().BeFalse();
            values.Should().BeNull();

            TryParseForwardedHeaderValues("proto= ", out values).Should().BeFalse();
            values.Should().BeNull();

            TryParseForwardedHeaderValues("by= ", out values).Should().BeFalse();
            values.Should().BeNull();

            TryParseForwardedHeaderValues("for=192.0.2.60,proto=http,by=203.0.113.43,host=abc", out values).Should().BeTrue();
            values.Should().HaveCount(4);
            values.Should().BeEquivalentTo(new[]
            {
                new ForwardedHeaderValue(@for: "192.0.2.60"),
                new ForwardedHeaderValue(proto: "http"),
                new ForwardedHeaderValue(by: "203.0.113.43"),
                new ForwardedHeaderValue(host: "abc")
            });

            TryParseForwardedHeaderValues("for=192.0.2.60;proto=http;by=203.0.113.43;host=abc,for=192.0.2.60;proto=https;by=203.0.113.43;host=efg", out values).Should().BeTrue();
            values.Should().HaveCount(2);
            values.Should().BeEquivalentTo(new[]
            {
                new ForwardedHeaderValue("203.0.113.43", "192.0.2.60", "abc", "http"),
                new ForwardedHeaderValue("203.0.113.43", "192.0.2.60", "efg", "https")
            });
        }

        [TestMethod]
        public async Task SetHeadersAsync_IgnoreForwarded_ForwardedAndXForwardedHeadersNotIncludedAsync()
        {
            // Arrange
            var options = TestExtensions.CreateDefaultOptions();
            var headersOptions = options.Routes["/api/"].Proxy!.UpstreamRequest.Headers;
            headersOptions.IgnoreForwarded = true;
            headersOptions.UseXForwarded = false;

            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IpV4Sample;
            context.Connection.LocalIpAddress = IpV4Sample2;

            // Act
            var message = await SetHeadersAsync(context, options);

            // Assert
            message.Headers.Contains(XForwardedForHeader).Should().BeFalse();
            message.Headers.Contains(ForwardedHeader).Should().BeFalse();
        }

        [TestMethod]
        public async Task SetHeadersAsync_IgnoreForwardedAndUseXForwarded_ForwardedAndXForwardedHeadersNotIncludedAsync()
        {
            // Arrange
            var options = TestExtensions.CreateDefaultOptions();
            var headersOptions = options.Routes["/api/"].Proxy!.UpstreamRequest.Headers;
            headersOptions.IgnoreForwarded = true;
            headersOptions.UseXForwarded = true;

            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IpV4Sample;
            context.Connection.LocalIpAddress = IpV4Sample2;

            // Act
            var message = await SetHeadersAsync(context, options);

            // Assert
            message.Headers.Contains(XForwardedForHeader).Should().BeFalse();
            message.Headers.Contains(ForwardedHeader).Should().BeFalse();
        }

        [TestMethod]
        public async Task SetHeadersAsync_HasForwardAndXForwardedHeadersButIgnoreDownstreamHeaders_ForwardedAndXForwardedHeadersNotKeptAsync()
        {
            // Arrange
            var options = TestExtensions.CreateDefaultOptions();
            var headersOptions = options.Routes["/api/"].Proxy!.UpstreamRequest.Headers;
            headersOptions.IgnoreAllDownstreamHeaders = true;
            headersOptions.IgnoreForwarded = false;
            headersOptions.UseXForwarded = false;

            var context = new DefaultHttpContext();
            context.Request.Headers.Add(Headers.Names.Forwarded, "for=192.0.2.60;proto=http;by=203.0.113.43;host=abc, for=192.0.2.12;proto=https;by=203.0.113.43;host=efg");
            context.Request.Headers.Add(Headers.Names.XForwardedFor, "some-for");
            context.Request.Headers.Add(Headers.Names.XForwardedHost, "some-host");
            context.Request.Headers.Add(Headers.Names.XForwardedProto, "some-proto");
            context.Request.Host = new HostString("some-new-host");
            context.Request.Scheme = "https";
            context.Connection.RemoteIpAddress = IpV4Sample;
            context.Connection.LocalIpAddress = IpV4Sample2;

            // Act
            var message = await SetHeadersAsync(context, options);

            // Assert
            message.Headers.GetValues(Headers.Names.Forwarded).Should().BeEquivalentTo(
                new[]
                {
                    "by=4.5.6.7;for=0.1.2.3;host=some-new-host;proto=https"
                });
        }

        [TestMethod]
        public async Task SetHeadersAsync_HasForwardAndXForwardedHeadersButIgnoreDownstreamHeadersAndUseXForwarded_ForwardedAndXForwardedHeadersNotKeptAsync()
        {
            // Arrange
            var options = TestExtensions.CreateDefaultOptions();
            var headersOptions = options.Routes["/api/"].Proxy!.UpstreamRequest.Headers;
            headersOptions.IgnoreAllDownstreamHeaders = true;
            headersOptions.IgnoreForwarded = false;
            headersOptions.UseXForwarded = true;

            var context = new DefaultHttpContext();
            context.Request.Headers.Add(Headers.Names.Forwarded, "for=192.0.2.60;proto=http;by=203.0.113.43;host=abc, for=192.0.2.12;proto=https;by=203.0.113.43;host=efg");
            context.Request.Headers.Add(Headers.Names.XForwardedFor, "some-for");
            context.Request.Headers.Add(Headers.Names.XForwardedHost, "some-host");
            context.Request.Headers.Add(Headers.Names.XForwardedProto, "some-proto");
            context.Request.Host = new HostString("some-new-host");
            context.Request.Scheme = "https";
            context.Connection.RemoteIpAddress = IpV4Sample;
            context.Connection.LocalIpAddress = IpV4Sample2;

            // Act
            var message = await SetHeadersAsync(context, options);

            // Assert
            message.Headers.GetValues(Headers.Names.XForwardedFor).Should().BeEquivalentTo(
                new[]
                {
                    IpV4Sample.ToString()
                });
        }

        [TestMethod]
        public async Task SetHeadersAsync_HasXForwardHeadersButIgnoreDownstreamHeaders_ExistingXForwardedHeaderIsNotKeptAsync()
        {
            // Arrange
            var options = TestExtensions.CreateDefaultOptions();
            var headersOptions = options.Routes["/api/"].Proxy!.UpstreamRequest.Headers;
            headersOptions.IgnoreAllDownstreamHeaders = true;
            headersOptions.IgnoreForwarded = false;
            headersOptions.UseXForwarded = false;

            var context = new DefaultHttpContext();
            context.Request.Headers.Add(Headers.Names.XForwardedFor, "some-for");
            context.Request.Headers.Add(Headers.Names.XForwardedHost, "some-host");
            context.Request.Headers.Add(Headers.Names.XForwardedProto, "some-proto");
            context.Request.Host = new HostString("some-new-host");
            context.Request.Scheme = "https";
            context.Connection.RemoteIpAddress = IpV4Sample;
            context.Connection.LocalIpAddress = IpV4Sample2;

            // Act
            var message = await SetHeadersAsync(context, options);

            // Assert
            message.Headers.GetValues(Headers.Names.Forwarded).Should().BeEquivalentTo(
                new[]
                {
                    "by=4.5.6.7;for=0.1.2.3;host=some-new-host;proto=https"
                });
        }

        [TestMethod]
        public async Task SetHeadersAsync_UseXForwardedAndDoesntHaveForwardedAndXForwardedHeaders_XForwardedHeadersCreatedAsync()
        {
            // Arrange
            var options = TestExtensions.CreateDefaultOptions();
            var headersOptions = options.Routes["/api/"].Proxy!.UpstreamRequest.Headers;
            headersOptions.IgnoreForwarded = false;
            headersOptions.UseXForwarded = true;

            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IpV4Sample;
            context.Connection.LocalIpAddress = IpV4Sample2;
            context.Request.Host = new HostString("some-host");
            context.Request.Scheme = "http";

            // Act
            var message = await SetHeadersAsync(context, options);

            // Assert
            message.Headers.GetValues(XForwardedForHeader).SingleOrDefault().Should().Be(IpV4Sample.ToString());
            message.Headers.GetValues(XForwardedHostHeader).SingleOrDefault().Should().Be("some-host");
            message.Headers.GetValues(XForwardedProtoHeader).SingleOrDefault().Should().Be("http");
        }

        [TestMethod]
        public async Task SetHeadersAsync_UseXForwardedAndHasForwarded_XForwardedHeaderCreatedAsync()
        {
            // Arrange
            var options = TestExtensions.CreateDefaultOptions();
            var headersOptions = options.Routes["/api/"].Proxy!.UpstreamRequest.Headers;
            headersOptions.IgnoreForwarded = false;
            headersOptions.UseXForwarded = true;

            var context = new DefaultHttpContext();
            context.Request.Headers.Add(Headers.Names.Forwarded, "for=192.0.2.60;proto=http;by=203.0.113.43;host=abc, for=192.0.2.12;proto=https;by=203.0.113.43;host=efg");
            context.Connection.RemoteIpAddress = IpV4Sample;
            context.Connection.LocalIpAddress = IpV4Sample2;
            context.Request.Host = new HostString("some-host");
            context.Request.Scheme = "http";

            // Act
            var message = await SetHeadersAsync(context, options);

            // Assert
            message.Headers.GetValues(XForwardedForHeader).SingleOrDefault().Should().Be($"192.0.2.60, 192.0.2.12, {IpV4Sample}");
            message.Headers.GetValues(XForwardedHostHeader).SingleOrDefault().Should().Be("some-host");
            message.Headers.GetValues(XForwardedProtoHeader).SingleOrDefault().Should().Be("http");
        }

        [TestMethod]
        public async Task SetHeadersAsync_UseXForwardedHasXForwarded_XForwardedHeaderCreatedAsync()
        {
            // Arrange
            var options = TestExtensions.CreateDefaultOptions();
            var headersOptions = options.Routes["/api/"].Proxy!.UpstreamRequest.Headers;
            headersOptions.IgnoreForwarded = false;
            headersOptions.UseXForwarded = true;

            var context = new DefaultHttpContext();
            context.Request.Headers.Add(Headers.Names.XForwardedFor, "some-for");
            context.Request.Headers.Add(Headers.Names.XForwardedHost, "some-host");
            context.Request.Headers.Add(Headers.Names.XForwardedProto, "some-proto");
            context.Connection.RemoteIpAddress = IpV4Sample;
            context.Connection.LocalIpAddress = IpV4Sample2;
            context.Request.Host = new HostString("some-host");
            context.Request.Scheme = "http";

            // Act
            var message = await SetHeadersAsync(context, options);

            // Assert
            message.Headers.GetValues(XForwardedForHeader).SingleOrDefault().Should().Be($"some-for, {IpV4Sample}");
            message.Headers.GetValues(XForwardedHostHeader).SingleOrDefault().Should().Be("some-host");
            message.Headers.GetValues(XForwardedProtoHeader).SingleOrDefault().Should().Be("http");
        }

        [TestMethod]
        public async Task SetHeadersAsync_UseXForwardedHasXForwardedAndForwarded_XForwardedHeaderCreatedAsync()
        {
            // Arrange
            var options = TestExtensions.CreateDefaultOptions();
            var headersOptions = options.Routes["/api/"].Proxy!.UpstreamRequest.Headers;
            headersOptions.IgnoreForwarded = false;
            headersOptions.UseXForwarded = true;

            var context = new DefaultHttpContext();
            context.Request.Headers.Add(Headers.Names.Forwarded, "for=192.0.2.60;proto=http;by=203.0.113.43;host=abc, for=192.0.2.12;proto=https;by=203.0.113.43;host=efg");
            context.Request.Headers.Add(Headers.Names.XForwardedFor, "some-for");
            context.Request.Headers.Add(Headers.Names.XForwardedHost, "some-host");
            context.Request.Headers.Add(Headers.Names.XForwardedProto, "some-proto");
            context.Connection.RemoteIpAddress = IpV4Sample;
            context.Connection.LocalIpAddress = IpV4Sample2;
            context.Request.Host = new HostString("some-host");
            context.Request.Scheme = "http";

            // Act
            var message = await SetHeadersAsync(context, options);

            // Assert
            message.Headers.GetValues(XForwardedForHeader).SingleOrDefault().Should().Be($"some-for, 192.0.2.60, 192.0.2.12, {IpV4Sample}");
            message.Headers.GetValues(XForwardedHostHeader).SingleOrDefault().Should().Be("some-host");
            message.Headers.GetValues(XForwardedProtoHeader).SingleOrDefault().Should().Be("http");
        }

        [TestMethod]
        public async Task SetHeadersAsync_HasForwardHeaders_ExistingForwardedHeaderIsKeptAsync()
        {
            // Arrange
            var options = TestExtensions.CreateDefaultOptions();
            var headersOptions = options.Routes["/api/"].Proxy!.UpstreamRequest.Headers;
            headersOptions.IgnoreAllDownstreamHeaders = false;
            headersOptions.IgnoreForwarded = false;
            headersOptions.UseXForwarded = false;

            var context = new DefaultHttpContext();
            context.Request.Headers.Add(Headers.Names.Forwarded, "for=192.0.2.60;proto=http;by=203.0.113.43;host=abc, for=192.0.2.12;proto=https;by=203.0.113.43;host=efg");
            context.Request.Host = new HostString("some-new-host");
            context.Request.Scheme = "https";
            context.Connection.RemoteIpAddress = IpV4Sample;
            context.Connection.LocalIpAddress = IpV4Sample2;

            // Act
            var message = await SetHeadersAsync(context, options);

            // Assert
            message.Headers.GetValues(Headers.Names.Forwarded).Should().BeEquivalentTo(
                new[]
                {
                    "by=203.0.113.43;for=192.0.2.60;host=abc;proto=http, by=203.0.113.43;for=192.0.2.12;host=efg;proto=https, by=4.5.6.7;for=0.1.2.3;host=some-new-host;proto=https"
                });
        }

        [TestMethod]
        public async Task SetHeadersAsync_HasXForwardHeaders_XForwardHeaderIsConvertedToForwardHeaderAsync()
        {
            // Arrange
            var options = TestExtensions.CreateDefaultOptions();
            var headersOptions = options.Routes["/api/"].Proxy!.UpstreamRequest.Headers;
            headersOptions.IgnoreAllDownstreamHeaders = false;
            headersOptions.IgnoreForwarded = false;
            headersOptions.UseXForwarded = false;

            var context = new DefaultHttpContext();
            context.Request.Headers.Add(Headers.Names.XForwardedFor, "some-for");
            context.Request.Headers.Add(Headers.Names.XForwardedHost, "some-host");
            context.Request.Headers.Add(Headers.Names.XForwardedProto, "some-proto");
            context.Request.Host = new HostString("some-new-host");
            context.Request.Scheme = "https";
            context.Connection.RemoteIpAddress = IpV4Sample;
            context.Connection.LocalIpAddress = IpV4Sample2;

            // Act
            var message = await SetHeadersAsync(context, options);

            // Assert
            message.Headers.GetValues(Headers.Names.Forwarded).Should().BeEquivalentTo(
                new[]
                {
                    "for=some-for;host=some-host;proto=some-proto, by=4.5.6.7;for=0.1.2.3;host=some-new-host;proto=https"
                });
        }

        [TestMethod]
        public async Task SetHeadersAsync_HasForwardAndXForwardHeaders_XForwardHeaderIsConvertedToForwardHeaderAndExistingForwardHeaderIsKeptAsync()
        {
            // Arrange
            var options = TestExtensions.CreateDefaultOptions();
            var headersOptions = options.Routes["/api/"].Proxy!.UpstreamRequest.Headers;
            headersOptions.IgnoreAllDownstreamHeaders = false;
            headersOptions.IgnoreForwarded = false;
            headersOptions.UseXForwarded = false;

            var context = new DefaultHttpContext();
            context.Request.Headers.Add(Headers.Names.Forwarded, "for=192.0.2.60;proto=http;by=203.0.113.43;host=abc, for=192.0.2.12;proto=https;by=203.0.113.43;host=efg");
            context.Request.Headers.Add(Headers.Names.XForwardedFor, "some-for");
            context.Request.Headers.Add(Headers.Names.XForwardedHost, "some-host");
            context.Request.Headers.Add(Headers.Names.XForwardedProto, "some-proto");
            context.Request.Host = new HostString("some-new-host");
            context.Request.Scheme = "https";
            context.Connection.RemoteIpAddress = IpV4Sample;
            context.Connection.LocalIpAddress = IpV4Sample2;

            // Act
            var message = await SetHeadersAsync(context, options);

            // Assert
            message.Headers.GetValues(Headers.Names.Forwarded).Should().BeEquivalentTo(
                new[]
                {
                    "for=some-for;host=some-host;proto=some-proto, by=203.0.113.43;for=192.0.2.60;host=abc;proto=http, by=203.0.113.43;for=192.0.2.12;host=efg;proto=https, by=4.5.6.7;for=0.1.2.3;host=some-new-host;proto=https"
                });
        }

        [TestMethod]
        public async Task SetHeadersAsync_IpAddressV6InXForwarded_IsNotWrappedInBracketsAsync()
        {
            // Arrange
            var options = TestExtensions.CreateDefaultOptions();
            var headersOptions = options.Routes["/api/"].Proxy!.UpstreamRequest.Headers;
            headersOptions.IgnoreForwarded = false;
            headersOptions.UseXForwarded = true;

            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IpV6Sample;
            context.Connection.LocalIpAddress = IpV4Sample2;

            // Act
            var message = await SetHeadersAsync(context, options);

            // Assert
            message.Headers.GetValues(XForwardedForHeader).Should().BeEquivalentTo(
                new[]
                {
                    "1020:3040:5060:7080:9010:1112:1314:1516"
                });
        }

        [TestMethod]
        public async Task SetHeadersAsync_IpAddressV6InForwarded_IsWrappedInBracketsAsync()
        {
            // Arrange
            var options = TestExtensions.CreateDefaultOptions();
            var headersOptions = options.Routes["/api/"].Proxy!.UpstreamRequest.Headers;
            headersOptions.IgnoreForwarded = false;
            headersOptions.UseXForwarded = false;

            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IpV6Sample;
            context.Connection.LocalIpAddress = IpV6Sample;

            // Act
            var message = await SetHeadersAsync(context, options);

            // Assert
            message.Headers.GetValues(ForwardedHeader).Should().BeEquivalentTo(
                new[]
                {
                    "by=\"[1020:3040:5060:7080:9010:1112:1314:1516]\";for=\"[1020:3040:5060:7080:9010:1112:1314:1516]\""
                });
        }
    }
}