using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Core.Input
{
    public sealed class InputService : IInputService, IDisposable
    {
        public GameInput Actions { get; }

        private readonly List<MapsHandle> _stack = new();

        public InputService(GameInput actions)
        {
            Actions = actions;
        }

        public IDisposable Push(InputMaps maps)
        {
            var handle = new MapsHandle(this, maps);
            _stack.Add(handle);
            ApplyTop();
            return handle;
        }

        private void Pop(MapsHandle handle)
        {
            if (_stack.Remove(handle))
            {
                ApplyTop();
            }
        }

        private void ApplyTop()
        {
            var maps = _stack.Count == 0 ? InputMaps.None : _stack[^1].Maps;
            SetEnabled(Actions.Player.Get(), (maps & InputMaps.Player) != 0);
            SetEnabled(Actions.UI.Get(), (maps & InputMaps.Ui) != 0);
        }

        private static void SetEnabled(InputActionMap map, bool isEnabled)
        {
            if (isEnabled)
            {
                map.Enable();
            }
            else
            {
                map.Disable();
            }
        }

        public void Dispose()
        {
            _stack.Clear();
            Actions.Disable();
        }

        private sealed class MapsHandle : IDisposable
        {
            public InputMaps Maps { get; }

            private readonly InputService _owner;

            public MapsHandle(InputService owner, InputMaps maps)
            {
                _owner = owner;
                Maps = maps;
            }

            public void Dispose()
            {
                _owner.Pop(this);
            }
        }
    }
}
