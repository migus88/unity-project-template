using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Audio;
using Core.Results;
using Core.Time;
using Core.Views;
using Cysharp.Threading.Tasks;
using Gameplay.Collectibles;
using Gameplay.Room;
using Gameplay.Round;
using NSubstitute;
using NUnit.Framework;
using OneOf;
using TestUtils;
using Unity.Loading;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gameplay.Tests.Collectibles
{
    public sealed class CollectiblesPresenterTests
    {
        private static readonly DateTime Start = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        private IViewFactory _viewFactory = null!;
        private TimerService _timers = null!;
        private ScoreModel _score = null!;
        private RoundService _round = null!;
        private GameplayConfig _config = null!;
        private GameplayContent _content = null!;
        private RoomView _room = null!;
        private CollectiblesPresenter _presenter = null!;

        private readonly List<Object> _createdObjects = new();

        [SetUp]
        public void SetUp()
        {
            _viewFactory = Substitute.For<IViewFactory>();
            var gameClock = new FakeClock(Start);
            _timers = new TimerService(new FakeClock(Start), gameClock);
            _score = new ScoreModel();
            _round = new RoundService(_score, _timers, gameClock);
            _config = CreateAsset<GameplayConfig>();
            _content = CreateAsset<GameplayContent>();
            _room = CreateObject("Room").AddComponent<RoomView>();
            _presenter = new CollectiblesPresenter(_round, _config, _content, Substitute.For<IAudioService>(), new ViewPoolFactory(_viewFactory));
        }

        [TearDown]
        public void TearDown()
        {
            _presenter.Dispose();
            _round.Dispose();
            _score.Dispose();
            _timers.Dispose();

            foreach (var createdObject in _createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public async Task BeginAsync_EffectLoads_PrewarmsOneIdleEffect()
        {
            // Arrange
            var effect = CreateObject("Effect").AddComponent<PickupEffectView>();
            _viewFactory.CreateAsync<PickupEffectView>(Arg.Any<Loadable<GameObject>>(), Arg.Any<Transform>(), Arg.Any<CancellationToken>())
                .Returns(_ => UniTask.FromResult<OneOf<PickupEffectView, NotFound>>(effect));

            // Act
            await _presenter.BeginAsync(_room, CancellationToken.None);

            // Assert
            _ = _viewFactory.Received(1).CreateAsync<PickupEffectView>(Arg.Any<Loadable<GameObject>>(), Arg.Any<Transform>(), Arg.Any<CancellationToken>());
            effect.gameObject.activeSelf.Should().BeFalse();
        }

        [Test]
        public async Task BeginAsync_EffectNotFound_WarnsAndCompletes()
        {
            // Arrange
            _viewFactory.CreateAsync<PickupEffectView>(Arg.Any<Loadable<GameObject>>(), Arg.Any<Transform>(), Arg.Any<CancellationToken>())
                .Returns(_ => UniTask.FromResult<OneOf<PickupEffectView, NotFound>>(new NotFound()));

            // Act
            await _presenter.BeginAsync(_room, CancellationToken.None);

            // Assert
            LogAssert.Expect(LogType.Warning, "[Gameplay] The pickup effect prefab could not be loaded, skipping the effect.");
        }

        private GameObject CreateObject(string name)
        {
            var gameObject = new GameObject(name);
            _createdObjects.Add(gameObject);
            return gameObject;
        }

        private T CreateAsset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(asset);
            return asset;
        }
    }
}
