using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace TestUtils
{
    public sealed class TestOpenLedger
    {
        private readonly HashSet<string> _opened = new(StringComparer.Ordinal);
        private WeakReference<object>? _fixture;

        public void Record(TestOpenContext context, string assetPath)
        {
            if (context.Fixture == null || context.IsSlow)
            {
                return;
            }

            if (!IsCurrent(context.Fixture))
            {
                _fixture = new WeakReference<object>(context.Fixture);
                _opened.Clear();
            }

            if (!_opened.Add(assetPath))
            {
                Assert.Fail($"{assetPath} was opened a second time in fixture {context.FixtureName}. Open it once in [OneTimeSetUp] and reset what the tests change in [SetUp] (Rules §9; skill writing-tests, Scene and prefab tests), or mark a deliberate exception [Slow(\"reason\")].");
            }
        }

        private bool IsCurrent(object fixture)
        {
            return _fixture != null && _fixture.TryGetTarget(out var current) && ReferenceEquals(current, fixture);
        }
    }
}
