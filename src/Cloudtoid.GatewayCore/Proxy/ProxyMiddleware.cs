﻿namespace Cloudtoid.GatewayCore.Proxy
{
    using System.Diagnostics.CodeAnalysis;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Cloudtoid;
    using Cloudtoid.GatewayCore.Downstream;
    using Cloudtoid.GatewayCore.Routes;
    using Cloudtoid.GatewayCore.Trace;
    using Cloudtoid.GatewayCore.Upstream;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using static Contract;

    internal sealed class ProxyMiddleware
    {
        private readonly RequestDelegate next;
        private readonly IRequestCreator requestCreator;
        private readonly IRequestSender sender;
        private readonly IResponseSender responseSender;
        private readonly IRouteResolver routeResolver;
        private readonly ITraceIdProvider traceIdProvider;
        private readonly ILogger<ProxyMiddleware> logger;

        public ProxyMiddleware(
            RequestDelegate next,
            IRequestCreator requestCreator,
            IRequestSender sender,
            IResponseSender responseSender,
            IRouteResolver routeResolver,
            ITraceIdProvider traceIdProvider,
            ILogger<ProxyMiddleware> logger)
        {
            this.next = CheckValue(next, nameof(next));
            this.requestCreator = CheckValue(requestCreator, nameof(requestCreator));
            this.sender = CheckValue(sender, nameof(sender));
            this.responseSender = CheckValue(responseSender, nameof(responseSender));
            this.routeResolver = CheckValue(routeResolver, nameof(routeResolver));
            this.traceIdProvider = CheckValue(traceIdProvider, nameof(traceIdProvider));
            this.logger = CheckValue(logger, nameof(logger));
        }

        [SuppressMessage("Style", "VSTHRD200:Use Async suffix for async methods", Justification = "Implementing an ASP.NET middleware. This signature cannot be changed.")]
        public async Task Invoke(HttpContext httpContext)
        {
            CheckValue(httpContext, nameof(httpContext));

            httpContext.RequestAborted.ThrowIfCancellationRequested();

            logger.LogDebug("Reverse proxy received a new inbound downstream HTTP '{0}' request.", httpContext.Request.Method);

            await ProxyAsync(httpContext);
            ////await next.Invoke(httpContext);
        }

        private async Task ProxyAsync(HttpContext httpContext)
        {
            if (!routeResolver.TryResolve(httpContext, out var route))
                return;

            logger.LogDebug(
                "Reverse proxy found a matching route for the inbound downstream HTTP '{0}' request.",
                httpContext.Request.Method);

            var context = new ProxyContext(
                traceIdProvider,
                httpContext,
                route);

            var cancellationToken = httpContext.RequestAborted;
            var upstreamRequest = await CreateUpstreamRequestAsync(context, cancellationToken);
            var upstreamResponse = await SendUpstreamRequestAsync(context, upstreamRequest, cancellationToken);
            await SendDownstreamResponseAsync(context, upstreamResponse, cancellationToken);
        }

        private async Task<HttpRequestMessage> CreateUpstreamRequestAsync(
            ProxyContext context,
            CancellationToken cancellationToken)
        {
            return await requestCreator
                .CreateRequestAsync(context, cancellationToken)
                .TraceOnFaulted(logger, "Failed to create an outbound upstream request.", cancellationToken);
        }

        private async Task<HttpResponseMessage> SendUpstreamRequestAsync(
            ProxyContext context,
            HttpRequestMessage upstreamRequest,
            CancellationToken cancellationToken)
        {
            try
            {
                return await sender
                    .SendAsync(context, upstreamRequest, cancellationToken)
                    .TraceOnFaulted(logger, "Failed to forward the request to the upstream system.", cancellationToken);
            }
            catch (HttpRequestException hre)
            {
                // Bad gateway (HTTP status 502) indicates that this proxy server received a bad
                // response from another proxy or the origin server.
                throw new ProxyException(HttpStatusCode.BadGateway, hre);
            }
        }

        private async Task SendDownstreamResponseAsync(
            ProxyContext context,
            HttpResponseMessage upstreamResponse,
            CancellationToken cancellationToken)
        {
            await responseSender
                .SendResponseAsync(context, upstreamResponse, cancellationToken)
                .TraceOnFaulted(logger, "Failed to create and send the downstream response message.", cancellationToken);
        }
    }
}
