using System;
using Core.Input;
using Gameplay.Flow;
using Migs.MLock.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Gameplay.Player
{
    internal sealed class PlayerInputHandler : GameInput.IPlayerActions, ILockable<InputLockTag>, IStartable, IDisposable
    {
        public InputLockTag LockTags => InputLockTag.Movement | InputLockTag.Pause;

        private bool _isLocked;

        private readonly IInputService _input;
        private readonly ILockService<InputLockTag> _locks;
        private readonly PlayerInputState _state;
        private readonly PauseRequests _pauseRequests;

        public PlayerInputHandler(IInputService input, ILockService<InputLockTag> locks, PlayerInputState state, PauseRequests pauseRequests)
        {
            _input = input;
            _locks = locks;
            _state = state;
            _pauseRequests = pauseRequests;
        }

        public void Start()
        {
            _input.Actions.Player.AddCallbacks(this);
            _locks.Subscribe(this);
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            if (_isLocked)
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
            if (_isLocked || !context.performed)
            {
                return;
            }

            _pauseRequests.Request();
        }

        public void HandleLocking()
        {
            _isLocked = true;
            _state.Move = Vector2.zero;
        }

        public void HandleUnlocking()
        {
            _isLocked = false;
        }

        public void Dispose()
        {
            _input.Actions.Player.RemoveCallbacks(this);
            _locks.Unsubscribe(this);
        }
    }
}
