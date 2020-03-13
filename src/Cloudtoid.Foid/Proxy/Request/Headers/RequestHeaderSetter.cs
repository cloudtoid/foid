﻿namespace Cloudtoid.Foid.Proxy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using Microsoft.Net.Http.Headers;
    using static Contract;
    using Options = OptionsProvider.ProxyOptions.UpstreamOptions.RequestOptions.HeadersOptions;

    /// <summary>
    /// By inheriting from this class, one can have full control over the outbound upstream request headers. Please consider the following extensibility points:
    /// 1. Inherit from <see cref="RequestHeaderValuesProvider"/>, override its methods, and register it with DI; or
    /// 2. Implement <see cref="IRequestHeaderValuesProvider"/> and register it with DI; or
    /// 3. Inherit from <see cref="RequestHeaderSetter"/>, override its methods, and register it with DI; or
    /// 4. Implement <see cref="IRequestHeaderSetter"/> and register it with DI.
    ///
    /// Dependency Injection registrations:
    /// 1. <c>TryAddSingleton<IRequestHeaderValuesProvider, MyRequestHeaderValuesProvider>()</c>
    /// 2. <c>TryAddSingleton<IRequestHeaderSetter, MyRequestHeaderSetter>()</c>
    /// </summary>
    public class RequestHeaderSetter : IRequestHeaderSetter
    {
        private static readonly ISet<string> HeaderTransferBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ProxyHeaderNames.ExternalAddress,
            HeaderNames.Host,
            ProxyHeaderNames.CallId,
            ProxyHeaderNames.ProxyName,
        };

        private readonly HeaderSanetizer sanetizer;

        public RequestHeaderSetter(
            IRequestHeaderValuesProvider provider,
            ITraceIdProvider traceIdProvider,
            IHostProvider hostProvider,
            OptionsProvider options,
            ILogger<RequestHeaderSetter> logger)
        {
            Provider = CheckValue(provider, nameof(provider));
            TraceIdProvider = CheckValue(traceIdProvider, nameof(traceIdProvider));
            HostProvider = CheckValue(hostProvider, nameof(hostProvider));
            Options = CheckValue(options, nameof(options));
            Logger = CheckValue(logger, nameof(logger));
            sanetizer = new HeaderSanetizer(logger);
        }

        protected IRequestHeaderValuesProvider Provider { get; }

        protected ITraceIdProvider TraceIdProvider { get; }

        protected IHostProvider HostProvider { get; }

        protected OptionsProvider Options { get; }

        protected ILogger<RequestHeaderSetter> Logger { get; }

        // Do NOT cache this value. Options react to changes.
        private Options HeaderOptions => Options.Proxy.Upstream.Request.Headers;

        public virtual Task SetHeadersAsync(HttpContext context, HttpRequestMessage upstreamRequest)
        {
            CheckValue(context, nameof(context));
            CheckValue(upstreamRequest, nameof(upstreamRequest));

            context.RequestAborted.ThrowIfCancellationRequested();

            AddDownstreamRequestHeadersToUpstream(context, upstreamRequest);
            AddHostHeader(context, upstreamRequest);
            AddExternalAddressHeader(context, upstreamRequest);
            AddClientAddressHeader(context, upstreamRequest);
            AddClientProtocolHeader(context, upstreamRequest);
            AddCorrelationIdHeader(context, upstreamRequest);
            AddCallIdHeader(context, upstreamRequest);
            AddProxyNameHeader(context, upstreamRequest);
            AddExtraHeaders(context, upstreamRequest);

            return Task.CompletedTask;
        }

        protected virtual void AddDownstreamRequestHeadersToUpstream(
            HttpContext context,
            HttpRequestMessage upstreamRequest)
        {
            if (HeaderOptions.IgnoreAllDownstreamHeaders)
                return;

            var headers = context.Request.Headers;
            if (headers is null)
                return;

            var allowHeadersWithEmptyValue = HeaderOptions.AllowHeadersWithEmptyValue;
            var allowHeadersWithUnderscoreInName = HeaderOptions.AllowHeadersWithUnderscoreInName;
            var correlationIdHeader = TraceIdProvider.GetCorrelationIdHeader(context);
            var headersWithOverride = HeaderOptions.HeaderNames;

            foreach (var header in headers)
            {
                var name = header.Key;

                if (!sanetizer.IsValid(
                    name,
                    header.Value,
                    allowHeadersWithEmptyValue,
                    allowHeadersWithUnderscoreInName))
                    continue;

                // Content headers should not be here. Ignore them.
                if (HeaderTypes.IsContentHeader(name))
                    continue;

                if (name.EqualsOrdinalIgnoreCase(correlationIdHeader))
                    continue;

                // If blacklisted, we will not trasnfer its value
                if (HeaderTransferBlacklist.Contains(name))
                    continue;

                // If it has an override, we will not trasnfer its value
                if (headersWithOverride.Contains(name))
                    continue;

                AddHeaderValues(context, upstreamRequest, name, header.Value);
            }
        }

        protected virtual void AddHostHeader(HttpContext context, HttpRequestMessage upstreamRequest)
        {
            if (HeaderOptions.IgnoreHost)
                return;

            upstreamRequest.Headers.TryAddWithoutValidation(
                HeaderNames.Host,
                HostProvider.GetHost(context));
        }

        protected virtual void AddExternalAddressHeader(HttpContext context, HttpRequestMessage upstreamRequest)
        {
            if (!HeaderOptions.IncludeExternalAddress)
                return;

            var clientAddress = GetRemoteIpAddressOrDefault(context);
            if (clientAddress is null)
                return;

            AddHeaderValues(
                context,
                upstreamRequest,
                ProxyHeaderNames.ExternalAddress,
                clientAddress);
        }

        protected virtual void AddClientAddressHeader(HttpContext context, HttpRequestMessage upstreamRequest)
        {
            if (HeaderOptions.IgnoreClientAddress)
                return;

            var clientAddress = GetRemoteIpAddressOrDefault(context);
            if (clientAddress is null)
                return;

            AddHeaderValues(
                context,
                upstreamRequest,
                ProxyHeaderNames.ClientAddress,
                clientAddress);
        }

        protected virtual void AddClientProtocolHeader(HttpContext context, HttpRequestMessage upstreamRequest)
        {
            if (HeaderOptions.IgnoreClientProtocol)
                return;

            AddHeaderValues(
                context,
                upstreamRequest,
                ProxyHeaderNames.ClientProtocol,
                context.Request.Scheme);
        }

        protected virtual void AddCorrelationIdHeader(HttpContext context, HttpRequestMessage upstreamRequest)
        {
            if (HeaderOptions.IgnoreCorrelationId)
                return;

            AddHeaderValues(
                context,
                upstreamRequest,
                TraceIdProvider.GetCorrelationIdHeader(context),
                TraceIdProvider.GetCorrelationId(context));
        }

        protected virtual void AddCallIdHeader(HttpContext context, HttpRequestMessage upstreamRequest)
        {
            if (HeaderOptions.IgnoreCallId)
                return;

            AddHeaderValues(
                context,
                upstreamRequest,
                ProxyHeaderNames.CallId,
                TraceIdProvider.GetCallId(context));
        }

        protected virtual void AddProxyNameHeader(HttpContext context, HttpRequestMessage upstreamRequest)
        {
            if (!HeaderOptions.TryGetProxyName(context, out var name))
                return;

            AddHeaderValues(
                context,
                upstreamRequest,
                ProxyHeaderNames.ProxyName,
                name);
        }

        protected virtual void AddExtraHeaders(HttpContext context, HttpRequestMessage upstreamRequest)
        {
            foreach (var header in HeaderOptions.Headers)
                upstreamRequest.Headers.TryAddWithoutValidation(header.Name, header.GetValues(context));
        }

        protected virtual void AddHeaderValues(
            HttpContext context,
            HttpRequestMessage upstreamRequest,
            string name,
            params string[] downstreamValues)
        {
            if (Provider.TryGetHeaderValues(context, name, downstreamValues, out var upstreamValues) && upstreamValues != null)
            {
                upstreamRequest.Headers.TryAddWithoutValidation(name, upstreamValues);
                return;
            }

            Logger.LogInformation(
                "Header '{0}' is not added. This was was instructed by the {1}.{2}.",
                name,
                nameof(IRequestHeaderValuesProvider),
                nameof(IRequestHeaderValuesProvider.TryGetHeaderValues));
        }

        private static string? GetRemoteIpAddressOrDefault(HttpContext context)
            => context.Connection.RemoteIpAddress?.ToString();
    }
}
