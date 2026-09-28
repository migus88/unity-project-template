using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Core.Localization
{
    public sealed class LocalizedLabelBinder : IStartable, IDisposable
    {
        private DisposableBag _subscriptions;

        private readonly ILocalizationService _localization;
        private readonly IReadOnlyList<GameObject> _roots;
        private readonly Observable<Scene> _loadedScenes;
        private readonly List<LocalizedLabel> _labels = new();

        public LocalizedLabelBinder(ILocalizationService localization, IReadOnlyList<GameObject> roots, Observable<Scene> loadedScenes)
        {
            _localization = localization;
            _roots = roots;
            _loadedScenes = loadedScenes;
        }

        public void Start()
        {
            foreach (var root in _roots)
            {
                Collect(root);
            }

            _localization.Current.Subscribe(_ => ApplyAll()).AddTo(ref _subscriptions);
            _loadedScenes.Subscribe(BindScene).AddTo(ref _subscriptions);
        }

        private void BindScene(Scene scene)
        {
            var firstNewLabel = _labels.Count;

            foreach (var root in scene.GetRootGameObjects())
            {
                Collect(root);
            }

            for (var i = firstNewLabel; i < _labels.Count; i++)
            {
                Apply(_labels[i]);
            }
        }

        private void Collect(GameObject root)
        {
            _labels.AddRange(root.GetComponentsInChildren<LocalizedLabel>(true));
        }

        private void ApplyAll()
        {
            _labels.RemoveAll(label => label == null);

            foreach (var label in _labels)
            {
                Apply(label);
            }
        }

        private void Apply(LocalizedLabel label)
        {
            label.SetText(_localization.Get(label.Key));
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }
    }
}
