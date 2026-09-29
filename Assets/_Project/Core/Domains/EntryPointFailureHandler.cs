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
            builder.RegisterEntryPointExceptionHandler(Handle);
        }

        private static void Handle(Exception exception)
        {
            if (exception is OperationCanceledException)
            {
                return;
            }

            Log.Exception(exception);
        }
    }
}
