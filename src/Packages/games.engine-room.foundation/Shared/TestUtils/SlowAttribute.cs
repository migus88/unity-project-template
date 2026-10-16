using System;
using NUnit.Framework;

namespace TestUtils
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class SlowAttribute : CategoryAttribute
    {
        public const string CategoryName = "Slow";

        public string Reason { get; }

        public SlowAttribute(string reason)
        {
            Reason = reason;
        }
    }
}
