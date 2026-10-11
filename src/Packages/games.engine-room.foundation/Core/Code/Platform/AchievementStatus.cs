using System;
using OneOf;

namespace Core.Platform
{
    [GenerateOneOf]
    public sealed partial class AchievementStatus : OneOfBase<AchievementStatus.Locked, AchievementStatus.Unlocked, AchievementStatus.Unavailable>
    {
        public readonly record struct Locked;

        public readonly record struct Unlocked(DateTime? UnlockedUtc);

        public readonly record struct Unavailable;
    }
}
