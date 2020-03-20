﻿namespace Cloudtoid.Foid.Expression
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using static Contract;
    using Cache = System.Collections.Generic.IReadOnlyDictionary<string, ExpressionEvaluator.ParsedExpression>;
    using VariableEvaluator = System.Func<CallContext, string?>;

    internal sealed class ExpressionEvaluator : IExpressionEvaluator
    {
        private static readonly VariableTrie<VariableEvaluator> VariableEvaluatorTrie = BuildTrie();
        private Cache cache = new Dictionary<string, ParsedExpression>(0, StringComparer.Ordinal);

        public string Evaluate(CallContext context, string expression)
        {
            CheckValue(context, nameof(context));
            CheckValue(expression, nameof(expression));
            return EvaluateCore(context, expression);
        }

        private string EvaluateCore(CallContext context, string expression)
        {
            if (!cache.TryGetValue(expression, out var parsedExpression))
            {
                Cache snapshot, newCache;

                parsedExpression = Parse(expression);
                do
                {
                    snapshot = cache;
                    if (snapshot.ContainsKey(expression))
                        break;

                    newCache = new Dictionary<string, ParsedExpression>(snapshot, StringComparer.Ordinal)
                    {
                        { expression, parsedExpression }
                    };
                }
                while (Interlocked.CompareExchange(ref cache, newCache, snapshot) != snapshot);
            }

            return parsedExpression.Evaluate(context);
        }

        /// <summary>
        /// "Content-Length" request header field
        /// </summary>
        private static string? GetContentLength(CallContext context)
            => context.Request.ContentLength?.ToStringInvariant();

        /// <summary>
        /// "Content-Type" request header field
        /// </summary>
        private static string? GetContentType(CallContext context)
            => context.Request.ContentType;

        /// <summary>
        /// The value correlation identifier header if not present or a newly generated one.
        /// </summary>
        private static string? GetCorrelationId(CallContext context)
            => context.CorrelationId;

        /// <summary>
        /// The value correlation identifier header if not present or a newly generated one.
        /// The default header name of correlation identifier is "x-correlation-id" but this can be changed
        /// using the CorrelationIdHeader option.
        /// </summary>
        private static string? GetCallId(CallContext context)
            => context.CallId;

        /// <summary>
        /// The value that should be used as the HOST header on the outbound upstream request.
        /// </summary>
        private static string? GetHost(CallContext context)
            => context.Host;

        /// <summary>
        /// The HTTP method of the inbound downstream request
        /// </summary>
        private static string? GetRequestMethod(CallContext context)
            => context.Request.Method;

        /// <summary>
        /// The scheme (HTTP or HTTPS) used by the inbound downstream request
        /// </summary>
        private static string? GetRequestScheme(CallContext context)
            => context.Request.Scheme;

        /// <summary>
        /// The unescaped path base value.
        /// This is identical to <see cref="HttpRequest.PathBase"/>
        /// </summary>
        private static string? GetRequestPathBase(CallContext context)
            => context.Request.PathBase.Value;

        /// <summary>
        /// The unescaped path value.
        /// This is identical to <see cref="HttpRequest.Path"/>
        /// </summary>
        private static string? GetRequestPath(CallContext context)
            => context.Request.Path.Value;

        /// <summary>
        /// The escaped query string with the leading '?' character.
        /// This is identical to <see cref="HttpRequest.QueryString"/>
        /// </summary>
        private static string? GetRequestQueryString(CallContext context)
            => context.Request.QueryString.Value;

        /// <summary>
        /// The original escaped request URI including the query string portion.
        /// scheme + host + path-base + path + query-string
        /// This is identical to <see cref="UriHelper.GetEncodedUrl(HttpRequest)"/>.
        /// </summary>
        private static string? GetRequestEncodedUri(CallContext context)
            => context.Request.GetEncodedUrl();

        /// <summary>
        /// The IP address of the client
        /// </summary>
        private static string? GetRemoteAddress(CallContext context)
            => context.HttpContext.Connection?.RemoteIpAddress?.ToString();

        /// <summary>
        /// The IP port number of the remote client.
        /// </summary>
        private static string? GetRemotePort(CallContext context)
            => context.HttpContext.Connection?.RemotePort.ToStringInvariant();

        /// <summary>
        /// The IP address of the server which accepted the request
        /// </summary>
        private static string? GetServerAddress(CallContext context)
            => context.HttpContext.Connection?.LocalIpAddress?.ToString();

        /// <summary>
        /// The IP port number of the server which accepted the request
        /// </summary>
        private static string? GetServerPort(CallContext context)
            => context.HttpContext.Connection?.LocalPort.ToStringInvariant();

        /// <summary>
        /// The name of the server which accepted the request
        /// </summary>
        private static string? GetServerName(CallContext context)
            => Environment.MachineName;

        /// <summary>
        /// The protocol of the inbound downstream request, usually “HTTP/1.0”, “HTTP/1.1”, or “HTTP/2.0”
        /// </summary>
        private static string? GetServerProtocol(CallContext context)
            => context.Request.Protocol;

        private static ParsedExpression Parse(string expression)
        {
            var instructions = new List<dynamic>();
            int index = 0;
            int len = expression.Length;
            var sb = new StringBuilder(expression.Length);

            while (index < len)
            {
                var c = expression[index++];

                if (c != '$')
                {
                    sb.Append(c);
                    continue;
                }

                int varNameStartIndex = index;
                while (index < len)
                {
                    if (!VariableNames.IsValidVariableChar(expression[index]))
                        break;

                    index++;
                }

                if (varNameStartIndex == index)
                {
                    sb.AppendDollar();
                    continue;
                }

                var varName = expression[varNameStartIndex..index];
                if (!VariableEvaluatorTrie.TryGetBestMatch(varName, out var varEvaluator, out var lengthMatched))
                {
                    sb.AppendDollar().Append(varName);
                    continue;
                }

                index -= varName.Length - lengthMatched;
                if (sb.Length > 0)
                {
                    var str = sb.ToString();
                    instructions.Add(str);
                    sb.Clear();
                }

                instructions.Add(varEvaluator);
            }

            if (sb.Length > 0)
            {
                var str = sb.ToString();
                instructions.Add(str);
            }

            return new ParsedExpression(instructions);
        }

        private static VariableTrie<VariableEvaluator> BuildTrie()
        {
            return new VariableTrie<VariableEvaluator>()
                .AddValue(VariableNames.ContentLength, GetContentLength)
                .AddValue(VariableNames.ContentType, GetContentType)
                .AddValue(VariableNames.CorrelationId, GetCorrelationId)
                .AddValue(VariableNames.CallId, GetCallId)
                .AddValue(VariableNames.Host, GetHost)
                .AddValue(VariableNames.RequestMethod, GetRequestMethod)
                .AddValue(VariableNames.RequestScheme, GetRequestScheme)
                .AddValue(VariableNames.RequestPathBase, GetRequestPathBase)
                .AddValue(VariableNames.RequestPath, GetRequestPath)
                .AddValue(VariableNames.RequestQueryString, GetRequestQueryString)
                .AddValue(VariableNames.RequestEncodedUri, GetRequestEncodedUri)
                .AddValue(VariableNames.RemoteAddress, GetRemoteAddress)
                .AddValue(VariableNames.RemotePort, GetRemotePort)
                .AddValue(VariableNames.ServerAddress, GetServerAddress)
                .AddValue(VariableNames.ServerName, GetServerName)
                .AddValue(VariableNames.ServerPort, GetServerPort)
                .AddValue(VariableNames.ServerProtocol, GetServerProtocol);
        }

        internal struct ParsedExpression
        {
            private readonly IList<dynamic> instructions;

            internal ParsedExpression(IList<dynamic> instructions)
            {
                this.instructions = instructions;
            }

            internal string Evaluate(CallContext context)
            {
                if (instructions.Count == 0)
                    return string.Empty;

                var sb = new StringBuilder();
                foreach (var instruction in instructions)
                {
                    if (instruction is string value)
                    {
                        sb.Append(value);
                        continue;
                    }

                    sb.Append(instruction(context));
                }

                return sb.ToString();
            }
        }
    }
}
