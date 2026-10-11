using System;
using Cysharp.Threading.Tasks;
using VContainer.Unity;

namespace Core.Logging
{
    internal sealed class UnhandledExceptionForwarder : IInitializable, IDisposable
    {
        public void Initialize()
        {
            UniTaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        internal void OnUnobservedTaskException(Exception exception)
        {
            Forward(exception);
        }

        internal void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            if (args.ExceptionObject is Exception exception)
            {
                Forward(exception);
            }
        }

        private static void Forward(Exception exception)
        {
            if (exception is OperationCanceledException)
            {
                return;
            }

            Log.Exception(exception);
        }

        public void Dispose()
        {
            UniTaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        }
    }
}
