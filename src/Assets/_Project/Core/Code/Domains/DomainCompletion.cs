using System;
using Cysharp.Threading.Tasks;

namespace Core.Domains
{
    public sealed class DomainCompletion<TResult> : IDomainCompletion where TResult : class
    {
        public UniTask<TResult> Task => _source.Task;
        public bool IsCompleted { get; private set; }

        private readonly UniTaskCompletionSource<TResult> _source = new();

        public void Complete(TResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (IsCompleted)
            {
                throw new InvalidOperationException($"{nameof(DomainCompletion<TResult>)}<{typeof(TResult).Name}> was already completed.");
            }

            IsCompleted = true;
            _source.TrySetResult(result);
        }

        void IDomainCompletion.Fail(Exception exception)
        {
            if (IsCompleted)
            {
                return;
            }

            IsCompleted = true;
            _source.TrySetException(exception);
        }
    }
}
