﻿namespace Cloudtoid.Foid.Routes.Pattern
{
    using System.Collections.Generic;

    internal static class PatternConstants
    {
        internal const string EscapeSequence = @"\\";
        internal const char SegmentStart = '/';
        internal const char VariableStart = ':';
        internal const char OptionalStart = '(';
        internal const char OptionalEnd = ')';
        internal const char Wildcard = '*';

        internal static readonly HashSet<char> Escapable = new HashSet<char>
        {
            VariableStart,
            OptionalStart,
            OptionalEnd,
            Wildcard
        };
    }
}
