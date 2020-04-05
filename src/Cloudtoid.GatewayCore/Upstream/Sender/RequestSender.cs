﻿namespace Cloudtoid.GatewayCore.Upstream
{
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using static Contract;

    internal sealed class RequestSender : IRequestSender
    {
        private readonly IRequestSenderHttpClientFactory httpClientFactory;

        public RequestSender(IRequestSenderHttpClientFactory httpClientFactory)
        {
            this.httpClientFactory = CheckValue(httpClientFactory, nameof(httpClientFactory));
        }

        public async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage upstreamMessage,
            CancellationToken cancellationToken)
        {
            CheckValue(upstreamMessage, nameof(upstreamMessage));

            cancellationToken.ThrowIfCancellationRequested();

            var client = httpClientFactory.CreateClient();
            return await client.SendAsync(upstreamMessage, cancellationToken);
        }
    }
}
