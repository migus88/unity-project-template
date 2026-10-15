using System;
using Core.Input;
using Gameplay.Flow;
using Migs.MLock;
using Migs.MLock.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Gameplay.Player
{
    internal sealed class PlayerInputHandler : GameInput.IPlayerActions, IStartable, IDisposable
    {
        private readonly IInputService _input;
        private readonly PlayerInputState _state;
        private readonly PauseRequests _pauseRequests;
        private readonly LockGate<InputLockTag> _lockGate;

        public PlayerInputHandler(IInputService input, ILockService<InputLockTag> locks, PlayerInputState state, PauseRequests pauseRequests)
        {
            _input = input;
            _state = state;
            _pauseRequests = pauseRequests;
            _lockGate = new LockGate<InputLockTag>(locks, InputLockTag.Movement | InputLockTag.Pause, OnLockChanged);
        }

        public void Start()
        {
            _input.Actions.Player.AddCallbacks(this);
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            if (_lockGate.IsLocked)
            {
                return;
            }

            if (context.performed)
            {
                _state.Move = context.ReadValue<Vector2>();
            }
            else if (context.canceled)
            {
                _state.Move = Vector2.zero;
            }
        }

        public void OnLook(InputAction.CallbackContext context)
        {
        }

        public void OnJump(InputAction.CallbackContext context)
        {
        }

        public void OnInteract(InputAction.CallbackContext context)
        {
        }

        public void OnPause(InputAction.CallbackContext context)
        {
            if (_lockGate.IsLocked || !context.performed)
            {
                return;
            }

            _pauseRequests.Request();
        }

        private void OnLockChanged(bool isLocked)
        {
            if (isLocked)
            {
                _state.Move = Vector2.zero;
                return;
            }

            var move = _input.Actions.Player.Move;
            _state.Move = move.enabled ? move.ReadValue<Vector2>() : Vector2.zero;
        }

        public void Dispose()
        {
            _input.Actions.Player.RemoveCallbacks(this);
            _lockGate.Dispose();
        }
    }
}
