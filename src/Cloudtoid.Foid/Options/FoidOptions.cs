﻿namespace Cloudtoid.Foid
{
    using System;

    public sealed class FoidOptions
    {
        public ProxyOptions Proxy { get; set; } = new ProxyOptions();

        public sealed class ProxyOptions
        {
            public UpstreamOptions Upstream { get; set; } = new UpstreamOptions();

            public DownstreamOptions Downstream { get; set; } = new DownstreamOptions();

            public sealed class UpstreamOptions
            {
                public RequestOptions Request { get; set; } = new RequestOptions();

                public sealed class RequestOptions
                {
                    /// <summary>
                    /// This is the total timeout in milliseconds to wait for the outbound upstream proxy call to complete
                    /// </summary>
                    public string? TimeoutInMilliseconds { get; set; }

                    public HeadersOptions Headers { get; set; } = new HeadersOptions();

                    public sealed class HeadersOptions
                    {
                        /// <summary>
                        /// By default, headers with an empty value are dropped.
                        /// </summary>
                        public bool AllowHeadersWithEmptyValue { get; set; }

                        /// <summary>
                        /// By default, headers with an underscore in their names are dropped.
                        /// </summary>
                        public bool AllowHeadersWithUnderscoreInName { get; set; }

                        /// <summary>
                        /// If true, an "x-foid-external-address" header with the immediate downstream IP address is added to the outgoing upstream call.
                        /// The default value is <c>false</c>.
                        /// </summary>
                        public bool IncludeExternalAddress { get; set; }

                        /// <summary>
                        /// If false, it will copy all the headers from the incoming downstream request to the outgoing upstream request.
                        /// The default value is <c>false</c>.
                        /// </summary>
                        public bool IgnoreAllDownstreamRequestHeaders { get; set; }

                        /// <summary>
                        /// If false, it will append a host header to the outgoing upstream request.
                        /// The default value is <c>false</c>.
                        /// </summary>
                        public bool IgnoreHost { get; set; }

                        /// <summary>
                        /// If false, it will append the IP address of the nearest client to the "x-forwarded-for" header.
                        /// The default value is <c>false</c>.
                        /// </summary>
                        public bool IgnoreClientAddress { get; set; }

                        /// <summary>
                        /// If false, it will append the client protocol (HTTP or HTTPS) to the "x-forwarded-proto" header.
                        /// The default value is <c>false</c>.
                        /// </summary>
                        public bool IgnoreClientProtocol { get; set; }

                        /// <summary>
                        /// If false, it will append a correlation identifier header if not present. The actual header name is defined by <see cref="CorrelationIdHeader"/>
                        /// The default value is <c>false</c>.
                        /// </summary>
                        public bool IgnoreCorrelationId { get; set; }

                        /// <summary>
                        /// If false, it will append a "x-call-id" header. This is a guid that is always new for each call.
                        /// The default value is <c>false</c>.
                        /// </summary>
                        public bool IgnoreCallId { get; set; }

                        /// <summary>
                        /// Header name for correlation identifier.
                        /// The default value is "x-correlation-id".
                        /// </summary>
                        public string? CorrelationIdHeader { get; set; }

                        /// <summary>
                        /// If the incoming downstream request does not have a HOST header, the value provided here will be used.
                        /// </summary>
                        public string? DefaultHost { get; set; }

                        /// <summary>
                        /// If this is not empty, an "x-foid-proxy-name" header with this value is added to the outgoing upstream call.
                        /// </summary>
                        public string? ProxyName { get; set; }

                        /// <summary>
                        /// Extra headers to be appended to the outgoing downstream response. If a header already exists, it is replaced with the new value.
                        /// </summary>
                        public ExtraHeader[] Headers { get; set; } = Array.Empty<ExtraHeader>();
                    }
                }
            }

            public sealed class DownstreamOptions
            {
                public ResponseOptions Response { get; set; } = new ResponseOptions();

                public sealed class ResponseOptions
                {
                    public HeadersOptions Headers { get; set; } = new HeadersOptions();

                    public sealed class HeadersOptions
                    {
                        /// <summary>
                        /// By default, headers with an empty value are dropped.
                        /// </summary>
                        public bool AllowHeadersWithEmptyValue { get; set; }

                        /// <summary>
                        /// By default, headers with an underscore in their names are dropped.
                        /// </summary>
                        public bool AllowHeadersWithUnderscoreInName { get; set; }

                        /// <summary>
                        /// If false, it will copy all headers from the incoming upstream response to the outgoing downstream response.
                        /// The default value is <c>false</c>.
                        /// </summary>
                        public bool IgnoreAllUpstreamResponseHeaders { get; set; }

                        /// <summary>
                        /// Extra headers to be appended to the outgoing downstream response. If a header already exists, it is replaced with the new value.
                        /// </summary>
                        public ExtraHeader[] Headers { get; set; } = Array.Empty<ExtraHeader>();
                    }
                }
            }

            public class ExtraHeader
            {
                public string? Name { get; set; }

                public string[]? Values { get; set; }
            }
        }
    }
}