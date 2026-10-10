using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Core.Domains;
using NUnit.Framework;
using TestUtils;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Core.Tests.Domains
{
    public sealed class DomainLifetimeScopeTests
    {
        private readonly List<GameObject> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in _createdObjects)
            {
                if (createdObject != null)
                {
                    createdObject.GetComponent<LifetimeScope>().DisposeCore();
                    UnityEngine.Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void VContainerSettingsInstance_EditMode_IsNullSoTestScopesGetNoImplicitRoot()
        {
            // Act
            var instance = VContainerSettings.Instance;

            // Assert
            instance.Should().BeNull();
        }

        [Test]
        public void Build_NoParent_Throws()
        {
            // Arrange
            var scope = CreateScope<FailingDomainScope>("Domain Scope");

            // Act
            Action act = () => scope.Build();

            // Assert
            act.Should().Throw<InvalidOperationException>().WithMessage("*not built by DomainRunner*");
        }

        [Test]
        public void Build_ParentButNotBuiltByRunner_Throws()
        {
            // Arrange
            var parent = CreateScope<LifetimeScope>("Parent Scope");

            using (LifetimeScope.Enqueue(builder => builder.RegisterInstance(new ScopeRef(parent, 0))))
            {
                parent.Build();
            }

            var scope = CreateScope<FailingDomainScope>("Domain Scope");

            // Act
            Action act = () =>
            {
                using (LifetimeScope.EnqueueParent(parent))
                {
                    scope.Build();
                }
            };

            // Assert
            act.Should().Throw<InvalidOperationException>().WithMessage("*not built by DomainRunner*");
        }

        [Test]
        public void OnApplicationQuit_BuiltScope_DisposesContainer()
        {
            // Arrange
            var parent = CreateScope<LifetimeScope>("Parent Scope");

            using (LifetimeScope.Enqueue(builder => builder.RegisterInstance(new ScopeRef(parent, 0))))
            {
                parent.Build();
            }

            var scope = CreateScope<EmptyDomainScope>("Domain Scope");

            using (LifetimeScope.EnqueueParent(parent))
            using (LifetimeScope.Enqueue(builder =>
            {
                builder.RegisterInstance(new DomainCompletion<TestDomainResult>()).As<IDomainCompletion>();
                builder.Register<DisposeProbe>(Lifetime.Singleton);
            }))
            {
                scope.Build();
            }

            var probe = scope.Container.Resolve<DisposeProbe>();

            // Act
            scope.OnApplicationQuit();

            // Assert
            probe.IsDisposed.Should().BeTrue();
            scope.Container.Should().BeNull();
        }

        private TScope CreateScope<TScope>(string name) where TScope : LifetimeScope
        {
            var scopeObject = new GameObject(name);
            _createdObjects.Add(scopeObject);
            return scopeObject.AddComponent<TScope>();
        }
    
        private sealed class DisposeProbe : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }
    }
}
