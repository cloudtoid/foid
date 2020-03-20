﻿namespace Cloudtoid.Foid.Proxy
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class UriRewriter : IUriRewriter
    {
        public Task<Uri> RewriteUriAsync(CallContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException($"Please implement {nameof(IUriRewriter)} and register it with DI as a transient object.");
    }
}