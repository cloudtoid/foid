﻿namespace Cloudtoid.GatewayCore.Settings
{
    using System;
    using System.Net.Http;

    public sealed class UpstreamRequestSenderSettings
    {
        private readonly RouteSettingsContext context;
        private readonly string? timeoutExpression;

        internal UpstreamRequestSenderSettings(
            RouteSettingsContext context,
            string httpClientName,
            string? timeoutExpression,
            TimeSpan connectTimeout,
            TimeSpan expect100ContinueTimeout,
            TimeSpan pooledConnectionIdleTimeout,
            TimeSpan pooledConnectionLifetime,
            TimeSpan responseDrainTimeout,
            int maxAutomaticRedirections,
            int maxConnectionsPerServer,
            int maxResponseDrainSizeInBytes,
            int maxResponseHeadersLengthInKilobytes,
            bool allowAutoRedirect,
            bool useCookies)
        {
            this.context = context;
            HttpClientName = httpClientName;
            this.timeoutExpression = timeoutExpression;
            ConnectTimeout = connectTimeout;
            Expect100ContinueTimeout = expect100ContinueTimeout;
            PooledConnectionIdleTimeout = pooledConnectionIdleTimeout;
            PooledConnectionLifetime = pooledConnectionLifetime;
            ResponseDrainTimeout = responseDrainTimeout;
            MaxAutomaticRedirections = maxAutomaticRedirections;
            MaxConnectionsPerServer = maxConnectionsPerServer;
            MaxResponseDrainSizeInBytes = maxResponseDrainSizeInBytes;
            MaxResponseHeadersLengthInKilobytes = maxResponseHeadersLengthInKilobytes;
            AllowAutoRedirect = allowAutoRedirect;
            UseCookies = useCookies;
        }

        /// <summary>
        /// Gets or sets the name of the <see cref="HttpClient"/> to be used for upstream requests.
        /// Every time a route is defined in settings or an existing route is modified, we need to recreate the named
        /// <see cref="HttpClient"/>. The name here is used by the <see cref="IHttpClientFactory"/> to get instances of
        /// <see cref="HttpClient"/> configured with the request sender settings specified in <see cref="UpstreamRequestSenderSettings"/>.
        /// </summary>
        public string HttpClientName { get; }

        public TimeSpan ConnectTimeout { get; }

        public TimeSpan Expect100ContinueTimeout { get; }

        public TimeSpan PooledConnectionIdleTimeout { get; }

        public TimeSpan PooledConnectionLifetime { get; }

        public TimeSpan ResponseDrainTimeout { get; }

        public int MaxAutomaticRedirections { get; }

        public int MaxConnectionsPerServer { get; }

        public int MaxResponseDrainSizeInBytes { get; }

        public int MaxResponseHeadersLengthInKilobytes { get; }

        public bool AllowAutoRedirect { get; }

        public bool UseCookies { get; }

        public TimeSpan GetTimeout(ProxyContext proxyContext)
        {
            var result = context.Evaluate(proxyContext, timeoutExpression);

            return long.TryParse(result, out var timeout) && timeout > 0
                ? TimeSpan.FromMilliseconds(timeout)
                : Defaults.Route.Proxy.Upstream.Request.Sender.Timeout;
        }
    }
}
