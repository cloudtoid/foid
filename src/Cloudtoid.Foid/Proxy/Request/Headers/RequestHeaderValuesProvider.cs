﻿namespace Cloudtoid.Foid.Proxy
{
    using System.Collections.Generic;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Options;
    using Microsoft.Net.Http.Headers;
    using static Contract;

    public class RequestHeaderValuesProvider : IRequestHeaderValuesProvider
    {
        public RequestHeaderValuesProvider(IOptionsMonitor<FoidOptions> options)
        {
            Options = CheckValue(options, nameof(options));
        }

        public virtual bool AllowHeadersWithEmptyValue => HeaderOptions.AllowHeadersWithEmptyValue;

        public virtual bool AllowHeadersWithUnderscoreInName => HeaderOptions.AllowHeadersWithUnderscoreInName;

        public virtual bool IncludeExternalAddress => HeaderOptions.IncludeExternalAddress;

        public virtual bool IgnoreClientAddress => HeaderOptions.IgnoreClientAddress;

        public virtual bool IgnoreClientProtocol => HeaderOptions.IgnoreClientProtocol;

        public virtual bool IgnoreRequestId => HeaderOptions.IgnoreRequestId;

        public virtual bool IgnoreCallId => HeaderOptions.IgnoreCallId;

        protected IOptionsMonitor<FoidOptions> Options { get; }

        private FoidOptions.ProxyOptions.UpstreamOptions.RequestOptions.HeadersOptions HeaderOptions
            => Options.CurrentValue.Proxy.Upstream.Request.Headers;

        public virtual bool TryGetHeaderValues(
            HttpContext context,
            string name,
            IList<string> downstreamValues,
            out IList<string> upstreamValues)
        {
            CheckValue(context, nameof(context));
            CheckNonEmpty(name, nameof(name));

            if (name.EqualsOrdinalIgnoreCase(HeaderNames.Host))
            {
                upstreamValues = new[] { GetHostHeaderValue(context, downstreamValues) };
                return true;
            }

            upstreamValues = downstreamValues;
            return true;
        }

        public virtual string GetDefaultHostHeaderValue(HttpContext context)
            => HeaderOptions.DefaultHost;

        public virtual string? GetProxyNameHeaderValue(HttpContext context)
            => HeaderOptions.ProxyName;

        public virtual IEnumerable<ExtraHeader> GetExtraHeaders(HttpContext context)
            => HeaderOptions.ExtraHeaders;

        private string GetHostHeaderValue(HttpContext context, IList<string> downstreamValues)
        {
            // No value, just return the machine name as the host name
            if (downstreamValues.Count == 0)
                return GetDefaultHostHeaderValue(context);

            // If the HOST header includes a PORT number, remove the port number
            var host = downstreamValues[0];
            var portIndex = host.LastIndexOf(':');

            return portIndex == -1 ? host : host.Substring(0, portIndex);
        }
    }
}
