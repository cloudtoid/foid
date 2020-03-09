﻿namespace Cloudtoid.Foid
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Options;
    using Microsoft.Net.Http.Headers;
    using static Contract;

    internal sealed class HostProvider : IHostProvider
    {
        private static readonly object HostKey = new object();
        private readonly IOptionsMonitor<FoidOptions> options;

        public HostProvider(IOptionsMonitor<FoidOptions> options)
        {
            this.options = CheckValue(options, nameof(options));
        }

        public string GetHost(HttpContext context)
        {
            if (context.Items.TryGetValue(HostKey, out var existingHost))
                return (string)existingHost;

            var headersOptions = options.CurrentValue.Proxy.Upstream.Request.Headers;
            if (headersOptions.IgnoreAllDownstreamRequestHeaders)
                return CreateHost(context);

            if (!context.Request.Headers.TryGetValue(HeaderNames.Host, out var values) || values.Count == 0)
                return CreateHost(context);

            // If the HOST header includes a PORT number, remove the port number
            var host = GetHostWithoutPortNumber(values[0]);

            context.Items.Add(HostKey, host);
            return host;
        }

        private string CreateHost(HttpContext context)
        {
            var host = options.CurrentValue.Proxy.Upstream.Request.Headers.DefaultHost;
            context.Items.Add(HostKey, host);
            return host;
        }

        private static string GetHostWithoutPortNumber(string host)
        {
            var portIndex = host.LastIndexOf(':');
            return portIndex == -1 ? host : host.Substring(0, portIndex);
        }
    }
}
