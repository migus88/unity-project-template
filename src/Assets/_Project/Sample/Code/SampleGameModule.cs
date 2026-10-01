using Bootstrap;
using Core.Domains;
using Gameplay;
using MainMenu;
using UnityEngine;
using VContainer;

namespace Sample
{
    [CreateAssetMenu(menuName = "Game Modules/Sample")]
    public sealed class SampleGameModule : GameModule
    {
        [SerializeField] private MainMenuDomainDescriptor _mainMenuDescriptor = null!;
        [SerializeField] private GameplayDomainDescriptor _gameplayDescriptor = null!;

        public override void Install(IContainerBuilder builder)
        {
            builder.RegisterDomain<MainMenuDomain>(_mainMenuDescriptor);
            builder.RegisterDomain<GameplayDomain>(_gameplayDescriptor);
            builder.Register<IMainFlow, SampleMainFlow>(Lifetime.Singleton);
        }
    }
}
