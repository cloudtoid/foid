﻿using System.Collections.Generic;
using Cloudtoid.GatewayCore.Settings;

namespace Cloudtoid.GatewayCore
{
    public sealed class Route
    {
        internal Route(
            RouteSettings settings,
            string pathSuffix,
            IReadOnlyDictionary<string, string> variables)
        {
            Settings = settings;
            PathSuffix = pathSuffix;
            Variables = variables;
        }

        public RouteSettings Settings { get; }

        /// <summary>
        /// Gets the suffix portion of the URL path that was not matched to the pattern and should be added to the outbound upstream request.
        /// </summary>
        public string PathSuffix { get; }

        /// <summary>
        /// Gets the variables and their values extracted from the route pattern and the inbound URL path respectively.
        /// </summary>
        public IReadOnlyDictionary<string, string> Variables { get; }
    }
}
