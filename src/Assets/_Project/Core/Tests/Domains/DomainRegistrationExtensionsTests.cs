using System.Collections.Generic;
using AwesomeAssertions;
using Core.Domains;
using NUnit.Framework;
using UnityEngine;
using VContainer;

namespace Core.Tests.Domains
{
    public sealed class DomainRegistrationExtensionsTests
    {
        private FirstTestDomainDescriptor _descriptor = null!;
        private SecondTestDomainDescriptor _subDescriptor = null!;

        [SetUp]
        public void SetUp()
        {
            _descriptor = ScriptableObject.CreateInstance<FirstTestDomainDescriptor>();
            _subDescriptor = ScriptableObject.CreateInstance<SecondTestDomainDescriptor>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_descriptor);
            Object.DestroyImmediate(_subDescriptor);
        }

        [Test]
        public void RegisterDomain_Descriptor_RegistersDescriptorAsConcreteTypeAndDomainAsScoped()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.RegisterInstance(new ScopeRef(null!, 0));

            // Act
            builder.RegisterDomain<TestDomain>(_descriptor);
            using var container = builder.Build();

            // Assert
            container.Resolve<FirstTestDomainDescriptor>().Should().BeSameAs(_descriptor);
            var domain = container.Resolve<TestDomain>();
            domain.Descriptor.Should().BeSameAs(_descriptor);
            container.Resolve<TestDomain>().Should().BeSameAs(domain);
            container.Resolve<IReadOnlyList<IDebugRunnableDomain>>().Should().ContainSingle().Which.Should().BeSameAs(domain);
        }

        [Test]
        public void RegisterDomain_ResolvedFromChildScope_ReceivesChildScopeRef()
        {
            // Arrange
            var parentScope = new ScopeRef(null!, 0);
            var childScope = new ScopeRef(null!, 1);
            var builder = new ContainerBuilder();
            builder.RegisterInstance(parentScope);
            builder.RegisterDomain<TestDomain>(_descriptor);
            using var container = builder.Build();
            using var child = container.CreateScope(childBuilder => childBuilder.RegisterInstance(childScope));

            // Act
            var domain = child.Resolve<TestDomain>();

            // Assert
            domain.LauncherScope.Should().BeSameAs(childScope);
            container.Resolve<TestDomain>().LauncherScope.Should().BeSameAs(parentScope);
        }

        [Test]
        public void RegisterSubDomain_Descriptor_RegistersScopedDomainThatIsNotDebugRunnable()
        {
            // Arrange
            var childScope = new ScopeRef(null!, 1);
            var builder = new ContainerBuilder();
            builder.RegisterInstance(new ScopeRef(null!, 0));
            builder.RegisterDomain<TestDomain>(_descriptor);

            // Act
            builder.RegisterSubDomain<TestSubDomain>(_subDescriptor);
            using var container = builder.Build();
            using var child = container.CreateScope(childBuilder => childBuilder.RegisterInstance(childScope));

            // Assert
            container.Resolve<SecondTestDomainDescriptor>().Should().BeSameAs(_subDescriptor);
            var domain = child.Resolve<TestSubDomain>();
            domain.Descriptor.Should().BeSameAs(_subDescriptor);
            domain.LauncherScope.Should().BeSameAs(childScope);
            child.Resolve<TestSubDomain>().Should().BeSameAs(domain);
            container.Resolve<IReadOnlyList<IDebugRunnableDomain>>().Should().ContainSingle().Which.Should().BeOfType<TestDomain>();
        }
    }
}
