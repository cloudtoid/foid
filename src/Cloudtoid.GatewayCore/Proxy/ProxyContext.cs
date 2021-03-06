﻿using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Cloudtoid.GatewayCore.Expression;
using Cloudtoid.GatewayCore.Settings;
using Microsoft.AspNetCore.Http;
using static Cloudtoid.Contract;

namespace Cloudtoid.GatewayCore
{
    [DebuggerStepThrough]
    public sealed class ProxyContext
    {
        private readonly IExpressionEvaluator evaluator;
        private string? proxyName;
        private Version? requestHttpVersion;
        private Version? upstreamRequestHttpVersion;

        internal ProxyContext(
            IExpressionEvaluator evaluator,
            HttpContext httpContext,
            Route route)
        {
            ProxySettings = CheckValue(
                route.Settings.Proxy,
                nameof(route.Settings.Proxy),
                "This is the actual proxy context. We should never get here if the proxy is null.");

            this.evaluator = evaluator;
            HttpContext = httpContext;
            Route = route;
        }

        public Route Route { get; }

        public HttpContext HttpContext { get; }

        public string ProxyName
            => proxyName ??= ProxySettings.EvaluateProxyName(this);

        public Version RequestHttpVersion
            => requestHttpVersion ??= HttpVersion.ParseOrDefault(Request.Protocol) ?? HttpVersion.Version11;

        public Version UpstreamRequestHttpVersion
            => upstreamRequestHttpVersion ??= ProxyUpstreamRequestSettings.EvaluateHttpVersion(this);

        internal HttpRequest Request
            => HttpContext.Request;

        internal HttpResponse Response
            => HttpContext.Response;

        internal RouteSettings Settings
            => Route.Settings;

        internal ProxySettings ProxySettings { get; }

        internal UpstreamRequestSettings ProxyUpstreamRequestSettings
            => ProxySettings.UpstreamRequest;

        internal UpstreamRequestHeadersSettings ProxyUpstreamRequestHeadersSettings
            => ProxyUpstreamRequestSettings.Headers;

        internal UpstreamRequestSenderSettings ProxyUpstreamRequestSenderSettings
            => ProxyUpstreamRequestSettings.Sender;

        internal DownstreamResponseSettings ProxyDownstreamResponseSettings
            => ProxySettings.DownstreamResponse;

        internal DownstreamResponseHeadersSettings ProxyDownstreamResponseHeaderSettings
            => ProxyDownstreamResponseSettings.Headers;

        [return: NotNullIfNotNull("expression")]
        internal string? Evaluate(string? expression)
            => evaluator.Evaluate(this, expression);
    }
}
