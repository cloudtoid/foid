﻿namespace Cloudtoid.Foid.Routes
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using Cloudtoid.Foid.Settings;
    using Microsoft.AspNetCore.Http;

    public interface IRouteProvider : IReadOnlyCollection<RouteSettings>
    {
        bool TryGetRoute(
            HttpContext context,
            [NotNullWhen(true)] out Route? route);
    }
}
