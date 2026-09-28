using AwesomeAssertions;
using Core.Localization;
using NUnit.Framework;
using Object = UnityEngine.Object;

namespace Core.Tests.Localization
{
    public sealed class LocalizationTableRegistrationTests
    {
        private static readonly TextKey PlayKey = new("Shared", "play");

        private LocalizationTable _table = null!;
        private LocalizationService _service = null!;
        private LocalizationTableRegistration _registration = null!;

        [SetUp]
        public void SetUp()
        {
            _table = TestLocalizationTables.Create("Shared", ("play", "Play", "Graj"));
            _service = new LocalizationService(Language.English, [Language.English, Language.Polish]);
            _registration = new LocalizationTableRegistration(_service, _table);
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
            Object.DestroyImmediate(_table);
        }

        [Test]
        public void Initialize_Table_AddsTableToService()
        {
            // Act
            _registration.Initialize();

            // Assert
            _service.Get(PlayKey).Should().Be("Play");
        }

        [Test]
        public void Dispose_AfterInitialize_RemovesTableFromService()
        {
            // Arrange
            _registration.Initialize();

            // Act
            _registration.Dispose();

            // Assert
            _service.Get(PlayKey).Should().Be("Shared/play");
        }

        [Test]
        public void Dispose_WithoutInitialize_DoesNothing()
        {
            // Arrange
            _service.AddTable(_table);

            // Act
            _registration.Dispose();

            // Assert
            _service.Get(PlayKey).Should().Be("Play");
        }

        [Test]
        public void Dispose_Twice_RemovesTableOnce()
        {
            // Arrange
            _registration.Initialize();
            _registration.Dispose();

            // Act
            _registration.Dispose();

            // Assert
            _service.Get(PlayKey).Should().Be("Shared/play");
        }
    }
}
