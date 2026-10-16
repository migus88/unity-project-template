using System;
using Core.Input;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Consent.Prompt
{
    internal sealed class ConsentCancelInputHandler : GameInput.IUIActions, IStartable, IDisposable
    {
        private readonly IInputService _input;
        private readonly ConsentPromptPresenter _presenter;

        public ConsentCancelInputHandler(IInputService input, ConsentPromptPresenter presenter)
        {
            _input = input;
            _presenter = presenter;
        }

        public void Start()
        {
            _input.Actions.UI.AddCallbacks(this);
        }

        public void OnCancel(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                _presenter.Decline();
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

        public void OnZoom(InputAction.CallbackContext context)
        {
        }

        public void OnPan(InputAction.CallbackContext context)
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
        }
    }
}
