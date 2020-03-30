﻿namespace Cloudtoid.Foid.Expression
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using Cloudtoid.UrlPattern;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using static Contract;
    using Cache = System.Collections.Generic.IReadOnlyDictionary<string, ExpressionEvaluator.ParsedExpression>;
    using SystemVariableEvaluator = System.Func<ProxyContext, string?>;

    internal sealed class ExpressionEvaluator : IExpressionEvaluator
    {
        private static readonly VariableTrie<SystemVariableEvaluator> SystemVariablesTrie = BuildTrie();
        private Cache cache = new Dictionary<string, ParsedExpression>(0, StringComparer.Ordinal);

        public string Evaluate(ProxyContext context, string expression)
        {
            CheckValue(context, nameof(context));
            CheckValue(expression, nameof(expression));
            return EvaluateCore(context, expression);
        }

        private string EvaluateCore(ProxyContext context, string expression)
        {
            if (!cache.TryGetValue(expression, out var parsedExpression))
            {
                Cache snapshot, newCache;

                parsedExpression = Parse(context, expression);
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
        private static string? GetContentLength(ProxyContext context)
            => context.Request.ContentLength?.ToStringInvariant();

        /// <summary>
        /// "Content-Type" request header field
        /// </summary>
        private static string? GetContentType(ProxyContext context)
            => context.Request.ContentType;

        /// <summary>
        /// The value correlation identifier header if not present or a newly generated one.
        /// </summary>
        private static string? GetCorrelationId(ProxyContext context)
            => context.CorrelationId;

        /// <summary>
        /// The value correlation identifier header if not present or a newly generated one.
        /// The default header name of correlation identifier is "x-correlation-id" but this can be changed
        /// using the CorrelationIdHeader option.
        /// </summary>
        private static string? GetCallId(ProxyContext context)
            => context.CallId;

        /// <summary>
        /// The value that must be used as the HOST header on the outbound upstream request.
        /// </summary>
        private static string? GetHost(ProxyContext context)
            => context.Host;

        /// <summary>
        /// The HTTP method of the inbound downstream request
        /// </summary>
        private static string? GetRequestMethod(ProxyContext context)
            => context.Request.Method;

        /// <summary>
        /// The scheme (HTTP or HTTPS) used by the inbound downstream request
        /// </summary>
        private static string? GetRequestScheme(ProxyContext context)
            => context.Request.Scheme;

        /// <summary>
        /// The unescaped path base value.
        /// This is identical to <see cref="HttpRequest.PathBase"/>
        /// </summary>
        private static string? GetRequestPathBase(ProxyContext context)
            => context.Request.PathBase.Value;

        /// <summary>
        /// The unescaped path value.
        /// This is identical to <see cref="HttpRequest.Path"/>
        /// </summary>
        private static string? GetRequestPath(ProxyContext context)
            => context.Request.Path.Value;

        /// <summary>
        /// The escaped query string with the leading '?' character.
        /// This is identical to <see cref="HttpRequest.QueryString"/>
        /// </summary>
        private static string? GetRequestQueryString(ProxyContext context)
            => context.Request.QueryString.Value;

        /// <summary>
        /// The original escaped request URI including the query string portion.
        /// scheme + host + path-base + path + query-string
        /// This is identical to <see cref="UriHelper.GetEncodedUrl(HttpRequest)"/>.
        /// </summary>
        private static string? GetRequestEncodedUri(ProxyContext context)
            => context.Request.GetEncodedUrl();

        /// <summary>
        /// The IP address of the client
        /// </summary>
        private static string? GetRemoteAddress(ProxyContext context)
            => context.HttpContext.Connection?.RemoteIpAddress?.ToString();

        /// <summary>
        /// The IP port number of the remote client.
        /// </summary>
        private static string? GetRemotePort(ProxyContext context)
            => context.HttpContext.Connection?.RemotePort.ToStringInvariant();

        /// <summary>
        /// The IP address of the server which accepted the request
        /// </summary>
        private static string? GetServerAddress(ProxyContext context)
            => context.HttpContext.Connection?.LocalIpAddress?.ToString();

        /// <summary>
        /// The IP port number of the server which accepted the request
        /// </summary>
        private static string? GetServerPort(ProxyContext context)
            => context.HttpContext.Connection?.LocalPort.ToStringInvariant();

        /// <summary>
        /// The name of the server which accepted the request
        /// </summary>
        private static string? GetServerName(ProxyContext context)
            => Environment.MachineName;

        /// <summary>
        /// The protocol of the inbound downstream request, usually “HTTP/1.0”, “HTTP/1.1”, or “HTTP/2.0”
        /// </summary>
        private static string? GetServerProtocol(ProxyContext context)
            => context.Request.Protocol;

        /// <summary>
        /// Returns the value of a variable that is specified in the inbound downstream request.
        /// </summary>
        private static string? GetVariableValueFromRoute(ProxyContext context, string name)
            => context.Route.Variables.TryGetValue(name, out var value) ? value : null;

        private static ParsedExpression Parse(ProxyContext context, string expression)
        {
            var instructions = new List<dynamic>();
            int index = 0;
            int len = expression.Length;
            var sb = new StringBuilder(expression.Length);

            void ProcessLastSegment()
            {
                if (sb.Length == 0)
                    return;

                instructions.Add(sb.ToString());
                sb.Clear();
            }

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
                    if (!PatternVariables.IsValidVariableChar(expression[index], isFirstChar: varNameStartIndex == index))
                        break;

                    index++;
                }

                if (varNameStartIndex == index)
                {
                    sb.AppendDollar();
                    continue;
                }

                // Is it a system variable?
                var varName = expression[varNameStartIndex..index];
                if (SystemVariablesTrie.TryGetBestMatch(varName, out var varEvaluator, out var lengthMatched))
                {
                    ProcessLastSegment();
                    index -= varName.Length - lengthMatched;
                    instructions.Add(varEvaluator);
                    continue;
                }

                // Is it a variable extracted from the path section of the inbound downstream URL?
                if (context.Settings.VariableTrie.TryGetBestMatch(varName, out _, out lengthMatched))
                {
                    ProcessLastSegment();
                    index -= varName.Length - lengthMatched;
                    instructions.Add(new RouteVariable(varName));
                    continue;
                }

                sb.AppendDollar().Append(varName);
            }

            ProcessLastSegment();
            return new ParsedExpression(instructions);
        }

        private static VariableTrie<SystemVariableEvaluator> BuildTrie()
        {
            return new VariableTrie<SystemVariableEvaluator>()
                .AddValue(SystemVariableNames.ContentLength, GetContentLength)
                .AddValue(SystemVariableNames.ContentType, GetContentType)
                .AddValue(SystemVariableNames.CorrelationId, GetCorrelationId)
                .AddValue(SystemVariableNames.CallId, GetCallId)
                .AddValue(SystemVariableNames.Host, GetHost)
                .AddValue(SystemVariableNames.RequestMethod, GetRequestMethod)
                .AddValue(SystemVariableNames.RequestScheme, GetRequestScheme)
                .AddValue(SystemVariableNames.RequestPathBase, GetRequestPathBase)
                .AddValue(SystemVariableNames.RequestPath, GetRequestPath)
                .AddValue(SystemVariableNames.RequestQueryString, GetRequestQueryString)
                .AddValue(SystemVariableNames.RequestEncodedUri, GetRequestEncodedUri)
                .AddValue(SystemVariableNames.RemoteAddress, GetRemoteAddress)
                .AddValue(SystemVariableNames.RemotePort, GetRemotePort)
                .AddValue(SystemVariableNames.ServerAddress, GetServerAddress)
                .AddValue(SystemVariableNames.ServerName, GetServerName)
                .AddValue(SystemVariableNames.ServerPort, GetServerPort)
                .AddValue(SystemVariableNames.ServerProtocol, GetServerProtocol);
        }

        internal struct ParsedExpression
        {
            private readonly IList<dynamic> instructions;

            internal ParsedExpression(IList<dynamic> instructions)
            {
                this.instructions = instructions;
            }

            internal string Evaluate(ProxyContext context)
            {
                if (instructions.Count == 0)
                    return string.Empty;

                var sb = new StringBuilder();
                foreach (var instruction in instructions)
                {
                    if (instruction is string value)
                    {
                        // Just a string
                        sb.Append(value);
                    }
                    else if (instruction is RouteVariable routeVariable)
                    {
                        // The value of the variable found in the route
                        sb.Append(GetVariableValueFromRoute(context, routeVariable.Name));
                    }
                    else
                    {
                        // The value of a system variable
                        sb.Append(instruction(context));
                    }
                }

                return sb.ToString();
            }
        }

        private struct RouteVariable
        {
            internal RouteVariable(string name)
            {
                Name = name;
            }

            internal string Name { get; }
        }
    }
}
