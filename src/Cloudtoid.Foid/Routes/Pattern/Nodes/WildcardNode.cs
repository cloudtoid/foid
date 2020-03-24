﻿namespace Cloudtoid.Foid.Routes.Pattern
{
    /// <summary>
    /// Represents '*' in the pattern. '*' is the wild-card.
    /// </summary>
    internal sealed class WildcardNode : LeafNode
    {
        private static readonly string Value = PatternConstants.Wildcard.ToString();

        private WildcardNode()
        {
        }

        internal static WildcardNode Instance { get; } = new WildcardNode();

        public override string ToString() => Value;

        internal override void Accept(PatternNodeVisitor visitor)
            => visitor.VisitWildcard(this);
    }
}
