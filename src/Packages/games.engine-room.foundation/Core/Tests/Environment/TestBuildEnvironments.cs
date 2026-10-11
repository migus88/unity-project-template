using System;
using System.Collections.Generic;
using Core.Editor.Environment;
using Core.Environment;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Core.Tests.Environment
{
    internal sealed class TestBuildEnvironments : IDisposable
    {
        private readonly List<Object> _created = new();

        public BuildEnvironment Create(string environmentName = "development", bool isTelemetryAllowed = false, string telemetryEndpoint = "", params string[] remoteConfigKeys)
        {
            var environment = Track(ScriptableObject.CreateInstance<BuildEnvironment>());
            environment.name = environmentName;
            using var serialized = new SerializedObject(environment);
            serialized.FindProperty("<EnvironmentName>k__BackingField").stringValue = environmentName;
            serialized.FindProperty("<IsTelemetryAllowed>k__BackingField").boolValue = isTelemetryAllowed;
            serialized.FindProperty("<TelemetryEndpoint>k__BackingField").stringValue = telemetryEndpoint;
            var defaults = serialized.FindProperty("_remoteConfigDefaults");
            defaults.arraySize = remoteConfigKeys.Length;

            for (var i = 0; i < remoteConfigKeys.Length; i++)
            {
                defaults.GetArrayElementAtIndex(i).FindPropertyRelative("<Key>k__BackingField").stringValue = remoteConfigKeys[i];
            }

            LogAssert.ignoreFailingMessages = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            LogAssert.ignoreFailingMessages = false;
            return environment;
        }

        public BuildProfile CreateProfile(string name)
        {
            var profile = Track(ScriptableObject.CreateInstance<BuildProfile>());
            profile.name = name;
            return profile;
        }

        public ActiveBuildEnvironment CreateSlot(BuildEnvironment? environment)
        {
            var slot = Track(ScriptableObject.CreateInstance<ActiveBuildEnvironment>());
            using var serialized = new SerializedObject(slot);
            serialized.FindProperty("<Environment>k__BackingField").objectReferenceValue = environment;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return slot;
        }

        public BuildEnvironmentMap CreateMap(ActiveBuildEnvironment slot, BuildEnvironment editorEnvironment)
        {
            var map = Track(ScriptableObject.CreateInstance<BuildEnvironmentMap>());
            using var serialized = new SerializedObject(map);
            serialized.FindProperty("<Active>k__BackingField").objectReferenceValue = slot;
            serialized.FindProperty("<EditorEnvironment>k__BackingField").objectReferenceValue = editorEnvironment;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return map;
        }

        public void Dispose()
        {
            foreach (var created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
