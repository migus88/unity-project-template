using System;
using Core.Input;
using Migs.MLock;
using Migs.MLock.Interfaces;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Gameplay.Pause
{
    internal sealed class PauseInputHandler : GameInput.IUIActions, IStartable, IDisposable
    {
        private readonly IInputService _input;
        private readonly ResumeRequests _resumeRequests;
        private readonly LockGate<InputLockTag> _lockGate;

        public PauseInputHandler(IInputService input, ILockService<InputLockTag> locks, ResumeRequests resumeRequests)
        {
            _input = input;
            _resumeRequests = resumeRequests;
            _lockGate = new LockGate<InputLockTag>(locks, InputLockTag.Ui);
        }

        public void Start()
        {
            _input.Actions.UI.AddCallbacks(this);
        }

        public void OnCancel(InputAction.CallbackContext context)
        {
            if (_lockGate.IsLocked || !context.performed)
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

        public void OnZoom(InputAction.CallbackContext context)
        {
        }

        public void OnToggleView(InputAction.CallbackContext context)
        {
        }

        public void OnConfirm(InputAction.CallbackContext context)
        {
        }

        public void OnDetails(InputAction.CallbackContext context)
        {
        }

        public void OnPeek(InputAction.CallbackContext context)
        {
        }

        public void OnShortcut1(InputAction.CallbackContext context)
        {
        }

        public void OnShortcut2(InputAction.CallbackContext context)
        {
        }

        public void OnShortcut3(InputAction.CallbackContext context)
        {
        }

        public void Dispose()
        {
            _input.Actions.UI.RemoveCallbacks(this);
            _lockGate.Dispose();
        }
    }
}
