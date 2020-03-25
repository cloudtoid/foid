﻿namespace Cloudtoid.Foid.Routes.Pattern
{
    using System.Diagnostics.CodeAnalysis;

    internal interface IPatternValidator
    {
        bool Validate(
            PatternNode pattern,
            [NotNullWhen(false)] out string? error);
    }
}
