using System;
using OneOf;

namespace Core.Telemetry
{
    [GenerateOneOf]
    internal sealed partial class TelemetrySendOutcome : OneOfBase<TelemetrySendOutcome.Sent, TelemetrySendOutcome.Rejected, TelemetrySendOutcome.Retry>
    {
        public readonly record struct Sent;

        public readonly record struct Rejected(long Status, string Body);

        public readonly record struct Retry(TimeSpan? RetryAfter, string Reason);
    }
}
