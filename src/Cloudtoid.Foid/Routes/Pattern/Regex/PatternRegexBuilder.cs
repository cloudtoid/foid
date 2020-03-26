﻿namespace Cloudtoid.Foid.Routes.Pattern
{
    using System;
    using System.Text;
    using System.Text.RegularExpressions;

    internal sealed class PatternRegexBuilder : PatternNodeVisitor
    {
        private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);
        private static readonly string SegmentStart = Regex.Escape(@"/");
        private static readonly string Wildcard = $"[^{SegmentStart}]+";  // [^\/]+
        private static readonly string Start = $@"\A({SegmentStart})?"; // \A(\/)?
        private static readonly string End = $@"({SegmentStart})?$"; // (\/)?$
        private static readonly RegexOptions Options =
            RegexOptions.IgnoreCase
            | RegexOptions.Singleline
            | RegexOptions.ExplicitCapture
            | RegexOptions.Compiled
            | RegexOptions.CultureInvariant;

        private readonly StringBuilder builder = new StringBuilder(Start);

        internal Regex Build(PatternNode pattern)
        {
            Visit(pattern);
            builder.Append(End);
            return new Regex(builder.ToString(), Options, MatchTimeout);
        }

        protected internal override void VisitMatch(MatchNode node)
            => builder.Append(Regex.Escape(node.Value));

        protected internal override void VisitVariable(VariableNode node)
        {
            // TODO: Instead of [^/], should we be exact about what characters can be included?

            // - Generates a regex capture with the name of the variable:  (?<variable>[^\/]+)
            // - Variable name does not need to be escaped or validated. The PatternParser ensures that it only contains 'a-zA-Z0-9_'
            //   and the first character is not a number.
            builder.Append($"(?<{node.Name}>[^{SegmentStart}]+)");
        }

        protected internal override void VisitSegmentStart(SegmentStartNode node)
            => builder.Append(SegmentStart);

        protected internal override void VisitWildcard(WildcardNode node)
            => builder.Append(Wildcard);

        protected internal override void VisitOptional(OptionalNode node)
        {
            // regex: (node)?
            // Note that RegexOptions.ExplicitCapture is on so no need to use "(?:node)?".

            builder.AppendOpenParentheses();
            base.VisitOptional(node);
            builder.Append(")?");
        }
    }
}
