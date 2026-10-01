using AwesomeAssertions;
using AwesomeAssertions.Execution;
using OneOf;

namespace TestUtils
{
    public sealed class OneOfAssertions
    {
        public IOneOf Subject { get; }

        private readonly AssertionChain _chain;

        public OneOfAssertions(IOneOf subject, AssertionChain chain)
        {
            Subject = subject;
            _chain = chain;
        }

        [CustomAssertion]
        public AndWhichConstraint<OneOfAssertions, TCase> BeCase<TCase>(string because = "", params object[] becauseArgs)
        {
            var value = Subject.Value;

            _chain
                .BecauseOf(because, becauseArgs)
                .ForCondition(value is TCase)
                .FailWith("Expected {context:union} to be case {0}{reason}, but found case {1} with value {2}.", typeof(TCase), value?.GetType(), value);

            var matched = value is TCase typed ? typed : default!;
            return new AndWhichConstraint<OneOfAssertions, TCase>(this, matched);
        }

        [CustomAssertion]
        public AndConstraint<OneOfAssertions> NotBeCase<TCase>(string because = "", params object[] becauseArgs)
        {
            var value = Subject.Value;

            _chain
                .BecauseOf(because, becauseArgs)
                .ForCondition(value is not TCase)
                .FailWith("Expected {context:union} not to be case {0}{reason}, but it was, with value {1}.", typeof(TCase), value);

            return new AndConstraint<OneOfAssertions>(this);
        }
    }
}
