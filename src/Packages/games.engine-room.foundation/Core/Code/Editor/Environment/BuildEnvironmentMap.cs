using System;
using System.Collections.Generic;
using Core.Environment;
using UnityEngine;

namespace Core.Editor.Environment
{
    [CreateAssetMenu(menuName = "Core/Build Environment Map")]
    public sealed class BuildEnvironmentMap : ScriptableObject
    {
        [field: SerializeField] public ActiveBuildEnvironment? Active { get; private set; }
        [field: SerializeField] public BuildEnvironment? EditorEnvironment { get; private set; }

        public IReadOnlyList<BuildEnvironmentMapping> Mappings => _mappings;

        [SerializeField] private BuildEnvironmentMapping[] _mappings = Array.Empty<BuildEnvironmentMapping>();
    }
}
