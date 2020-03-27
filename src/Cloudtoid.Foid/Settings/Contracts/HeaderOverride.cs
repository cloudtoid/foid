﻿namespace Cloudtoid.Foid.Settings
{
    using System.Collections.Generic;
    using System.Linq;

    public sealed class HeaderOverride
    {
        private readonly RouteSettingsContext context;
        private readonly string[] values;

        internal HeaderOverride(
            RouteSettingsContext context,
            string name,
            string[] values)
        {
            this.context = context;
            Name = name;
            this.values = values;
        }

        public string Name { get; }

        public bool HasValues => values.Length > 0;

        public IEnumerable<string> GetValues(ProxyContext proxyContext)
            => values.Select(v => context.Evaluate(proxyContext, v));
    }
}