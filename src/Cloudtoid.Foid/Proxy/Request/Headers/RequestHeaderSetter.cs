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

    internal sealed class RequestHeaderSetter : IRequestHeaderSetter
    {
        private static readonly HashSet<string> HeaderTransferBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Headers.Names.ExternalAddress,
            Headers.Names.CallId,
            Headers.Names.ProxyName,
        };

        private readonly IRequestHeaderValuesProvider provider;
        private readonly ITraceIdProvider traceIdProvider;
        private readonly ILogger<RequestHeaderSetter> logger;

        public RequestHeaderSetter(
            IRequestHeaderValuesProvider provider,
            ITraceIdProvider traceIdProvider,
            ILogger<RequestHeaderSetter> logger)
        {
            this.provider = CheckValue(provider, nameof(provider));
            this.traceIdProvider = CheckValue(traceIdProvider, nameof(traceIdProvider));
            this.logger = CheckValue(logger, nameof(logger));
        }

        public Task SetHeadersAsync(HttpContext context, HttpRequestMessage upstreamRequest)
        {
            CheckValue(context, nameof(context));
            CheckValue(upstreamRequest, nameof(upstreamRequest));

            context.RequestAborted.ThrowIfCancellationRequested();

            AddDownstreamHeadersToUpstream(context, upstreamRequest);
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

        private void AddDownstreamHeadersToUpstream(HttpContext context, HttpRequestMessage upstreamRequest)
        {
            if (provider.IgnoreAllDownstreamRequestHeaders)
                return;

            var headers = context.Request.Headers;
            if (headers is null)
                return;

            foreach (var header in headers)
            {
                var key = header.Key;

                // Remove empty headers
                if (!provider.AllowHeadersWithEmptyValue && header.Value.All(s => string.IsNullOrEmpty(s)))
                {
                    logger.LogInformation("Removing header '{0}' as its value is empty.", key);
                    continue;
                }

                // Remove headers with underscore in their names
                if (!provider.AllowHeadersWithUnderscoreInName && key.Contains('_'))
                {
                    logger.LogInformation("Removing header '{0}' as headers should not have underscores in their name.", header.Key);
                    continue;
                }

                if (key.EqualsOrdinalIgnoreCase(provider.CorrelationIdHeader))
                    continue;

                // If blacklisted, we will not trasnfer its value
                if (HeaderTransferBlacklist.Contains(key))
                    continue;

                AddHeaderValues(context, upstreamRequest, key, header.Value);
            }
        }

        private void AddHostHeader(HttpContext context, HttpRequestMessage upstreamRequest)
        {
            if (upstreamRequest.Headers.Contains(HeaderNames.Host))
                return;

            upstreamRequest.Headers.TryAddWithoutValidation(
                HeaderNames.Host,
                provider.GetDefaultHostHeaderValue(context));
        }

        private void AddExternalAddressHeader(HttpContext context, HttpRequestMessage upstreamRequest)
        {
            if (!provider.IncludeExternalAddress)
                return;

            var clientAddress = GetRemoteIpAddressOrDefault(context);
            if (clientAddress is null)
                return;

            AddHeaderValues(
                context,
                upstreamRequest,
                Headers.Names.ExternalAddress,
                clientAddress);
        }

        private void AddClientAddressHeader(HttpContext context, HttpRequestMessage upstreamRequest)
        {
            if (provider.IgnoreClientAddress)
                return;

            var clientAddress = GetRemoteIpAddressOrDefault(context);
            if (clientAddress is null)
                return;

            AddHeaderValues(
                context,
                upstreamRequest,
                Headers.Names.ClientAddress,
                clientAddress);
        }

        private void AddClientProtocolHeader(HttpContext context, HttpRequestMessage upstreamRequest)
        {
            if (provider.IgnoreClientProtocol)
                return;

            AddHeaderValues(
                context,
                upstreamRequest,
                Headers.Names.ClientProtocol,
                context.Request.Scheme);
        }

        private void AddCorrelationIdHeader(HttpContext context, HttpRequestMessage upstreamRequest)
        {
            if (provider.IgnoreCorrelationId)
                return;

            AddHeaderValues(
                context,
                upstreamRequest,
                provider.CorrelationIdHeader,
                traceIdProvider.GetCorrelationId(context));
        }

        private void AddCallIdHeader(HttpContext context, HttpRequestMessage upstreamRequest)
        {
            if (provider.IgnoreCallId)
                return;

            AddHeaderValues(
                context,
                upstreamRequest,
                Headers.Names.CallId,
                traceIdProvider.GetCallId(context));
        }

        private void AddProxyNameHeader(HttpContext context, HttpRequestMessage upstreamRequest)
        {
            var name = provider.GetProxyNameHeaderValue(context);
            if (string.IsNullOrWhiteSpace(name))
                return;

            AddHeaderValues(
                context,
                upstreamRequest,
                Headers.Names.ProxyName,
                name);
        }

        private void AddExtraHeaders(HttpContext context, HttpRequestMessage upstreamRequest)
        {
            foreach (var (key, values) in provider.GetExtraHeaders(context))
                upstreamRequest.Headers.TryAddWithoutValidation(key, values);
        }

        private void AddHeaderValues(
            HttpContext context,
            HttpRequestMessage upstreamRequest,
            string key,
            params string[] downstreamValues)
        {
            if (provider.TryGetHeaderValues(context, key, downstreamValues, out var upstreamValues) && upstreamValues != null)
            {
                upstreamRequest.Headers.TryAddWithoutValidation(key, upstreamValues);
                return;
            }

            logger.LogInformation(
                "Header '{0}' is not added. This was was instructed by the {1}.{2}.",
                key,
                nameof(IRequestHeaderValuesProvider),
                nameof(IRequestHeaderValuesProvider.TryGetHeaderValues));
        }

        private static string? GetRemoteIpAddressOrDefault(HttpContext context)
            => context.Connection.RemoteIpAddress?.ToString();
    }
}
