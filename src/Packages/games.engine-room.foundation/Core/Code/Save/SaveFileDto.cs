using System;
using System.Collections.Generic;

namespace Core.Save
{
    internal sealed record SaveFileDto(int FormatVersion, DateTime SavedAtUtc, Dictionary<string, SaveSectionDto?>? Sections);
}
