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

        [SetUp]
        public void SetUp()
        {
            _descriptor = ScriptableObject.CreateInstance<FirstTestDomainDescriptor>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_descriptor);
        }

        [Test]
        public void RegisterDomain_Descriptor_RegistersDescriptorAsConcreteTypeAndDomainAsSingleton()
        {
            // Arrange
            var builder = new ContainerBuilder();

            // Act
            builder.RegisterDomain<TestDomain>(_descriptor);
            using var container = builder.Build();

            // Assert
            container.Resolve<FirstTestDomainDescriptor>().Should().BeSameAs(_descriptor);
            var domain = container.Resolve<TestDomain>();
            domain.Descriptor.Should().BeSameAs(_descriptor);
            container.Resolve<IReadOnlyList<IDebugRunnableDomain>>().Should().ContainSingle().Which.Should().BeSameAs(domain);
        }
    }
}
