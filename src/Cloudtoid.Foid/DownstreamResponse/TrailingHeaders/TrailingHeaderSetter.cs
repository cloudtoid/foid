﻿namespace Cloudtoid.Foid.Proxy
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Cloudtoid.Foid.Headers;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using static Contract;

    /// <summary>
    /// By inheriting from this class, one can have full control over the outbound downstream response trailing headers. Please consider the following extensibility points:
    /// <list type="number">
    /// <item><description>Inherit from <see cref="TrailingHeaderValuesProvider"/>, override its methods, and register it with DI; or</description></item>
    /// <item><description>Implement <see cref="ITrailingHeaderValuesProvider"/> and register it with DI; or</description></item>
    /// <item><description>Inherit from <see cref="ResponseHeaderSetter"/>, override its methods, and register it with DI; or</description></item>
    /// <item><description>Implement <see cref="IResponseHeaderSetter"/> and register it with DI</description></item>
    /// </list>
    /// </summary>
    /// <example>
    /// Dependency Injection registrations:
    /// <list type="bullet">
    /// <item><description><c>TryAddSingleton&lt;<see cref="ITrailingHeaderValuesProvider"/>, MyTrailingHeaderValuesProvider&gt;()</c></description></item>
    /// <item><description><c>TryAddSingleton&lt;<see cref="IResponseHeaderSetter"/>, MyResponseHeaderSetter&gt;()</c></description></item>
    /// </list>
    /// </example>
    public class TrailingHeaderSetter : ITrailingHeaderSetter
    {
        private static readonly ISet<string> HeaderTransferBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Names.CallId,
        };

        private readonly HeaderSanetizer sanetizer;

        public TrailingHeaderSetter(
            ITrailingHeaderValuesProvider provider,
            ILogger<TrailingHeaderSetter> logger)
        {
            Provider = CheckValue(provider, nameof(provider));
            Logger = CheckValue(logger, nameof(logger));
            sanetizer = new HeaderSanetizer(logger);
        }

        protected ITrailingHeaderValuesProvider Provider { get; }

        protected ILogger<TrailingHeaderSetter> Logger { get; }

        public virtual Task SetHeadersAsync(
            ProxyContext context,
            HttpResponseMessage upstreamResponse,
            CancellationToken cancellationToken)
        {
            CheckValue(context, nameof(context));
            CheckValue(upstreamResponse, nameof(upstreamResponse));

            var options = context.ProxyDownstreamResponseHeaderOptions;
            if (options.IgnoreAllUpstreamHeaders)
                return Task.CompletedTask;

            cancellationToken.ThrowIfCancellationRequested();

            if (upstreamResponse.TrailingHeaders is null)
                return Task.CompletedTask;

            if (!ResponseTrailerExtensions.SupportsTrailers(context.Response))
                return Task.CompletedTask;

            var allowHeadersWithEmptyValue = options.AllowHeadersWithEmptyValue;
            var allowHeadersWithUnderscoreInName = options.AllowHeadersWithUnderscoreInName;
            var headers = upstreamResponse.TrailingHeaders;

            foreach (var header in headers)
            {
                if (!sanetizer.IsValid(
                    header.Key,
                    header.Value,
                    allowHeadersWithEmptyValue,
                    allowHeadersWithUnderscoreInName))
                    continue;

                AddHeaderValues(context, header.Key, header.Value.AsArray());
            }

            return Task.CompletedTask;
        }

        protected virtual void AddHeaderValues(
            ProxyContext context,
            string name,
            params string[] upstreamValues)
        {
            if (Provider.TryGetHeaderValues(context, name, upstreamValues, out var downstreamValues) && downstreamValues != null)
            {
                context.Response.AppendTrailer(name, downstreamValues);
                return;
            }

            Logger.LogInformation(
                "Header '{0}' is not added. This was instructed by the {1}.{2}.",
                name,
                nameof(ITrailingHeaderValuesProvider),
                nameof(ITrailingHeaderValuesProvider.TryGetHeaderValues));
        }
    }
}
