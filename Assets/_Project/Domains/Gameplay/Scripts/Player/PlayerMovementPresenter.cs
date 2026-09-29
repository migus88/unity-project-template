using UnityEngine;
using VContainer.Unity;

namespace Gameplay.Player
{
    internal sealed class PlayerMovementPresenter : IFixedTickable
    {
        private readonly PlayerView _view;
        private readonly PlayerInputState _input;
        private readonly GameplayConfig _config;

        public PlayerMovementPresenter(PlayerView view, PlayerInputState input, GameplayConfig config)
        {
            _view = view;
            _input = input;
            _config = config;
        }

        public void FixedTick()
        {
            var move = Vector2.ClampMagnitude(_input.Move, 1f);
            _view.SetHorizontalVelocity(new Vector3(move.x, 0f, move.y) * _config.MoveSpeed);
        }
    }
}
