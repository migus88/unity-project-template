using System;
using Core.Input;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Gameplay.Pause
{
    internal sealed class PauseInputHandler : GameInput.IUIActions, IStartable, IDisposable
    {
        private readonly IInputService _input;
        private readonly ResumeRequests _resumeRequests;

        public PauseInputHandler(IInputService input, ResumeRequests resumeRequests)
        {
            _input = input;
            _resumeRequests = resumeRequests;
        }

        public void Start()
        {
            _input.Actions.UI.AddCallbacks(this);
        }

        public void OnCancel(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                _resumeRequests.Request();
            }
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

        public void Dispose()
        {
            _input.Actions.UI.RemoveCallbacks(this);
        }
    }
}
