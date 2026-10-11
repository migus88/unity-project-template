using AwesomeAssertions;
using Core;
using Core.Localization;
using Core.Privacy;
using Core.RemoteConfig;
using NSubstitute;
using NUnit.Framework;
using R3;
using Settings.Privacy;
using Shared.UI;
using TestUtils;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Settings.Tests
{
    public sealed class PrivacyPresenterTests
    {
        private const string PolicyUrl = "https://example.com/privacy";
        private const int PolicyVersion = 4;

        private GameObject _root = null!;
        private PrivacyRowView _view = null!;
        private Button _nextButton = null!;
        private TMP_Text _consentValue = null!;
        private Button _policyButton = null!;
        private TMP_InputField _installId = null!;
        private GameObject _deletionHint = null!;
        private ReactiveProperty<Language> _language = null!;
        private ILocalizationService _localization = null!;
        private IApplicationService _application = null!;
        private FakeAnalytics _analytics = null!;
        private PrivacyPresenter? _presenter;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Privacy");
            var selector = CreateSelector(_root.transform);
            _policyButton = new GameObject("Policy", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
            _policyButton.transform.SetParent(_root.transform, false);
            var input = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            input.transform.SetParent(_root.transform, false);
            _installId = input.GetComponent<TMP_InputField>();
            _deletionHint = new GameObject("Hint");
            _deletionHint.transform.SetParent(_root.transform, false);
            _view = _root.AddComponent<PrivacyRowView>();

            using var serializedView = new SerializedObject(_view);
            serializedView.FindProperty("_consent").objectReferenceValue = selector;
            serializedView.FindProperty("_policyButton").objectReferenceValue = _policyButton;
            serializedView.FindProperty("_installId").objectReferenceValue = _installId;
            serializedView.FindProperty("_deletionHint").objectReferenceValue = _deletionHint;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            _language = new ReactiveProperty<Language>(Language.English);
            _localization = Substitute.For<ILocalizationService>();
            _localization.Current.Returns(_language);
            _localization.Get(Arg.Any<TextKey>()).Returns(call => $"{call.Arg<TextKey>().Key}:{_language.Value}");
            _application = Substitute.For<IApplicationService>();
            _analytics = new FakeAnalytics();
        }

        [TearDown]
        public void TearDown()
        {
            _presenter?.Dispose();
            _presenter = null;
            _language.Dispose();
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void Start_Granted_ShowsYesAndInstallId()
        {
            // Arrange
            var consent = new FakeConsentService(new ConsentSnapshot(ConsentState.Granted, PolicyVersion, "install-a"));

            // Act
            StartPresenter(consent, new PrivacyPolicy(PolicyUrl, PolicyVersion));

            // Assert
            _consentValue.text.Should().Be("yes:English");
            _installId.text.Should().Be("install-a");
            _deletionHint.activeSelf.Should().BeTrue();
        }

        [Test]
        public void Start_UnknownWithoutInstallId_ShowsNoAndNoneTextAndHidesHint()
        {
            // Arrange
            var consent = new FakeConsentService(new ConsentSnapshot(ConsentState.Unknown, null, null));

            // Act
            StartPresenter(consent, new PrivacyPolicy(PolicyUrl, PolicyVersion));

            // Assert
            _consentValue.text.Should().Be("no:English");
            _installId.text.Should().Be("install_id_none:English");
            _deletionHint.activeSelf.Should().BeFalse();
        }

        [Test]
        public void ConsentToggled_WhileGranted_DeniesAndShowsRegeneratedId()
        {
            // Arrange
            var consent = new FakeConsentService(new ConsentSnapshot(ConsentState.Granted, PolicyVersion, "install-a"));
            StartPresenter(consent, new PrivacyPolicy(PolicyUrl, PolicyVersion));

            // Act
            _nextButton.onClick.Invoke();

            // Assert
            consent.DeniedVersions.Should().Equal(PolicyVersion);
            _consentValue.text.Should().Be("no:English");
            _installId.text.Should().Be(consent.InstallId).And.NotBe("install-a");
            _deletionHint.activeSelf.Should().BeTrue();
            _nextButton.interactable.Should().BeTrue();
        }

        [Test]
        public void ConsentToggled_WhileDenied_GrantsWithPolicyVersion()
        {
            // Arrange
            var consent = new FakeConsentService(new ConsentSnapshot(ConsentState.Denied, 1, null));
            StartPresenter(consent, new PrivacyPolicy(PolicyUrl, PolicyVersion));

            // Act
            _nextButton.onClick.Invoke();

            // Assert
            consent.GrantedVersions.Should().Equal(PolicyVersion);
            _consentValue.text.Should().Be("yes:English");
            _installId.text.Should().Be(consent.InstallId);
        }

        [Test]
        public void ConsentToggled_Grant_TracksConsentChanged()
        {
            // Arrange
            var consent = new FakeConsentService(new ConsentSnapshot(ConsentState.Unknown, null, null));
            StartPresenter(consent, new PrivacyPolicy(PolicyUrl, PolicyVersion));

            // Act
            _nextButton.onClick.Invoke();

            // Assert
            _analytics.OfType<ConsentChangedEvent>().Should().Equal(new ConsentChangedEvent(true));
        }

        [Test]
        public void PolicyClicked_OpensPolicyUrl()
        {
            // Arrange
            var consent = new FakeConsentService(new ConsentSnapshot(ConsentState.Unknown, null, null));
            StartPresenter(consent, new PrivacyPolicy(PolicyUrl, PolicyVersion));

            // Act
            _policyButton.onClick.Invoke();

            // Assert
            _application.Received(1).OpenUrl(PolicyUrl);
        }

        [Test]
        public void Start_UnpublishedPolicy_HidesPolicyLink()
        {
            // Arrange
            var consent = new FakeConsentService(new ConsentSnapshot(ConsentState.Unknown, null, null));

            // Act
            StartPresenter(consent, PrivacyPolicy.Unpublished);

            // Assert
            _policyButton.gameObject.activeSelf.Should().BeFalse();
        }

        [Test]
        public void LanguageChanged_RerendersConsentAndNoneText()
        {
            // Arrange
            var consent = new FakeConsentService(new ConsentSnapshot(ConsentState.Unknown, null, null));
            StartPresenter(consent, new PrivacyPolicy(PolicyUrl, PolicyVersion));

            // Act
            _language.Value = Language.Polish;

            // Assert
            _consentValue.text.Should().Be("no:Polish");
            _installId.text.Should().Be("install_id_none:Polish");
        }

        private void StartPresenter(FakeConsentService consent, PrivacyPolicy policy)
        {
            _presenter = new PrivacyPresenter(_view, consent, policy, _application, _localization, _analytics, Substitute.For<IRemoteConfigService>());
            _presenter.Start();
        }

        private SelectorView CreateSelector(Transform parent)
        {
            var selectorObject = new GameObject("Consent");
            selectorObject.transform.SetParent(parent, false);
            var previous = new GameObject("Previous", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
            previous.transform.SetParent(selectorObject.transform, false);
            _nextButton = new GameObject("Next", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
            _nextButton.transform.SetParent(selectorObject.transform, false);
            _consentValue = new GameObject("Value", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            _consentValue.transform.SetParent(selectorObject.transform, false);
            var selector = selectorObject.AddComponent<SelectorView>();

            using var serializedSelector = new SerializedObject(selector);
            serializedSelector.FindProperty("_previousButton").objectReferenceValue = previous;
            serializedSelector.FindProperty("_nextButton").objectReferenceValue = _nextButton;
            serializedSelector.FindProperty("_valueLabel").objectReferenceValue = _consentValue;
            serializedSelector.ApplyModifiedPropertiesWithoutUndo();
            return selector;
        }
    }
}
