using System;
using Core.Input;
using Migs.MLock.Interfaces;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Gameplay.Pause
{
    internal sealed class PauseInputHandler : GameInput.IUIActions, ILockable<InputLockTag>, IStartable, IDisposable
    {
        public InputLockTag LockTags => InputLockTag.Ui;

        private bool _isLocked;

        private readonly IInputService _input;
        private readonly ILockService<InputLockTag> _locks;
        private readonly ResumeRequests _resumeRequests;

        public PauseInputHandler(IInputService input, ILockService<InputLockTag> locks, ResumeRequests resumeRequests)
        {
            _input = input;
            _locks = locks;
            _resumeRequests = resumeRequests;
        }

        public void Start()
        {
            _input.Actions.UI.AddCallbacks(this);
            _locks.Subscribe(this);
        }

        public void OnCancel(InputAction.CallbackContext context)
        {
            if (_isLocked || !context.performed)
            {
                return;
            }

            _resumeRequests.Request();
        }

        public void OnNavigate(InputAction.CallbackContext context)
        {
        }

        public void OnSubmit(InputAction.CallbackContext context)
        {
        }

        public void OnPoint(InputAction.CallbackContext context)
        {
        }

        public void OnClick(InputAction.CallbackContext context)
        {
        }

        public void OnScrollWheel(InputAction.CallbackContext context)
        {
        }

        public void HandleLocking()
        {
            _isLocked = true;
        }

        public void HandleUnlocking()
        {
            _isLocked = false;
        }

        public void Dispose()
        {
            _input.Actions.UI.RemoveCallbacks(this);
            _locks.Unsubscribe(this);
        }
    }
}
