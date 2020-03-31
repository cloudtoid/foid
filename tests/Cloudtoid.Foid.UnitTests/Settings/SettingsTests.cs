﻿namespace Cloudtoid.Foid.UnitTests
{
    using System;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using Cloudtoid.Foid;
    using Cloudtoid.Foid.Expression;
    using Cloudtoid.Foid.Host;
    using Cloudtoid.Foid.Settings;
    using Cloudtoid.Foid.Trace;
    using FluentAssertions;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using NSubstitute;

    [TestClass]
    public sealed class SettingsTests
    {
        [TestMethod]
        public void New_FullyPopulatedOptions_AllValuesAreReadCorrectly()
        {
            var context = GetProxyContext(@"Settings\OptionsFull.json");
            var settings = context.Route.Settings;

            settings.Proxy!.GetCorrelationIdHeader(context).Should().Be("x-request-id");

            var request = settings.Proxy.UpstreamRequest;
            request.GetHttpVersion(context).Should().Be(HttpVersion.Version30);
            request.GetTimeout(context).TotalMilliseconds.Should().Be(5200);

            var requestHeaders = request.Headers;
            requestHeaders.TryGetProxyName(context, out var proxyName).Should().BeTrue();
            proxyName.Should().Be("some-proxy-name");
            requestHeaders.GetDefaultHost(context).Should().Be("this-machine-name");
            requestHeaders.AllowHeadersWithEmptyValue.Should().BeTrue();
            requestHeaders.AllowHeadersWithUnderscoreInName.Should().BeTrue();
            requestHeaders.IgnoreAllDownstreamHeaders.Should().BeTrue();
            requestHeaders.IgnoreCallId.Should().BeTrue();
            requestHeaders.IgnoreForwardedFor.Should().BeTrue();
            requestHeaders.IgnoreForwardedProtocol.Should().BeTrue();
            requestHeaders.IgnoreForwardedHost.Should().BeTrue();
            requestHeaders.IgnoreCorrelationId.Should().BeTrue();
            requestHeaders.IncludeExternalAddress.Should().BeTrue();
            requestHeaders.Overrides.Select(h => (h.Name, Values: h.GetValues(context)))
                .Should()
                .BeEquivalentTo(
                    new[]
                    {
                        (Name: "x-extra-1", Values: new[] { "value1_1", "value1_2" }),
                        (Name: "x-extra-2", Values: new[] { "value2_1", "value2_2" })
                    });

            var requestSender = request.Sender;
            requestSender.AllowAutoRedirect.Should().BeTrue();
            requestSender.UseCookies.Should().BeTrue();

            var response = settings.Proxy.DownstreamResponse;
            var responseHeaders = response.Headers;
            responseHeaders.AllowHeadersWithEmptyValue.Should().BeTrue();
            responseHeaders.AllowHeadersWithUnderscoreInName.Should().BeTrue();
            responseHeaders.Overrides.Select(h => (h.Name, Values: h.GetValues(context)))
                .Should()
                .BeEquivalentTo(
                    new[]
                    {
                        (Name: "x-extra-1", Values: new[] { "value1_1", "value1_2" }),
                        (Name: "x-extra-2", Values: new[] { "value2_1", "value2_2" })
                    });
        }

        [TestMethod]
        public void New_AllOptionsThatAllowExpressions_AllValuesAreEvaluatedCorrectly()
        {
            var context = GetProxyContext(@"Settings\OptionsWithExpressions.json");
            var settings = context.Route.Settings;

            var expressionValue = Environment.MachineName;
            settings.Proxy!.GetCorrelationIdHeader(context).Should().Be("CorrelationIdHeader:" + expressionValue);

            var request = settings.Proxy.UpstreamRequest;
            request.GetHttpVersion(context).Should().Be(HttpVersion.Version11);
            request.GetTimeout(context).TotalMilliseconds.Should().Be(5200);

            var requestHeaders = request.Headers;
            requestHeaders.TryGetProxyName(context, out var proxyName).Should().BeTrue();
            proxyName.Should().Be("ProxyName:" + expressionValue);
            requestHeaders.GetDefaultHost(context).Should().Be("DefaultHost:" + expressionValue);
            requestHeaders.Overrides.Select(h => (h.Name, Values: h.GetValues(context)))
                .Should()
                .BeEquivalentTo(
                    new[]
                    {
                        (Name: "x-extra-1", Values: new[] { "x-extra-1:v1:" + expressionValue, "x-extra-1:v2:" + expressionValue }),
                        (Name: "x-extra-2", Values: new[] { "x-extra-2:v1:" + expressionValue, "x-extra-2:v2:" + expressionValue })
                    });

            var response = settings.Proxy.DownstreamResponse;
            var responseHeaders = response.Headers;
            responseHeaders.Overrides.Select(h => (h.Name, Values: h.GetValues(context)))
                .Should()
                .BeEquivalentTo(
                    new[]
                    {
                        (Name: "x-extra-1", Values: new[] { "x-extra-1:v1:" + expressionValue, "x-extra-1:v2:" + expressionValue }),
                        (Name: "x-extra-2", Values: new[] { "x-extra-2:v1:" + expressionValue, "x-extra-2:v2:" + expressionValue })
                    });
        }

        [TestMethod]
        public void New_EmptyOptions_AllValuesSetToDefault()
        {
            var context = GetProxyContext(@"Settings\OptionsEmpty.json");
            var settings = context.Route.Settings;

            var request = settings.Proxy!.UpstreamRequest;
            request.GetHttpVersion(context).Should().Be(HttpVersion.Version20);
            request.GetTimeout(context).TotalMilliseconds.Should().Be(240000);

            var requestHeaders = request.Headers;
            requestHeaders.TryGetProxyName(context, out var proxyName).Should().BeTrue();
            proxyName.Should().Be("foid");
            requestHeaders.GetDefaultHost(context).Should().Be(Environment.MachineName);
            requestHeaders.AllowHeadersWithEmptyValue.Should().BeFalse();
            requestHeaders.AllowHeadersWithUnderscoreInName.Should().BeFalse();
            requestHeaders.IgnoreAllDownstreamHeaders.Should().BeFalse();
            requestHeaders.IgnoreCallId.Should().BeFalse();
            requestHeaders.IgnoreForwardedFor.Should().BeFalse();
            requestHeaders.IgnoreForwardedProtocol.Should().BeFalse();
            requestHeaders.IgnoreForwardedHost.Should().BeFalse();
            requestHeaders.IgnoreCorrelationId.Should().BeFalse();
            requestHeaders.IncludeExternalAddress.Should().BeFalse();
            requestHeaders.Overrides.Should().BeEmpty();

            var requestSender = request.Sender;
            requestSender.AllowAutoRedirect.Should().BeFalse();
            requestSender.UseCookies.Should().BeFalse();

            var response = settings.Proxy.DownstreamResponse;
            var responseHeaders = response.Headers;
            responseHeaders.AllowHeadersWithEmptyValue.Should().BeFalse();
            responseHeaders.AllowHeadersWithUnderscoreInName.Should().BeFalse();
            responseHeaders.Overrides.Should().BeEmpty();
        }

        [TestMethod]
        public void New_OptionsFileModified_FileIsReloaded()
        {
            try
            {
                File.Copy(@"Settings\Options1.json", @"Settings\OptionsReload.json", true);

                var config = new ConfigurationBuilder()
                    .AddJsonFile(@"Settings\OptionsReload.json", optional: false, reloadOnChange: true)
                    .Build();

                var services = new ServiceCollection()
                    .AddTest()
                    .Configure<ReverseProxyOptions>(config);

                var serviceProvider = services.BuildServiceProvider();
                var settingsProvider = serviceProvider.GetRequiredService<ISettingsProvider>();
                var monitor = serviceProvider.GetRequiredService<IOptionsMonitor<ReverseProxyOptions>>();

                var httpContext = new DefaultHttpContext();
                var settings = settingsProvider.CurrentValue.Routes.First();

                var context = new ProxyContext(
                    Substitute.For<IHostProvider>(),
                    Substitute.For<ITraceIdProvider>(),
                    httpContext,
                    new Route(settings, string.Empty, ImmutableDictionary<string, string>.Empty));

                using (var changeEvent = new AutoResetEvent(false))
                {
                    void Reset(object o)
                    {
                        changeEvent.Set();
                        monitor.OnChange(Reset);
                    }

                    monitor.OnChange(Reset);

                    settings.Proxy!.UpstreamRequest.GetTimeout(context).TotalMilliseconds.Should().Be(5000);

                    File.Copy(@"Settings\Options2.json", @"Settings\OptionsReload.json", true);
                    changeEvent.WaitOne(2000);

                    settings = settingsProvider.CurrentValue.Routes.First();
                    context = new ProxyContext(
                        Substitute.For<IHostProvider>(),
                        Substitute.For<ITraceIdProvider>(),
                        httpContext,
                        new Route(settings, string.Empty, ImmutableDictionary<string, string>.Empty));

                    settings.Proxy!.UpstreamRequest.GetTimeout(context).TotalMilliseconds.Should().Be(2000);

                    File.Copy(@"Settings\Options1.json", @"Settings\OptionsReload.json", true);
                    changeEvent.WaitOne(2000);
                    settings = settingsProvider.CurrentValue.Routes.First();
                    context = new ProxyContext(
                        Substitute.For<IHostProvider>(),
                        Substitute.For<ITraceIdProvider>(),
                        httpContext,
                        new Route(settings, string.Empty, ImmutableDictionary<string, string>.Empty));

                    settings.Proxy!.UpstreamRequest.GetTimeout(context).TotalMilliseconds.Should().Be(5000);
                }
            }
            finally
            {
                File.Delete(@"Settings\OptionsReload.json");
            }
        }

        [TestMethod]
        public void New_NoRoutes_CorrectError()
        {
            var options = new ReverseProxyOptions();
            CreateSettingsAndCheckLogs(options, "No routes are specified");
        }

        [TestMethod]
        public void New_EmptyRoute_CorrectError()
        {
            var options = new ReverseProxyOptions();
            options.Routes.Add(string.Empty, new ReverseProxyOptions.RouteOptions());
            CreateSettingsAndCheckLogs(options, "A route cannot be an empty string");
        }

        [TestMethod]
        public void New_NullProxyTo_CorrectError()
        {
            var options = new ReverseProxyOptions();
            options.Routes.Add("/a/b/c/", new ReverseProxyOptions.RouteOptions
            {
                Proxy = new ReverseProxyOptions.RouteOptions.ProxyOptions
                {
                    To = null
                }
            });

            CreateSettingsAndCheckLogs(options, "The 'To' cannot be empty or skipped.");
        }

        [TestMethod]
        public void New_EmptyProxyTo_CorrectError()
        {
            var options = new ReverseProxyOptions();
            options.Routes.Add("/a/b/c/", new ReverseProxyOptions.RouteOptions
            {
                Proxy = new ReverseProxyOptions.RouteOptions.ProxyOptions
                {
                    To = "    "
                }
            });

            CreateSettingsAndCheckLogs(options, "The 'To' cannot be empty or skipped.");
        }

        [TestMethod]
        public void New_WhenFailsToCompileRoutePattern_Fail()
        {
            var options = new ReverseProxyOptions();
            options.Routes.Add($"/category/:id/product/:id", new ReverseProxyOptions.RouteOptions
            {
                Proxy = new ReverseProxyOptions.RouteOptions.ProxyOptions
                {
                    To = "/somevalue"
                }
            });

            CreateSettingsAndCheckLogs(options, "The variable name 'id' has already been used. Variable names must be unique.");
        }

        [TestMethod]
        public void New_WhenCollidesWithSystemVariable_Fail()
        {
            var options = new ReverseProxyOptions();
            options.Routes.Add($"/:{SystemVariableNames.Host}/", new ReverseProxyOptions.RouteOptions
            {
                Proxy = new ReverseProxyOptions.RouteOptions.ProxyOptions
                {
                    To = "/somevalue"
                }
            });

            CreateSettingsAndCheckLogs(options, "The variable name 'host' collides with a system variable with the same name.");
        }

        [TestMethod]
        public void New_InvalidHeaderName_CorrectError()
        {
            var options = new ReverseProxyOptions();
            options.Routes.Add("/a/b/c/", new ReverseProxyOptions.RouteOptions
            {
                Proxy = new ReverseProxyOptions.RouteOptions.ProxyOptions
                {
                    To = "/e/f/g/",
                    UpstreamRequest = new ReverseProxyOptions.RouteOptions.ProxyOptions.UpstreamRequestOptions
                    {
                        Headers = new ReverseProxyOptions.RouteOptions.ProxyOptions.UpstreamRequestOptions.HeadersOptions
                        {
                            Overrides = new System.Collections.Generic.Dictionary<string, string[]>()
                            {
                                [" bad-header\\"] = new[] { "value" }
                            }
                        }
                    }
                }
            });

            CreateSettingsAndCheckLogs(options, "The ' bad-header\\' is not a valid HTTP header name. It will be ignored.");
        }

        private static ProxyContext GetProxyContext(string jsonFile)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile(jsonFile, optional: false)
                .Build();

            var services = new ServiceCollection()
                .AddTest()
                .Configure<ReverseProxyOptions>(config);

            var settingsProvider = services
                .BuildServiceProvider()
                .GetRequiredService<ISettingsProvider>();

            var httpContext = new DefaultHttpContext();
            var settings = settingsProvider.CurrentValue.Routes.First();

            return new ProxyContext(
                Substitute.For<IHostProvider>(),
                Substitute.For<ITraceIdProvider>(),
                httpContext,
                new Route(settings, string.Empty, ImmutableDictionary<string, string>.Empty));
        }

        private static void CreateSettingsAndCheckLogs(ReverseProxyOptions options, params string[] messages)
        {
            var services = new ServiceCollection().AddTest().AddTestOptions(options);
            var serviceProvider = services.BuildServiceProvider();
            var logger = (Logger<SettingsCreator>)serviceProvider.GetRequiredService<ILogger<SettingsCreator>>();
            serviceProvider.GetRequiredService<ISettingsProvider>();
            foreach (var message in messages)
                logger.Logs.Any(l => l.ContainsOrdinalIgnoreCase(message)).Should().BeTrue();
        }
    }
}
