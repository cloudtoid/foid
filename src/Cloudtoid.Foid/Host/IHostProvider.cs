﻿namespace Cloudtoid.Foid
{
    using Microsoft.AspNetCore.Http;

    public interface IHostProvider
    {
        /// <summary>
        /// Returns the value that should be used as the HOST header on the outgoing upstream request
        /// </summary>
        string GetHost(HttpContext context);
    }
}
