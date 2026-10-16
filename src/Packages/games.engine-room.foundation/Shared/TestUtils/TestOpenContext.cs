using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace TestUtils
{
    public readonly record struct TestOpenContext(object? Fixture, string FixtureName)
    {
        public static TestOpenContext None => new(null, string.Empty);

        public static TestOpenContext Current()
        {
            var context = TestContext.CurrentTestExecutionContext;
            var fixture = FixtureOf(context?.CurrentTest);

            if (context?.TestObject == null || fixture == null)
            {
                return None;
            }

            return new TestOpenContext(context.TestObject, fixture.FullName);
        }

        private static ITest? FixtureOf(ITest? test)
        {
            while (test != null)
            {
                if (test is TestFixture)
                {
                    return test;
                }

                test = test.Parent;
            }

            return null;
        }
    }
}
