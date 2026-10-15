using System.Collections.Generic;
using System.Linq;
using Core.Domains;
using UnityEditor;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Core.Editor
{
    public static class RootDomainDescriptors
    {
        public static IReadOnlyList<DomainDescriptor> Collect()
        {
            var descriptors = new List<DomainDescriptor>();
            var settings = PlayerSettings.GetPreloadedAssets().OfType<VContainerSettings>().FirstOrDefault();

            if (settings == null || settings.RootLifetimeScope == null)
            {
                return descriptors;
            }

            CollectFrom(settings.RootLifetimeScope, descriptors, new HashSet<Object>());
            return descriptors;
        }

        private static void CollectFrom(Object target, List<DomainDescriptor> descriptors, HashSet<Object> visited)
        {
            if (!visited.Add(target))
            {
                return;
            }

            using var serializedTarget = new SerializedObject(target);
            var property = serializedTarget.GetIterator();

            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                switch (property.objectReferenceValue)
                {
                    case DomainDescriptor descriptor:
                        if (visited.Add(descriptor))
                        {
                            descriptors.Add(descriptor);
                        }

                        break;
                    case DomainContent:
                        break;
                    case ScriptableObject asset:
                        CollectFrom(asset, descriptors, visited);
                        break;
                }
            }
        }
    }
}
