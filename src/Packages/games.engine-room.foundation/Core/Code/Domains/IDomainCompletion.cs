using System;

namespace Core.Domains
{
    internal interface IDomainCompletion
    {
        void Fail(Exception exception);
    }
}
