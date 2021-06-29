﻿using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static Cloudtoid.Contract;

namespace Cloudtoid.GatewayCore.Upstream
{
    internal sealed class RequestSender : IRequestSender
    {
        private readonly IHttpClientFactory httpClientFactory;

        public RequestSender(IHttpClientFactory httpClientFactory)
        {
            this.httpClientFactory = CheckValue(httpClientFactory, nameof(httpClientFactory));
        }

        public async Task<HttpResponseMessage> SendAsync(
            ProxyContext context,
            HttpRequestMessage upstreamMessage,
            CancellationToken cancellationToken)
        {
            CheckValue(upstreamMessage, nameof(upstreamMessage));

            cancellationToken.ThrowIfCancellationRequested();

            var settings = context.ProxyUpstreamRequestSenderSettings;
            var client = httpClientFactory.CreateClient(settings.HttpClientName);
            client.Timeout = settings.EvaluateTimeout(context);
            return await client.SendAsync(upstreamMessage, cancellationToken);
        }
    }
}
