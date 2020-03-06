﻿namespace Cloudtoid.Foid.Proxy
{
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;
    using Cloudtoid;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using static Contract;

    internal sealed class ProxyMiddleware
    {
        private readonly RequestDelegate next;
        private readonly IRequestCreator requestCreator;
        private readonly IRequestSender sender;
        private readonly ProxyConfig config;
        private readonly ILogger<ProxyMiddleware> logger;

        public ProxyMiddleware(
            RequestDelegate next,
            IRequestCreator requestCreator,
            IRequestSender sender,
            ProxyConfig config,
            ILogger<ProxyMiddleware> logger)
        {
            this.next = CheckValue(next, nameof(next));
            this.requestCreator = CheckValue(requestCreator, nameof(requestCreator));
            this.sender = CheckValue(sender, nameof(sender));
            this.config = CheckValue(config, nameof(config));
            this.logger = CheckValue(logger, nameof(logger));
        }

        [SuppressMessage("Style", "VSTHRD200:Use Async suffix for async methods", Justification = "Implementing an ASP.NET middleware. This signature cannot be changed.")]
        public async Task Invoke(HttpContext context)
        {
            CheckValue(context, nameof(context));

            logger.LogDebug("Reverse proxy received a new incoming HTTP {0} request.", context.Request.Method);

            // TODO: What error should we send back if any of the stuff below fail?

            var cancellationToken = context.RequestAborted;
            cancellationToken.ThrowIfCancellationRequested();

            var request = await requestCreator
                .CreateRequestAsync(context)
                .TraceOnFaulted(logger, "Failed to create an outgoing upstream HTTP request message", cancellationToken);

            var response = await Async
                .WithTimeout(sender.SendAsync, request, config.Values.TotalTimeout, cancellationToken)
                .TraceOnFaulted(logger, "Failed to forward the HTTP request to the upstream system.", cancellationToken);

            await next.Invoke(context);
        }
    }
}
