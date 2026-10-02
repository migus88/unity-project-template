#if UNITY_EDITOR || DEVELOPMENT_BUILD
using VContainer;
using VContainer.Unity;

namespace Core.Cheats
{
    public static class CheatRegistrationExtensions
    {
        public static void RegisterCheat<TCheat>(this IContainerBuilder builder) where TCheat : class, ICheat
        {
            builder.Register<TCheat>(Lifetime.Singleton);
            builder.RegisterEntryPoint<CheatRegistration<TCheat>>();
        }

        public static void RegisterCheats<TProvider>(this IContainerBuilder builder) where TProvider : class, ICheatProvider
        {
            builder.Register<TProvider>(Lifetime.Singleton);
            builder.RegisterEntryPoint<CheatProviderRegistration<TProvider>>();
        }

        public static void RegisterCheatConsole(this IContainerBuilder builder, CheatConsoleView view)
        {
            builder.RegisterComponent(view);
            builder.Register<CheatRegistry>(Lifetime.Singleton);
            builder.Register<CheatConsoleModel>(Lifetime.Singleton);
            builder.RegisterEntryPoint<CheatConsolePresenter>().AsSelf();
            builder.RegisterEntryPoint<CheatConsoleInputHandler>();
        }
    }
}
#endif
