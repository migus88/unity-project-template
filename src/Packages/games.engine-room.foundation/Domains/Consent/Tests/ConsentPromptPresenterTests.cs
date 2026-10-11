using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using Consent.Prompt;
using Core;
using Core.Analytics;
using Core.Audio;
using Core.Domains;
using Core.Input;
using Core.Privacy;
using Core.RemoteConfig;
using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using TestUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Consent.Tests
{
    public sealed class ConsentPromptPresenterTests
    {
        private const string PolicyUrl = "https://example.com/privacy";
        private const int PolicyVersion = 3;

        private GameObject _root = null!;
        private ConsentPromptView _view = null!;
        private Button _yes = null!;
        private Button _no = null!;
        private Button _close = null!;
        private Button _policy = null!;
        private FakeConsentService _consent = null!;
        private IRemoteConfigService _remoteConfig = null!;
        private IApplicationService _application = null!;
        private IInputService _input = null!;
        private IAnalytics _analytics = null!;
        private IUiInteractionSounds _uiSounds = null!;
        private DomainCompletion<ConsentResult> _completion = null!;
        private ConsentPromptPresenter? _presenter;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Consent");
            _yes = CreateButton("Yes");
            _no = CreateButton("No");
            _close = CreateButton("Close");
            _policy = CreateButton("Policy");
            _view = _root.AddComponent<ConsentPromptView>();

            using var serializedView = new SerializedObject(_view);
            serializedView.FindProperty("_yes").objectReferenceValue = _yes;
            serializedView.FindProperty("_no").objectReferenceValue = _no;
            serializedView.FindProperty("_close").objectReferenceValue = _close;
            serializedView.FindProperty("_policy").objectReferenceValue = _policy;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            _consent = new FakeConsentService(new ConsentSnapshot(ConsentState.Unknown, null, null));
            _remoteConfig = Substitute.For<IRemoteConfigService>();
            _application = Substitute.For<IApplicationService>();
            _input = Substitute.For<IInputService>();
            _input.Push(Arg.Any<InputMaps>()).Returns(Substitute.For<IDisposable>());
            _analytics = Substitute.For<IAnalytics>();
            _uiSounds = Substitute.For<IUiInteractionSounds>();
            _completion = new DomainCompletion<ConsentResult>();
        }

        [TearDown]
        public void TearDown()
        {
            _presenter?.Dispose();
            _presenter = null;
            Object.DestroyImmediate(_root);
        }

        [Test]
        public async Task YesClicked_GrantsPolicyVersionAndCompletesGranted()
        {
            // Arrange
            StartPresenter(new PrivacyPolicy(PolicyUrl, PolicyVersion));

            // Act
            _yes.onClick.Invoke();

            // Assert
            _consent.GrantedVersions.Should().Equal(PolicyVersion);
            _consent.DeniedVersions.Should().BeEmpty();
            (await _completion.Task).Should().BeCase<ConsentResult.Granted>();
        }

        [Test]
        public void YesClicked_RemoteVersionAboveBuiltIn_GrantsRemoteVersion()
        {
            // Arrange
            _remoteConfig.Get(CoreFlags.PrivacyPolicyVersion).Returns(PolicyVersion + 2);
            StartPresenter(new PrivacyPolicy(PolicyUrl, PolicyVersion));

            // Act
            _yes.onClick.Invoke();

            // Assert
            _consent.GrantedVersions.Should().Equal(PolicyVersion + 2);
        }

        [Test]
        public void YesClicked_TracksConsentGrantedAfterGrant()
        {
            // Arrange
            var isGrantedWhenTracked = false;
            _analytics.When(analytics => analytics.Track(Arg.Any<ConsentGrantedEvent>())).Do(_ => isGrantedWhenTracked = _consent.IsGranted);
            StartPresenter(new PrivacyPolicy(PolicyUrl, PolicyVersion));

            // Act
            _yes.onClick.Invoke();

            // Assert
            _analytics.Received(1).Track(new ConsentGrantedEvent(PolicyVersion));
            isGrantedWhenTracked.Should().BeTrue();
        }

        [Test]
        public async Task NoClicked_DeniesWithoutAnalyticsAndCompletesDenied()
        {
            // Arrange
            StartPresenter(new PrivacyPolicy(PolicyUrl, PolicyVersion));

            // Act
            _no.onClick.Invoke();

            // Assert
            _consent.DeniedVersions.Should().Equal(PolicyVersion);
            _consent.GrantedVersions.Should().BeEmpty();
            _analytics.DidNotReceiveWithAnyArgs().Track(default(ConsentGrantedEvent));
            (await _completion.Task).Should().BeCase<ConsentResult.Denied>();
        }

        [Test]
        public async Task CloseClicked_CompletesDenied()
        {
            // Arrange
            StartPresenter(new PrivacyPolicy(PolicyUrl, PolicyVersion));

            // Act
            _close.onClick.Invoke();

            // Assert
            _consent.DeniedVersions.Should().Equal(PolicyVersion);
            (await _completion.Task).Should().BeCase<ConsentResult.Denied>();
        }

        [Test]
        public async Task Decline_PlaysBackSoundAndCompletesDenied()
        {
            // Arrange
            StartPresenter(new PrivacyPolicy(PolicyUrl, PolicyVersion));

            // Act
            _presenter!.Decline();

            // Assert
            _uiSounds.Received(1).Play(UiInteraction.Back);
            _consent.DeniedVersions.Should().Equal(PolicyVersion);
            (await _completion.Task).Should().BeCase<ConsentResult.Denied>();
        }

        [Test]
        public async Task Decline_AfterYes_IsIgnored()
        {
            // Arrange
            StartPresenter(new PrivacyPolicy(PolicyUrl, PolicyVersion));
            _yes.onClick.Invoke();

            // Act
            _presenter!.Decline();

            // Assert
            _consent.GrantedVersions.Should().Equal(PolicyVersion);
            _consent.DeniedVersions.Should().BeEmpty();
            _uiSounds.DidNotReceive().Play(UiInteraction.Back);
            (await _completion.Task).Should().BeCase<ConsentResult.Granted>();
        }

        [Test]
        public void PolicyClicked_OpensPolicyUrl()
        {
            // Arrange
            StartPresenter(new PrivacyPolicy(PolicyUrl, PolicyVersion));

            // Act
            _policy.onClick.Invoke();

            // Assert
            _application.Received(1).OpenUrl(PolicyUrl);
            _completion.IsCompleted.Should().BeFalse();
        }

        [Test]
        public void Start_UnpublishedPolicy_HidesPolicyLink()
        {
            // Act
            StartPresenter(PrivacyPolicy.Unpublished);

            // Assert
            _policy.gameObject.activeSelf.Should().BeFalse();
        }

        private void StartPresenter(PrivacyPolicy policy)
        {
            _presenter = new ConsentPromptPresenter(_view, _consent, policy, _remoteConfig, _application, _input, _analytics, _uiSounds, _completion);
            _presenter.Start();
        }


        private Button CreateButton(string name)
        {
            var button = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
            button.transform.SetParent(_root.transform, false);
            return button;
        }
    }
}
