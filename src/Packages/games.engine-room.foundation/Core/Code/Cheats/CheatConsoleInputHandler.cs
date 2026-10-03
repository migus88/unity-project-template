#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Core.Input;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Core.Cheats
{
    internal sealed class CheatConsoleInputHandler : GameInput.IDebugActions, IStartable, IDisposable
    {
        private readonly IInputService _input;
        private readonly CheatConsolePresenter _presenter;

        public CheatConsoleInputHandler(IInputService input, CheatConsolePresenter presenter)
        {
            _input = input;
            _presenter = presenter;
        }

        public void Start()
        {
            _input.Actions.Debug.AddCallbacks(this);
        }

        public void OnToggleConsole(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                _presenter.Toggle();
            }
        }

        public void OnCloseConsole(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                _presenter.Cancel();
            }
        }

        public void OnCompleteCommand(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                _presenter.CompleteLine();
            }
        }

        public void OnPreviousCommand(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                _presenter.MoveUp();
            }
        }

        public void OnNextCommand(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                _presenter.MoveDown();
            }
        }

        public void Dispose()
        {
            _input.Actions.Debug.RemoveCallbacks(this);
        }
    }
}
#endif
