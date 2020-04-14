﻿namespace Cloudtoid.GatewayCore.Upstream
{
    using System.Diagnostics.CodeAnalysis;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text;
    using Cloudtoid.GatewayCore.Headers;

    public partial class RequestHeaderSetter
    {
        protected virtual void AddForwardedHeaders(ProxyContext context, HttpRequestMessage upstreamRequest)
        {
            if (context.ProxyUpstreamRequestHeadersSettings.IgnoreForwarded)
                return;

            if (context.ProxyUpstreamRequestHeadersSettings.UseXForwarded)
            {
                AddXForwardedHeaders(context, upstreamRequest);
                return;
            }

            AddForwardedHeader(context, upstreamRequest);
        }

        private void AddXForwardedHeaders(ProxyContext context, HttpRequestMessage upstreamRequest)
        {
            AddXForwardedForHeader(context, upstreamRequest);
            AddXForwardedProtocolHeader(context, upstreamRequest);
            AddXForwardedHostHeader(context, upstreamRequest);
        }

        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Forwarded
        protected virtual void AddForwardedHeader(ProxyContext context, HttpRequestMessage upstreamRequest)
        {
            var by = CreateValidForwardedIpAddress(context.HttpContext.Connection.LocalIpAddress);
            var @for = CreateValidForwardedIpAddress(context.HttpContext.Connection.RemoteIpAddress);
            var host = context.Request.Host;
            var proto = context.Request.Scheme;
            var value = CreateForwardHeaderValue(by, @for, host.Value, proto);

            if (value is null)
                return;

            AddHeaderValues(
                context,
                upstreamRequest,
                Names.Forwarded,
                value);
        }

        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/X-Forwarded-For
        protected virtual void AddXForwardedForHeader(ProxyContext context, HttpRequestMessage upstreamRequest)
        {
            var forAddress = GetRemoteIpAddressOrDefault(context);
            if (string.IsNullOrEmpty(forAddress))
                return;

            AddHeaderValues(
                context,
                upstreamRequest,
                Names.ForwardedFor,
                forAddress);
        }

        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/X-Forwarded-Proto
        protected virtual void AddXForwardedProtocolHeader(ProxyContext context, HttpRequestMessage upstreamRequest)
        {
            if (string.IsNullOrEmpty(context.Request.Scheme))
                return;

            AddHeaderValues(
                context,
                upstreamRequest,
                Names.ForwardedProtocol,
                context.Request.Scheme);
        }

        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/X-Forwarded-Host
        protected virtual void AddXForwardedHostHeader(ProxyContext context, HttpRequestMessage upstreamRequest)
        {
            var host = context.Request.Host;
            if (!host.HasValue)
                return;

            AddHeaderValues(
                context,
                upstreamRequest,
                Names.ForwardedHost,
                host.Value);
        }

        private static string? CreateForwardHeaderValue(
            string? by,
            string? @for,
            string? host,
            string? proto)
        {
            var builder = new StringBuilder();

            if (!string.IsNullOrEmpty(by))
            {
                builder.Append(ForwardedBy);
                builder.Append(by);
            }

            if (!string.IsNullOrEmpty(@for))
            {
                builder.AppendIfNotEmpty(Semicolon);
                builder.Append(ForwardedFor);
                builder.Append(@for);
            }

            if (!string.IsNullOrEmpty(host))
            {
                builder.AppendIfNotEmpty(Semicolon);
                builder.Append(ForwardedHost);
                builder.Append(host);
            }

            if (!string.IsNullOrEmpty(proto))
            {
                builder.AppendIfNotEmpty(Semicolon);
                builder.Append(ForwardedProto);
                builder.Append(proto);
            }

            return builder.Length == 0 ? null : builder.ToString();
        }

        [return: NotNullIfNotNull("ip")]
        private static string? CreateValidForwardedIpAddress(IPAddress? ip)
        {
            if (ip is null)
                return null;

            return ip.AddressFamily == AddressFamily.InterNetworkV6
                ? $"\"[{ip}]\""
                : ip.ToString();
        }
    }
}
