﻿namespace Cloudtoid.Foid.Proxy
{
    using System.Collections.Generic;
    using Microsoft.AspNetCore.Http;

    public interface IRequestHeaderValuesProvider
    {
        /// <summary>
        /// By default, headers with an empty value are dropped.
        /// </summary>
        bool AllowHeadersWithEmptyValue { get; }

        /// <summary>
        /// By default, headers with an underscore in their names are dropped.
        /// </summary>
        bool AllowHeadersWithUnderscoreInName { get; }

        /// <summary>
        /// By implementing this method, one can change the values of a given header.
        /// Return false, if the header should be dropped.
        /// </summary>
        bool TryGetHeaderValues(HttpContext context, string name, IList<string> downstreamValues, out IList<string> upstreamValues);

        /// <summary>
        /// If the incoming downstream request does not have a HOST header, the value provided here will be used.
        /// </summary>
        string GetDefaultHostHeaderValue(HttpContext context);
    }
}
