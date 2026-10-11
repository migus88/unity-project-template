using System;
using Core.Environment;
using UnityEditor.Build.Profile;
using UnityEngine;

namespace Core.Editor.Environment
{
    [Serializable]
    public struct BuildEnvironmentMapping
    {
        [field: SerializeField] public BuildProfile? Profile { get; private set; }
        [field: SerializeField] public BuildEnvironment? DevelopmentEnvironment { get; private set; }
        [field: SerializeField] public BuildEnvironment? ReleaseEnvironment { get; private set; }

        public BuildEnvironmentMapping(BuildProfile? profile, BuildEnvironment? developmentEnvironment, BuildEnvironment? releaseEnvironment)
        {
            Profile = profile;
            DevelopmentEnvironment = developmentEnvironment;
            ReleaseEnvironment = releaseEnvironment;
        }
    }
}
