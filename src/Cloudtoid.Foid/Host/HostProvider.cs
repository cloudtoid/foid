﻿namespace Cloudtoid.Foid.Host
{
    using static Contract;

    /// <summary>
    /// By inheriting from this class, one can override the HOST header of the outbound upstream request.
    /// You can also implement <see cref="IHostProvider"/> and register it with DI.
    /// </summary>
    public class HostProvider : IHostProvider
    {
        public virtual string GetHost(CallContext context)
        {
            CheckValue(context, nameof(context));

            if (context.ProxyUpstreamRequestHeadersOptions.IgnoreAllDownstreamHeaders)
                return GetDefaultHost(context);

            var hostHeader = context.Request.Host;
            if (!hostHeader.HasValue)
                return GetDefaultHost(context);

            // If the HOST header includes a PORT number, remove the port number
            // This matches NGINX's implementation
            return GetHostWithoutPortNumber(hostHeader.Value);
        }

        private string GetDefaultHost(CallContext context)
            => context.ProxyUpstreamRequestHeadersOptions.GetDefaultHost(context);

        private static string GetHostWithoutPortNumber(string host)
        {
            var portIndex = host.LastIndexOf(':');
            return portIndex == -1 ? host : host.Substring(0, portIndex);
        }
    }
}
