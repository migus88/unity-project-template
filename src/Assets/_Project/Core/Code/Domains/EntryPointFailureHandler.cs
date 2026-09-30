using System;
using Core.Logging;
using VContainer;
using VContainer.Unity;

namespace Core.Domains
{
    internal static class EntryPointFailureHandler
    {
        public static void RegisterEntryPointFailureHandler(this IContainerBuilder builder)
        {
            builder.RegisterEntryPointExceptionHandler(exception => TryLogFailure(exception));
        }

        public static void RegisterDomainEntryPointFailureHandler(this IContainerBuilder builder)
        {
            IDomainCompletion? completion = null;
            builder.RegisterBuildCallback(resolver => completion = resolver.Resolve<IDomainCompletion>());
            builder.RegisterEntryPointExceptionHandler(exception =>
            {
                if (TryLogFailure(exception))
                {
                    completion?.Fail(exception);
                }
            });
        }

        private static bool TryLogFailure(Exception exception)
        {
            if (exception is OperationCanceledException)
            {
                return false;
            }

            Log.Exception(exception);
            return true;
        }
    }
}
