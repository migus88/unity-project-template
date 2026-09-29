using System;
using System.Collections.Generic;
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
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NSubstitute.Extensions;
using NUnit.Framework;
using OneOf;
using R3;
using TestUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Success = OneOf.Types.Success;

namespace Core.Tests.Settings
{
    public sealed class SettingsServiceTests
    {
        private const string FilePath = "settings.json";
        private const string JumpOverridePath = "<Keyboard>/k";

        private static readonly SettingsDefaults Defaults = new(1f, 0.8f, 0.9f, 0.7f, Language.English);
        private static readonly Resolution DeviceResolution = CreateResolution(1920, 1080, 60, 1);

        private InMemoryFileStorage _disk = null!;
        private IFileStorage _storage = null!;
        private IAudioService _audio = null!;
        private ILocalizationService _localization = null!;
        private IInputService _input = null!;
        private IGraphicsDevice _graphics = null!;
        private GameInput _actions = null!;
        private SettingsService _service = null!;

        [SetUp]
        public void SetUp()
        {
            _disk = new InMemoryFileStorage();
            _storage = Substitute.For<IFileStorage>();
            _storage.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => _disk.ReadAsync(call.ArgAt<string>(0), call.ArgAt<CancellationToken>(1)));
            _storage.WriteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => _disk.WriteAsync(call.ArgAt<string>(0), call.ArgAt<string>(1), call.ArgAt<CancellationToken>(2)));
            _audio = Substitute.For<IAudioService>();
            _localization = Substitute.For<ILocalizationService>();
            _actions = new GameInput();
            _input = Substitute.For<IInputService>();
            _input.Actions.Returns(_actions);
            _graphics = Substitute.For<IGraphicsDevice>();
            _graphics.QualityLevel.Returns(1);
            _graphics.QualityLevelCount.Returns(3);
            _graphics.FullScreenMode.Returns(FullScreenMode.FullScreenWindow);
            _graphics.Resolution.Returns(DeviceResolution);
            _graphics.VSync.Returns(true);
            _service = CreateService();
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
            Object.DestroyImmediate(_actions.asset);
        }

        [Test]
        public void Current_BeforeLoad_IsDefaultState()
        {
            // Act
            var state = _service.Current.CurrentValue;

            // Assert
            state.Should().Be(DefaultState());
        }

        [Test]
        public void Constructor_UnsupportedDefaultLanguage_Throws()
        {
            // Act
            Action act = () => CreateService(Defaults with { Language = Language.Polish }, [Language.English]);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void Constructor_DefaultVolumeOutOfRange_Throws()
        {
            // Act
            Action act = () => CreateService(Defaults with { MusicVolume = 1.5f }, [Language.English]);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public async Task LoadAsync_MissingFile_AppliesDefaultsAndSavesThem()
        {
            // Act
            await _service.LoadAsync(CancellationToken.None);

            // Assert
            _service.Current.CurrentValue.Should().Be(DefaultState());
            _audio.Received(1).SetVolume(AudioChannel.Master, 1f);
            _audio.Received(1).SetVolume(AudioChannel.Music, 0.8f);
            _audio.Received(1).SetVolume(AudioChannel.Sfx, 0.9f);
            _audio.Received(1).SetVolume(AudioChannel.Ui, 0.7f);
            _localization.Received(1).SetLanguage(Language.English);
            _disk.Files.Should().ContainKey(FilePath);
            (await LoadWithNewServiceAsync()).Should().Be(DefaultState());
        }

        [Test]
        public async Task LoadAsync_ValidFile_AppliesStoredStateWithoutSaving()
        {
            // Arrange
            var stored = CustomState();
            _disk.Files[FilePath] = await SerializeWithNewServiceAsync(stored);
            var storedJson = _disk.Files[FilePath];

            // Act
            await _service.LoadAsync(CancellationToken.None);

            // Assert
            _service.Current.CurrentValue.Should().Be(stored);
            _audio.Received(1).SetVolume(AudioChannel.Master, 0.5f);
            _audio.Received(1).SetVolume(AudioChannel.Music, 0.25f);
            _audio.Received(1).SetVolume(AudioChannel.Sfx, 0f);
            _audio.Received(1).SetVolume(AudioChannel.Ui, 1f);
            _localization.Received(1).SetLanguage(Language.Polish);
            _graphics.Received(1).SetQualityLevel(2);
            _graphics.Received(1).SetVSync(false);
            _graphics.Received(1).SetScreen(stored.Resolution, FullScreenMode.Windowed);
            _ = _storage.DidNotReceive().WriteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
            _disk.Files[FilePath].Should().Be(storedJson);
        }

        [Test]
        public async Task LoadAsync_StoredBindingOverrides_AppliesThemToGameInput()
        {
            // Arrange
            _disk.Files[FilePath] = await SerializeWithNewServiceAsync(DefaultState() with { BindingOverridesJson = CreateJumpOverrideJson() });

            // Act
            await _service.LoadAsync(CancellationToken.None);

            // Assert
            _actions.Player.Jump.bindings[0].overridePath.Should().Be(JumpOverridePath);
        }

        [Test]
        public async Task SaveAsync_EnumFields_StoresNames()
        {
            // Arrange
            _service.Apply(CustomState());

            // Act
            await _service.SaveAsync(CancellationToken.None);

            // Assert
            var json = JObject.Parse(_disk.Files[FilePath]);
            json["language"]!.Value<string>().Should().Be("Polish");
            json["fullScreenMode"]!.Value<string>().Should().Be("Windowed");
        }

        [Test]
        public async Task LoadAsync_MalformedJson_WarnsAppliesDefaultsAndOverwritesFile()
        {
            // Arrange
            _disk.Files[FilePath] = "{ not json";
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Settings\] Settings file is corrupted"));

            // Act
            await _service.LoadAsync(CancellationToken.None);

            // Assert
            _service.Current.CurrentValue.Should().Be(DefaultState());
            _localization.Received(1).SetLanguage(Language.English);
            (await LoadWithNewServiceAsync()).Should().Be(DefaultState());
        }

        [Test]
        public async Task LoadAsync_UnsupportedFormatVersion_TreatsFileAsCorrupted()
        {
            // Arrange
            var json = JObject.Parse(await SerializeWithNewServiceAsync(CustomState()));
            json["formatVersion"] = 2;
            _disk.Files[FilePath] = json.ToString();
            LogAssert.Expect(LogType.Warning, new Regex("Unsupported format version 2"));

            // Act
            await _service.LoadAsync(CancellationToken.None);

            // Assert
            _service.Current.CurrentValue.Should().Be(DefaultState());
        }

        [Test]
        public async Task LoadAsync_InvalidValues_UsesDefaultsForThemKeepsTheRestAndSaves()
        {
            // Arrange
            var json = JObject.Parse(await SerializeWithNewServiceAsync(CustomState()));
            json["musicVolume"] = 3f;
            json["language"] = "Klingon";
            json["qualityLevel"] = 7;
            json["fullScreenMode"] = "Sideways";
            json["resolutionWidth"] = 0;
            _disk.Files[FilePath] = json.ToString();
            LogAssert.Expect(LogType.Warning, "[Settings] Settings file has invalid values for musicVolume, language, qualityLevel, fullScreenMode, resolution, using defaults for them.");

            // Act
            await _service.LoadAsync(CancellationToken.None);

            // Assert
            var expected = CustomState() with
            {
                MusicVolume = Defaults.MusicVolume,
                Language = Language.English,
                QualityLevel = 1,
                FullScreenMode = FullScreenMode.FullScreenWindow,
                Resolution = DeviceResolution,
            };
            _service.Current.CurrentValue.Should().Be(expected);
            (await LoadWithNewServiceAsync()).Should().Be(expected);
        }

        [Test]
        public async Task LoadAsync_SupportedEnumValueNotInSupportedLanguages_UsesDefaultLanguage()
        {
            // Arrange
            _service.Dispose();
            _service = CreateService(Defaults, [Language.English]);
            var json = JObject.Parse(await SerializeWithNewServiceAsync(DefaultState()));
            json["language"] = "Polish";
            _disk.Files[FilePath] = json.ToString();
            LogAssert.Expect(LogType.Warning, "[Settings] Settings file has invalid values for language, using defaults for them.");

            // Act
            await _service.LoadAsync(CancellationToken.None);

            // Assert
            _service.Current.CurrentValue.Language.Should().Be(Language.English);
            _localization.DidNotReceive().SetLanguage(Language.Polish);
        }

        [Test]
        public async Task LoadAsync_MissingFields_UsesDefaultsSilentlyAndSaves()
        {
            // Arrange
            _disk.Files[FilePath] = "{ \"formatVersion\": 1, \"masterVolume\": 0.3 }";

            // Act
            await _service.LoadAsync(CancellationToken.None);

            // Assert
            var expected = DefaultState() with { MasterVolume = 0.3f };
            _service.Current.CurrentValue.Should().Be(expected);
            (await LoadWithNewServiceAsync()).Should().Be(expected);
            JObject.Parse(_disk.Files[FilePath])["musicVolume"]!.Value<float>().Should().Be(Defaults.MusicVolume);
        }

        [Test]
        public async Task LoadAsync_InvalidBindingOverrides_ResetsBindingsWarnsAndSaves()
        {
            // Arrange
            var json = JObject.Parse(await SerializeWithNewServiceAsync(DefaultState()));
            json["bindingOverridesJson"] = "definitely not json";
            _disk.Files[FilePath] = json.ToString();
            _actions.Player.Jump.ApplyBindingOverride(0, JumpOverridePath);
            LogAssert.Expect(LogType.Warning, "[Settings] Settings file has invalid values for bindingOverridesJson, using defaults for them.");

            // Act
            await _service.LoadAsync(CancellationToken.None);

            // Assert
            _service.Current.CurrentValue.BindingOverridesJson.Should().BeEmpty();
            _actions.Player.Jump.bindings[0].overridePath.Should().BeNull();
            (await LoadWithNewServiceAsync()).BindingOverridesJson.Should().BeEmpty();
        }

        [Test]
        public async Task LoadAsync_ReadError_WarnsAppliesDefaultsAndKeepsFile()
        {
            // Arrange
            _storage.Configure().ReadAsync(FilePath, Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<OneOf<string, NotFound, Error>>(new Error("disk on fire")));
            LogAssert.Expect(LogType.Warning, "[Settings] Settings could not be read, using defaults without overwriting the file: disk on fire");

            // Act
            await _service.LoadAsync(CancellationToken.None);

            // Assert
            _service.Current.CurrentValue.Should().Be(DefaultState());
            _localization.Received(1).SetLanguage(Language.English);
            _ = _storage.DidNotReceive().WriteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task LoadAsync_SaveOfDefaultsFails_WarnsAndCompletes()
        {
            // Arrange
            _storage.Configure().WriteAsync(FilePath, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<OneOf<Success, Error>>(new Error("read-only disk")));
            LogAssert.Expect(LogType.Warning, "[Settings] Settings could not be saved: read-only disk");

            // Act
            await _service.LoadAsync(CancellationToken.None);

            // Assert
            _service.Current.CurrentValue.Should().Be(DefaultState());
        }

        [Test]
        public async Task LoadAsync_Cancelled_Throws()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            Func<Task> act = () => _service.LoadAsync(cts.Token).AsTask();

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Test]
        public void Apply_ValidState_AppliesSideEffectsAndUpdatesCurrent()
        {
            // Arrange
            var state = CustomState() with { BindingOverridesJson = CreateJumpOverrideJson() };
            var emitted = new List<SettingsState>();
            using var subscription = _service.Current.Subscribe(emitted.Add);

            // Act
            _service.Apply(state);

            // Assert
            _service.Current.CurrentValue.Should().Be(state);
            emitted.Should().Equal(DefaultState(), state);
            _audio.Received(1).SetVolume(AudioChannel.Music, 0.25f);
            _localization.Received(1).SetLanguage(Language.Polish);
            _graphics.Received(1).SetQualityLevel(2);
            _actions.Player.Jump.bindings[0].overridePath.Should().Be(JumpOverridePath);
        }

        [Test]
        public void Apply_EmptyBindingOverrides_RemovesExistingOverrides()
        {
            // Arrange
            _service.Apply(DefaultState() with { BindingOverridesJson = CreateJumpOverrideJson() });

            // Act
            _service.Apply(DefaultState());

            // Assert
            _actions.Player.Jump.bindings[0].overridePath.Should().BeNull();
        }

        [Test]
        public void Apply_UnchangedBindingOverrides_DoesNotReloadThem()
        {
            // Arrange
            _actions.Player.Jump.ApplyBindingOverride(0, JumpOverridePath);

            // Act
            _service.Apply(CustomState());

            // Assert
            _service.Current.CurrentValue.Should().Be(CustomState());
            _actions.Player.Jump.bindings[0].overridePath.Should().Be(JumpOverridePath);
        }

        [Test]
        public void Apply_ValidState_DoesNotSave()
        {
            // Act
            _service.Apply(CustomState());

            // Assert
            _ = _storage.DidNotReceive().WriteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [TestCase(-0.1f)]
        [TestCase(1.1f)]
        [TestCase(float.NaN)]
        public void Apply_VolumeOutOfRange_ThrowsAndChangesNothing(float volume)
        {
            // Act
            Action act = () => _service.Apply(DefaultState() with { SfxVolume = volume });

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
            _service.Current.CurrentValue.Should().Be(DefaultState());
            _audio.DidNotReceiveWithAnyArgs().SetVolume(default, default);
        }

        [TestCase(Language.None)]
        [TestCase((Language)99)]
        public void Apply_UnsupportedLanguage_Throws(Language language)
        {
            // Act
            Action act = () => _service.Apply(DefaultState() with { Language = language });

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
            _localization.DidNotReceiveWithAnyArgs().SetLanguage(default);
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void Apply_QualityLevelOutOfRange_Throws(int qualityLevel)
        {
            // Act
            Action act = () => _service.Apply(DefaultState() with { QualityLevel = qualityLevel });

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void Apply_UndefinedFullScreenMode_Throws()
        {
            // Act
            Action act = () => _service.Apply(DefaultState() with { FullScreenMode = (FullScreenMode)42 });

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void Apply_NonPositiveResolution_Throws()
        {
            // Act
            Action act = () => _service.Apply(DefaultState() with { Resolution = CreateResolution(0, 1080, 60, 1) });

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void Apply_InvalidBindingOverrides_ThrowsAndChangesNothing()
        {
            // Act
            Action act = () => _service.Apply(CustomState() with { BindingOverridesJson = "definitely not json" });

            // Assert
            act.Should().Throw<ArgumentException>();
            _service.Current.CurrentValue.Should().Be(DefaultState());
            _audio.DidNotReceiveWithAnyArgs().SetVolume(default, default);
        }

        [Test]
        public void Apply_InvalidBindingOverrides_KeepsPreviousOverrides()
        {
            // Arrange
            _service.Apply(DefaultState() with { BindingOverridesJson = CreateJumpOverrideJson() });

            // Act
            Action act = () => _service.Apply(DefaultState() with { BindingOverridesJson = "definitely not json" });

            // Assert
            act.Should().Throw<ArgumentException>();
            _actions.Player.Jump.bindings[0].overridePath.Should().Be(JumpOverridePath);
        }

        [Test]
        public async Task SaveAsync_AfterApply_WritesStateThatLoadsBack()
        {
            // Arrange
            var state = CustomState() with { BindingOverridesJson = CreateJumpOverrideJson() };
            _service.Apply(state);

            // Act
            var result = await _service.SaveAsync(CancellationToken.None);

            // Assert
            result.Should().BeCase<Success>();
            (await LoadWithNewServiceAsync()).Should().Be(state);
        }

        [Test]
        public async Task SaveAsync_WriteFails_ReturnsError()
        {
            // Arrange
            _storage.Configure().WriteAsync(FilePath, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<OneOf<Success, Error>>(new Error("read-only disk")));

            // Act
            var result = await _service.SaveAsync(CancellationToken.None);

            // Assert
            result.Should().BeCase<Error>().Which.Message.Should().Be("read-only disk");
        }

        [Test]
        public async Task SaveAsync_Cancelled_ThrowsWithoutWriting()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            Func<Task> act = () => _service.SaveAsync(cts.Token).AsTask();

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
            _ = _storage.DidNotReceive().WriteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        private SettingsService CreateService()
        {
            return CreateService(Defaults, [Language.English, Language.Polish]);
        }

        private SettingsService CreateService(SettingsDefaults defaults, IReadOnlyCollection<Language> supportedLanguages)
        {
            return new SettingsService(_storage, new JsonSerializer(), _audio, _localization, _input, _graphics, defaults, supportedLanguages);
        }

        private async Task<string> SerializeWithNewServiceAsync(SettingsState state)
        {
            using var writer = CreateService();
            writer.Apply(state);
            await writer.SaveAsync(CancellationToken.None);
            _audio.ClearReceivedCalls();
            _localization.ClearReceivedCalls();
            _graphics.ClearReceivedCalls();
            _storage.ClearReceivedCalls();
            _actions.RemoveAllBindingOverrides();
            return _disk.Files[FilePath];
        }

        private async Task<SettingsState> LoadWithNewServiceAsync()
        {
            using var reader = CreateService();
            await reader.LoadAsync(CancellationToken.None);
            return reader.Current.CurrentValue;
        }

        private string CreateJumpOverrideJson()
        {
            var other = new GameInput();

            try
            {
                other.Player.Jump.ApplyBindingOverride(0, JumpOverridePath);
                return other.asset.SaveBindingOverridesAsJson();
            }
            finally
            {
                Object.DestroyImmediate(other.asset);
            }
        }

        private static SettingsState DefaultState()
        {
            return new SettingsState(1f, 0.8f, 0.9f, 0.7f, Language.English, 1, FullScreenMode.FullScreenWindow, DeviceResolution, true, string.Empty);
        }

        private static SettingsState CustomState()
        {
            return new SettingsState(0.5f, 0.25f, 0f, 1f, Language.Polish, 2, FullScreenMode.Windowed, CreateResolution(1280, 720, 144, 1), false, string.Empty);
        }

        private static Resolution CreateResolution(int width, int height, uint numerator, uint denominator)
        {
            return new Resolution
            {
                width = width,
                height = height,
                refreshRateRatio = new RefreshRate { numerator = numerator, denominator = denominator },
            };
        }
    }
}
