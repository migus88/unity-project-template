using System.Collections.Generic;
using System.Reflection;
using Core.Audio;
using R3;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shared.UI.Tests
{
    internal sealed class UiInteractionTestCanvas
    {
        public GameObject Root { get; }
        public List<UiInteraction> Received { get; } = new();

        private DisposableBag _subscriptions;

        public UiInteractionTestCanvas()
        {
            Root = new GameObject("Canvas");
            var relay = Root.AddComponent<UiInteractionRelay>();
            relay.Requested.Subscribe(Received.Add).AddTo(ref _subscriptions);
        }

        public GameObject CreateChild(string name)
        {
            var child = new GameObject(name);
            child.SetActive(false);
            child.transform.SetParent(Root.transform, false);
            return child;
        }

        public static void Awaken(MonoBehaviour behaviour)
        {
            behaviour.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(behaviour, null);
        }

        public void Destroy()
        {
            _subscriptions.Dispose();
            Object.DestroyImmediate(Root);
        }
    }
}
