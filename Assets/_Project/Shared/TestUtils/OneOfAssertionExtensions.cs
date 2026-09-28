using AwesomeAssertions.Execution;
using OneOf;

namespace TestUtils
{
    public static class OneOfAssertionExtensions
    {
        public static OneOfAssertions Should(this IOneOf subject)
        {
            return new OneOfAssertions(subject, AssertionChain.GetOrCreate());
        }
    }
}
