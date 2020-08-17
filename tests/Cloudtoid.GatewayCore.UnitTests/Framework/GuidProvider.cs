﻿using System;

namespace Cloudtoid.GatewayCore.UnitTests
{
    internal sealed class GuidProvider : IGuidProvider
    {
        private GuidProvider()
        {
        }

#pragma warning disable RS0030 // Do not used banned APIs
        internal static Guid Value { get; } = Guid.NewGuid();
#pragma warning restore RS0030 // Do not used banned APIs

        internal static string StringValue => Value.ToStringInvariant("N");

        internal static IGuidProvider Instance { get; } = new GuidProvider();

        public Guid NewGuid() => Value;
    }
}
