using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Audio;
using Core.Input;
using Core.Localization;
using Core.Results;
using Core.Settings;
using Core.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using OneOf;
using TestUtils;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Core.Tests.Settings
{
    public sealed class SettingsSectionTests
    {
        private const string FilePath = "settings.json";
        private const string SectionKey = "sample";

        private static readonly SampleSettingsDto SampleDefault = new(0.5f, "normal");
        private static readonly SettingsSection<SampleSettingsDto> Section = new(SectionKey, 2, MigrateSample, SampleDefault);
        private static readonly SettingsDefaults Defaults = new(1f, 1f, 1f, 1f, Language.English);

        private InMemoryFileStorage _disk = null!;
        private IGraphicsDevice _graphics = null!;
        private GameInput _actions = null!;
        private IInputService _input = null!;
        private SettingsService _service = null!;

        [SetUp]
        public void SetUp()
        {
            _disk = new InMemoryFileStorage();
            _actions = new GameInput();
            _input = Substitute.For<IInputService>();
            _input.Actions.Returns(_actions);
            _graphics = Substitute.For<IGraphicsDevice>();
            _graphics.QualityLevel.Returns(0);
            _graphics.QualityLevelCount.Returns(1);
            _graphics.FullScreenMode.Returns(FullScreenMode.Windowed);
            _graphics.Resolution.Returns(new Resolution { width = 1280, height = 720, refreshRateRatio = new RefreshRate { numerator = 60, denominator = 1 } });
            _service = CreateService();
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
            Object.DestroyImmediate(_actions.asset);
        }

        [Test]
        public async Task Read_NoSettingsFile_ReturnsDefault()
        {
            // Arrange
            await _service.LoadAsync(CancellationToken.None);

            // Act
            var data = _service.Read(Section);

            // Assert
            data.Should().Be(SampleDefault);
        }

        [Test]
        public void Read_BeforeLoad_ReturnsDefault()
        {
            // Act
            var data = _service.Read(Section);

            // Assert
            data.Should().Be(SampleDefault);
        }

        [Test]
        public void Read_AfterWrite_ReturnsWrittenData()
        {
            // Arrange
            var written = new SampleSettingsDto(0.9f, "far");

            // Act
            _service.Write(Section, written);

            // Assert
            _service.Read(Section).Should().Be(written);
        }

        [Test]
        public async Task SaveAsync_AfterWrite_RoundTripsThroughTheFile()
        {
            // Arrange
            await _service.LoadAsync(CancellationToken.None);
            var written = new SampleSettingsDto(0.9f, "far");
            _service.Write(Section, written);

            // Act
            await _service.SaveAsync(CancellationToken.None);

            // Assert
            var json = JObject.Parse(_disk.Files[FilePath]);
            json["sections"]![SectionKey]!["version"]!.Value<int>().Should().Be(2);
            json["sections"]![SectionKey]!["data"]!["distance"]!.Value<float>().Should().Be(0.9f);
            (await LoadAndReadWithNewServiceAsync()).Should().Be(written);
        }

        [Test]
        public async Task Write_WithoutSave_DoesNotTouchTheFile()
        {
            // Arrange
            await _service.LoadAsync(CancellationToken.None);
            var before = _disk.Files[FilePath];

            // Act
            _service.Write(Section, new SampleSettingsDto(0.9f, "far"));

            // Assert
            _disk.Files[FilePath].Should().Be(before);
        }

        [Test]
        public async Task Read_OlderVersion_MigratesData()
        {
            // Arrange
            _disk.Files[FilePath] = CreateFile(new JObject { ["distance"] = 0.3f }, version: 1);
            await _service.LoadAsync(CancellationToken.None);

            // Act
            var data = _service.Read(Section);

            // Assert
            data.Should().Be(new SampleSettingsDto(0.3f, "migrated"));
        }

        [Test]
        public async Task SaveAsync_AfterMigratingRead_StoresTheMigratedVersion()
        {
            // Arrange
            _disk.Files[FilePath] = CreateFile(new JObject { ["distance"] = 0.3f }, version: 1);
            await _service.LoadAsync(CancellationToken.None);
            _service.Read(Section);

            // Act
            await _service.SaveAsync(CancellationToken.None);

            // Assert
            var section = JObject.Parse(_disk.Files[FilePath])["sections"]![SectionKey]!;
            section["version"]!.Value<int>().Should().Be(2);
            section["data"]!["mode"]!.Value<string>().Should().Be("migrated");
        }

        [Test]
        public async Task Read_MigrationFails_ReturnsDefaultAndWarnsOnce()
        {
            // Arrange
            _disk.Files[FilePath] = CreateFile(new JObject { ["somethingElse"] = true }, version: 1);
            await _service.LoadAsync(CancellationToken.None);
            LogAssert.Expect(LogType.Warning, "[Settings] Settings section 'sample' is corrupted, using its defaults: Version 1 has no distance.");

            // Act
            var first = _service.Read(Section);
            var second = _service.Read(Section);

            // Assert
            first.Should().Be(SampleDefault);
            second.Should().Be(SampleDefault);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task Read_NewerVersion_ReturnsDefaultAndWarns()
        {
            // Arrange
            _disk.Files[FilePath] = CreateFile(new JObject { ["distance"] = 0.3f, ["mode"] = "far" }, version: 3);
            await _service.LoadAsync(CancellationToken.None);
            LogAssert.Expect(LogType.Warning, "[Settings] Settings section 'sample' is corrupted, using its defaults: It has version 3, newer than the supported version 2.");

            // Act
            var data = _service.Read(Section);

            // Assert
            data.Should().Be(SampleDefault);
        }

        [Test]
        public async Task Read_DataDoesNotMatchDto_ReturnsDefaultAndWarns()
        {
            // Arrange
            _disk.Files[FilePath] = CreateFile(new JObject { ["distance"] = "very far", ["mode"] = "far" }, version: 2);
            await _service.LoadAsync(CancellationToken.None);
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Settings\] Settings section 'sample' is corrupted, using its defaults: It does not match SampleSettingsDto"));

            // Act
            var data = _service.Read(Section);

            // Assert
            data.Should().Be(SampleDefault);
        }

        [TestCase("\"not an object\"")]
        [TestCase("{ \"version\": 2 }")]
        [TestCase("{ \"version\": 0, \"data\": {} }")]
        [TestCase("{ \"version\": \"two\", \"data\": {} }")]
        public async Task Read_MalformedSection_ReturnsDefaultAndKeepsCoreSettings(string section)
        {
            // Arrange
            _disk.Files[FilePath] = $"{{ \"formatVersion\": 2, \"core\": {{ \"masterVolume\": 0.25 }}, \"sections\": {{ \"sample\": {section} }} }}";
            await _service.LoadAsync(CancellationToken.None);
            LogAssert.Expect(LogType.Warning, "[Settings] Settings section 'sample' is corrupted, using its defaults: It has no data or an invalid version.");

            // Act
            var data = _service.Read(Section);

            // Assert
            data.Should().Be(SampleDefault);
            _service.Current.CurrentValue.MasterVolume.Should().Be(0.25f);
        }

        [Test]
        public async Task Write_AfterCorruptedRead_ReplacesTheSection()
        {
            // Arrange
            _disk.Files[FilePath] = CreateFile(new JObject { ["distance"] = 0.3f, ["mode"] = "far" }, version: 3);
            await _service.LoadAsync(CancellationToken.None);
            LogAssert.Expect(LogType.Warning, new Regex("newer than the supported version"));
            _service.Read(Section);
            var written = new SampleSettingsDto(0.7f, "near");

            // Act
            _service.Write(Section, written);
            await _service.SaveAsync(CancellationToken.None);

            // Assert
            (await LoadAndReadWithNewServiceAsync()).Should().Be(written);
        }

        [Test]
        public async Task SaveAsync_UnknownSections_ArePreserved()
        {
            // Arrange
            var unknown = new JObject { ["version"] = 4, ["data"] = new JObject { ["anything"] = new JArray(1, 2, 3), ["when"] = "2026-09-29T10:00:00Z" } };
            var file = JObject.Parse(CreateFile(new JObject { ["distance"] = 0.3f, ["mode"] = "far" }, version: 2));
            file["sections"]!["deletedDomain"] = unknown;
            _disk.Files[FilePath] = file.ToString();
            await _service.LoadAsync(CancellationToken.None);

            // Act
            _service.Write(Section, new SampleSettingsDto(0.7f, "near"));
            await _service.SaveAsync(CancellationToken.None);

            // Assert
            var saved = ParseWithoutDates(_disk.Files[FilePath]);
            JToken.DeepEquals(saved["sections"]!["deletedDomain"], unknown).Should().BeTrue();
            saved["sections"]!["deletedDomain"]!["data"]!["when"]!.Type.Should().Be(JTokenType.String);
        }

        [Test]
        public async Task SaveAsync_CoreSettingsChange_KeepsSections()
        {
            // Arrange
            await _service.LoadAsync(CancellationToken.None);
            var written = new SampleSettingsDto(0.7f, "near");
            _service.Write(Section, written);
            await _service.SaveAsync(CancellationToken.None);

            // Act
            _service.Apply(_service.Current.CurrentValue with { MasterVolume = 0.1f });
            await _service.SaveAsync(CancellationToken.None);

            // Assert
            (await LoadAndReadWithNewServiceAsync()).Should().Be(written);
        }

        [Test]
        public void Write_DataThatIsNotAJsonObject_Throws()
        {
            // Arrange
            var section = new SettingsSection<string>(SectionKey, 1, (data, _) => data, "default");

            // Act
            Action act = () => _service.Write(section, "just a string");

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void Write_NullData_Throws()
        {
            // Act
            Action act = () => _service.Write(Section, null!);

            // Assert
            act.Should().Throw<ArgumentNullException>();
        }

        [TestCase("", 1)]
        [TestCase(" ", 1)]
        [TestCase(SectionKey, 0)]
        public void Read_InvalidSection_Throws(string key, int version)
        {
            // Arrange
            var section = Section with { Key = key, CurrentVersion = version };

            // Act
            Action act = () => _service.Read(section);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void Read_SectionWithoutDefault_Throws()
        {
            // Arrange
            var section = Section with { Default = null! };

            // Act
            Action act = () => _service.Read(section);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        private SettingsService CreateService()
        {
            return new SettingsService(
                _disk,
                new Core.Storage.JsonSerializer(),
                Substitute.For<IAudioService>(),
                Substitute.For<ILocalizationService>(),
                _input,
                _graphics,
                Defaults,
                [Language.English]);
        }

        private async Task<SampleSettingsDto> LoadAndReadWithNewServiceAsync()
        {
            using var reader = CreateService();
            await reader.LoadAsync(CancellationToken.None);
            return reader.Read(Section);
        }

        private static string CreateFile(JObject data, int version)
        {
            var file = new JObject
            {
                ["formatVersion"] = SettingsService.CurrentFormatVersion,
                ["core"] = new JObject(),
                ["sections"] = new JObject
                {
                    [SectionKey] = new JObject
                    {
                        ["version"] = version,
                        ["data"] = data,
                    },
                },
            };
            return file.ToString();
        }

        private static JObject ParseWithoutDates(string json)
        {
            using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
            return JObject.Load(reader);
        }

        private static OneOf<JObject, Corrupted> MigrateSample(JObject data, int fromVersion)
        {
            if (fromVersion != 1)
            {
                return new Corrupted($"No migration from version {fromVersion}.");
            }

            if (data["distance"] is null)
            {
                return new Corrupted("Version 1 has no distance.");
            }

            data["mode"] = "migrated";
            return data;
        }

        public sealed record SampleSettingsDto(float Distance, string Mode);
    }
}
