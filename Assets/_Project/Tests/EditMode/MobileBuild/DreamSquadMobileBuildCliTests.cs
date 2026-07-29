using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wassup.Editor.MobileBuild;

namespace Wassup.Tests.EditMode.MobileBuild
{
    public sealed class DreamSquadMobileBuildCliTests
    {
        private const string StorePassword = "store secret";
        private const string KeyPassword = "key secret";
        private string projectRoot;

        [SetUp]
        public void SetUp()
        {
            projectRoot = Path.GetFullPath(
                Path.Combine(Path.GetTempPath(), "dreamsquad-mobile-build-tests"));
        }

        [Test]
        public void AndroidRequest_ReadsRequiredInputsAndFallsBackToStorePassword()
        {
            var environment = CreateAndroidEnvironment();
            environment[MobileBuildRequest.AndroidKeyPasswordEnvironmentName] = string.Empty;

            var request = CreateRequest(MobileBuildPlatform.Android, environment);

            Assert.That(request.Version, Is.EqualTo("0.1.0"));
            Assert.That(request.BuildNumber, Is.EqualTo(123));
            Assert.That(request.AndroidKeystorePassword, Is.EqualTo(StorePassword));
            Assert.That(request.AndroidKeyPassword, Is.EqualTo(StorePassword));
            Assert.That(request.OutputPath, Does.EndWith(".apk"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("1")]
        [TestCase("1.2.3.4")]
        [TestCase("v1.2.3")]
        [TestCase("1.2-dev")]
        [TestCase("\uFF11.2.3")]
        public void Request_RejectsInvalidVersion(string version)
        {
            var environment = CreateAndroidEnvironment();
            environment[MobileBuildRequest.VersionEnvironmentName] = version;

            var exception = Assert.Throws<MobileBuildException>(
                () => CreateRequest(MobileBuildPlatform.Android, environment));

            Assert.That(exception.Message, Does.Contain(MobileBuildRequest.VersionEnvironmentName));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("0")]
        [TestCase("-1")]
        [TestCase("1.0")]
        [TestCase("2147483648")]
        public void Request_RejectsInvalidBuildNumber(string buildNumber)
        {
            var environment = CreateAndroidEnvironment();
            environment[MobileBuildRequest.BuildNumberEnvironmentName] = buildNumber;

            var exception = Assert.Throws<MobileBuildException>(
                () => CreateRequest(MobileBuildPlatform.Android, environment));

            Assert.That(
                exception.Message,
                Does.Contain(MobileBuildRequest.BuildNumberEnvironmentName));
        }

        [TestCase(MobileBuildRequest.AndroidKeystoreEnvironmentName)]
        [TestCase(MobileBuildRequest.AndroidKeystorePasswordEnvironmentName)]
        public void AndroidRequest_RejectsMissingSigningInput(string missingName)
        {
            var environment = CreateAndroidEnvironment();
            environment.Remove(missingName);

            var exception = Assert.Throws<MobileBuildException>(
                () => CreateRequest(MobileBuildPlatform.Android, environment));

            Assert.That(exception.Message, Does.Contain(missingName));
        }

        [Test]
        public void AndroidRequest_RejectsUnreadableKeystoreWithoutEchoingPath()
        {
            var environment = CreateAndroidEnvironment();
            environment[MobileBuildRequest.AndroidKeystoreEnvironmentName] =
                "private/somnia-dev.keystore";

            var exception = Assert.Throws<MobileBuildException>(() =>
                MobileBuildRequest.FromEnvironment(
                    MobileBuildPlatform.Android,
                    name => environment.TryGetValue(name, out var value) ? value : null,
                    _ => false,
                    projectRoot));

            Assert.That(
                exception.Message,
                Does.Contain(MobileBuildRequest.AndroidKeystoreEnvironmentName));
            Assert.That(exception.Message, Does.Not.Contain("private"));
        }

        [Test]
        public void IosRequest_DoesNotRequireAndroidSigningInputs()
        {
            var environment = CreateCommonEnvironment(MobileBuildPlatform.Ios);

            var request = CreateRequest(MobileBuildPlatform.Ios, environment);

            Assert.That(request.OutputPath, Does.EndWith(Path.Combine("iOS", "Xcode")));
            Assert.That(request.DescribeSafe(), Does.Contain("signingConfigured=False"));
        }

        [Test]
        public void Request_RejectsOutputOutsideBuildsMobile()
        {
            var environment = CreateCommonEnvironment(MobileBuildPlatform.Ios);
            environment[MobileBuildRequest.OutputEnvironmentName] =
                Path.Combine(projectRoot, "outside", "Xcode");

            var exception = Assert.Throws<MobileBuildException>(
                () => CreateRequest(MobileBuildPlatform.Ios, environment));

            Assert.That(exception.Message, Does.Contain(MobileBuildRequest.OutputEnvironmentName));
        }

        [TestCase("android")]
        [TestCase("ios")]
        public void Request_RejectsExistingOutput(string platformName)
        {
            var platform = ParsePlatform(platformName);
            var request = CreateRequest(platform, platform == MobileBuildPlatform.Android
                ? CreateAndroidEnvironment()
                : CreateCommonEnvironment(platform));

            Assert.Throws<MobileBuildException>(
                () => request.EnsureOutputDoesNotExist(
                    path => path == request.OutputPath,
                    _ => false));
            Assert.Throws<MobileBuildException>(
                () => request.EnsureOutputDoesNotExist(
                    _ => false,
                    path => path == request.OutputPath));
        }

        [Test]
        public void SafeDescription_DoesNotContainSigningSecretsOrPath()
        {
            var environment = CreateAndroidEnvironment();
            environment[MobileBuildRequest.AndroidKeystoreEnvironmentName] =
                "very-private/somnia-dev.keystore";

            var request = MobileBuildRequest.FromEnvironment(
                MobileBuildPlatform.Android,
                name => environment.TryGetValue(name, out var value) ? value : null,
                _ => true,
                projectRoot);
            var description = request.DescribeSafe();

            Assert.That(description, Does.Not.Contain("very-private"));
            Assert.That(description, Does.Not.Contain(StorePassword));
            Assert.That(description, Does.Not.Contain(KeyPassword));
        }

        [TestCase("unity")]
        [TestCase("company")]
        [TestCase("product")]
        [TestCase("target")]
        [TestCase("support")]
        [TestCase("app-id")]
        [TestCase("scenes")]
        [TestCase("orientation")]
        [TestCase("min-sdk")]
        [TestCase("backend")]
        [TestCase("architecture")]
        public void AndroidPreflight_RejectsConfigurationDrift(string drift)
        {
            var state = CreateValidPreflight(MobileBuildPlatform.Android);
            ApplyDrift(state, drift);

            Assert.Throws<MobileBuildException>(
                () => state.Validate(MobileBuildPlatform.Android));
        }

        [Test]
        public void IosPreflight_RejectsDeploymentTargetDrift()
        {
            var state = CreateValidPreflight(MobileBuildPlatform.Ios);
            state.IosDeploymentTarget = "14.0";

            Assert.Throws<MobileBuildException>(
                () => state.Validate(MobileBuildPlatform.Ios));
        }

        [Test]
        public void IosPreflight_RejectsTargetDeviceDrift()
        {
            var state = CreateValidPreflight(MobileBuildPlatform.Ios);
            state.IosTargetDevice = iOSTargetDevice.iPhoneOnly;

            Assert.Throws<MobileBuildException>(
                () => state.Validate(MobileBuildPlatform.Ios));
        }

        [TestCase("android")]
        [TestCase("ios")]
        public void Preflight_AcceptsExpectedConfiguration(string platformName)
        {
            var platform = ParsePlatform(platformName);
            Assert.DoesNotThrow(() => CreateValidPreflight(platform).Validate(platform));
        }

        [Test]
        public void Preflight_AcceptsTrackedSerializedScreenAutoRotation()
        {
            var state = CreateValidPreflight(MobileBuildPlatform.Android);
            state.DefaultOrientation =
                (UIOrientation)MobileBuildPreflightState.SerializedScreenAutoRotationValue;
            state.AllowLandscapeLeft = true;
            state.AllowLandscapeRight = true;

            Assert.DoesNotThrow(() => state.Validate(MobileBuildPlatform.Android));
        }

        // 세로가 가능해지는 설정은 전부 거부한다 — 이 검사의 목적이 그것이다.
        [TestCase((int)UIOrientation.Portrait, false, false, true, true)]
        [TestCase((int)UIOrientation.PortraitUpsideDown, false, false, true, true)]
        [TestCase((int)UIOrientation.AutoRotation, true, false, true, true)]
        [TestCase((int)UIOrientation.AutoRotation, false, true, true, true)]
        [TestCase((int)UIOrientation.AutoRotation, false, false, false, true)]
        [TestCase((int)UIOrientation.AutoRotation, false, false, true, false)]
        [TestCase(MobileBuildPreflightState.SerializedScreenAutoRotationValue, true, false, true, true)]
        [TestCase(MobileBuildPreflightState.SerializedScreenAutoRotationValue, false, true, true, true)]
        [TestCase(MobileBuildPreflightState.SerializedScreenAutoRotationValue, false, false, false, true)]
        [TestCase(MobileBuildPreflightState.SerializedScreenAutoRotationValue, false, false, true, false)]
        // 고정 가로여도 세로 허용 플래그가 켜져 있으면 거부(설정 실수 신호).
        [TestCase((int)UIOrientation.LandscapeRight, true, false, true, true)]
        [TestCase((int)UIOrientation.LandscapeLeft, false, true, true, true)]
        public void LandscapeOnly_RejectsPortraitReachableConfiguration(
            int defaultOrientation,
            bool allowPortrait,
            bool allowPortraitUpsideDown,
            bool allowLandscapeLeft,
            bool allowLandscapeRight)
        {
            Assert.That(
                MobileBuildPreflightState.IsLandscapeOnly(
                    (UIOrientation)defaultOrientation,
                    allowPortrait,
                    allowPortraitUpsideDown,
                    allowLandscapeLeft,
                    allowLandscapeRight),
                Is.False);
        }

        // 가로 고정(19ff8e8f — 자동회전 폐기)은 자동회전보다 엄격하다: 세로가
        // 구조적으로 불가능하므로 가로 방향 플래그와 무관하게 통과한다.
        [TestCase((int)UIOrientation.LandscapeRight, false, false, true, true)]
        [TestCase((int)UIOrientation.LandscapeLeft, false, false, true, true)]
        [TestCase((int)UIOrientation.LandscapeRight, false, false, false, false)]
        public void LandscapeOnly_AcceptsFixedLandscape(
            int defaultOrientation,
            bool allowPortrait,
            bool allowPortraitUpsideDown,
            bool allowLandscapeLeft,
            bool allowLandscapeRight)
        {
            Assert.That(
                MobileBuildPreflightState.IsLandscapeOnly(
                    (UIOrientation)defaultOrientation,
                    allowPortrait,
                    allowPortraitUpsideDown,
                    allowLandscapeLeft,
                    allowLandscapeRight),
                Is.True);
        }

        // 실제 프로젝트 설정이 가로 전용인지 고정한다. 어느 방식(자동회전/고정)인지는
        // 제품 결정이라 여기서 못박지 않는다 — 못박으면 설정 변경마다 이 테스트가
        // 빌드를 막는다(2026-07-27 `19ff8e8f` 이 5→2 로 바꿨을 때 실제로 그랬다).
        // 지키는 것은 preflight 가 통과한다는 사실 하나다.
        [Test]
        public void CapturedProjectOrientation_IsLandscapeOnly()
        {
            var state = MobileBuildPreflightState.Capture(MobileBuildPlatform.Android);

            Assert.That(
                MobileBuildPreflightState.IsLandscapeOnly(
                    state.DefaultOrientation,
                    state.AllowPortrait,
                    state.AllowPortraitUpsideDown,
                    state.AllowLandscapeLeft,
                    state.AllowLandscapeRight),
                Is.True,
                $"프로젝트 orientation 이 가로 전용이 아니다 (default={(int)state.DefaultOrientation}, "
                + $"portrait={state.AllowPortrait}/{state.AllowPortraitUpsideDown}, "
                + $"landscape={state.AllowLandscapeLeft}/{state.AllowLandscapeRight})");
            Assert.That(state.AllowPortrait, Is.False);
            Assert.That(state.AllowPortraitUpsideDown, Is.False);
        }

        [TestCase("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset")]
        [TestCase("Assets/_Project/Fonts/Anton SDF.asset")]
        [TestCase("Assets/_Project/Fonts/Bangers SDF.asset")]
        [TestCase("Assets/_Project/Fonts/Jua SDF.asset")]
        [TestCase("Assets/_Project/Fonts/Kanit SDF.asset")]
        public void PrebakedDynamicFont_PreservesGlyphsAcrossBuildExit(string assetPath)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            Assert.That(asset, Is.Not.Null, assetPath);

            var serialized = new SerializedObject(asset);
            Assert.That(
                serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue,
                Is.False,
                assetPath);
            Assert.That(
                serialized.FindProperty("m_GlyphTable").arraySize,
                Is.GreaterThan(0),
                assetPath);
            Assert.That(
                serialized.FindProperty("m_CharacterTable").arraySize,
                Is.GreaterThan(0),
                assetPath);
            Assert.That(
                serialized.FindProperty("m_SourceFontFile").objectReferenceValue,
                Is.Not.Null,
                assetPath);
        }

        [TestCase("android", BuildTarget.Android, true)]
        [TestCase("ios", BuildTarget.iOS, false)]
        public void BuildPlayerOptions_UseExactScenesTargetAndPlatformDebugOptions(
            string platformName,
            BuildTarget expectedTarget,
            bool expectsAllowDebugging)
        {
            var platform = ParsePlatform(platformName);
            var request = CreateRequest(platform, platform == MobileBuildPlatform.Android
                ? CreateAndroidEnvironment()
                : CreateCommonEnvironment(platform));

            var options = DreamSquadMobileBuildCli.CreateBuildPlayerOptions(request);

            CollectionAssert.AreEqual(DreamSquadMobileBuildCli.ExpectedScenes, options.scenes);
            Assert.That(options.target, Is.EqualTo(expectedTarget));
            Assert.That(options.locationPathName, Is.EqualTo(request.OutputPath));
            Assert.That((options.options & BuildOptions.Development) != 0, Is.True);
            Assert.That(
                (options.options & BuildOptions.AllowDebugging) != 0,
                Is.EqualTo(expectsAllowDebugging));
        }

        [Test]
        public void AndroidPlayerSettingsSnapshot_AppliesAndRestoresAllOverrides()
        {
            var initial = CreateInitialSettingsState();
            var current = initial.Clone();
            var request = CreateRequest(
                MobileBuildPlatform.Android,
                CreateAndroidEnvironment());
            var snapshot = new MobilePlayerSettingsSnapshot(
                MobileBuildPlatform.Android,
                initial,
                (_, state) => current = state.Clone());

            snapshot.Apply(request);

            Assert.That(current.BundleVersion, Is.EqualTo("0.1.0"));
            Assert.That(current.AndroidBundleVersionCode, Is.EqualTo(123));
            Assert.That(current.AndroidUseCustomKeystore, Is.True);
            Assert.That(
                current.AndroidKeyAlias,
                Is.EqualTo(DreamSquadMobileBuildCli.ExpectedAndroidKeyAlias));
            Assert.That(current.AndroidBuildAppBundle, Is.False);
            Assert.That(current.AndroidExportAsGoogleProject, Is.False);

            snapshot.Restore();

            AssertStatesEqual(initial, current);
        }

        [Test]
        public void IosPlayerSettingsSnapshot_AppliesAndRestoresSharedVersion()
        {
            var initial = CreateInitialSettingsState();
            var current = initial.Clone();
            var request = CreateRequest(
                MobileBuildPlatform.Ios,
                CreateCommonEnvironment(MobileBuildPlatform.Ios));
            var snapshot = new MobilePlayerSettingsSnapshot(
                MobileBuildPlatform.Ios,
                initial,
                (_, state) => current = state.Clone());

            snapshot.Apply(request);

            Assert.That(current.BundleVersion, Is.EqualTo("0.1.0"));
            Assert.That(current.IosBuildNumber, Is.EqualTo("123"));

            snapshot.Restore();

            AssertStatesEqual(initial, current);
        }

        private MobileBuildRequest CreateRequest(
            MobileBuildPlatform platform,
            IReadOnlyDictionary<string, string> environment)
        {
            return MobileBuildRequest.FromEnvironment(
                platform,
                name => environment.TryGetValue(name, out var value) ? value : null,
                _ => true,
                projectRoot);
        }

        private static MobileBuildPlatform ParsePlatform(string platformName)
        {
            return string.Equals(platformName, "android", StringComparison.Ordinal)
                ? MobileBuildPlatform.Android
                : MobileBuildPlatform.Ios;
        }

        private Dictionary<string, string> CreateCommonEnvironment(MobileBuildPlatform platform)
        {
            var stem = "DreamSquad-Demo-0.1.0-123-01234567";
            return new Dictionary<string, string>
            {
                [MobileBuildRequest.VersionEnvironmentName] = "0.1.0",
                [MobileBuildRequest.BuildNumberEnvironmentName] = "123",
                [MobileBuildRequest.OutputEnvironmentName] =
                    platform == MobileBuildPlatform.Android
                        ? Path.Combine(
                            projectRoot,
                            "Builds",
                            "Mobile",
                            stem,
                            "Android",
                            stem + ".apk")
                        : Path.Combine(
                            projectRoot,
                            "Builds",
                            "Mobile",
                            stem,
                            "iOS",
                            "Xcode")
            };
        }

        private Dictionary<string, string> CreateAndroidEnvironment()
        {
            var environment = CreateCommonEnvironment(MobileBuildPlatform.Android);
            environment[MobileBuildRequest.AndroidKeystoreEnvironmentName] =
                Path.Combine(projectRoot, "secure", "somnia-dev.keystore");
            environment[MobileBuildRequest.AndroidKeystorePasswordEnvironmentName] = StorePassword;
            environment[MobileBuildRequest.AndroidKeyPasswordEnvironmentName] = KeyPassword;
            return environment;
        }

        private static MobileBuildPreflightState CreateValidPreflight(
            MobileBuildPlatform platform)
        {
            return new MobileBuildPreflightState
            {
                UnityVersion = DreamSquadMobileBuildCli.ExpectedUnityVersion,
                CompanyName = DreamSquadMobileBuildCli.ExpectedCompanyName,
                ProductName = DreamSquadMobileBuildCli.ExpectedProductName,
                ActiveBuildTarget = platform == MobileBuildPlatform.Android
                    ? BuildTarget.Android
                    : BuildTarget.iOS,
                TargetSupported = true,
                ApplicationIdentifier =
                    DreamSquadMobileBuildCli.ExpectedApplicationIdentifier,
                EnabledScenes = (string[])DreamSquadMobileBuildCli.ExpectedScenes.Clone(),
                DefaultOrientation = UIOrientation.LandscapeRight,
                // 08cc7966 머지가 떨어뜨린 초기화(프로덕션 쪽은 72dca8ae 가 복원). 기본값 false 로
                // 두면 고정 가로 분기로는 통과하지만, orientation 을 autorotation 으로 바꿔보는
                // Preflight_AcceptsTrackedSerializedScreenAutoRotation 이 landscape 플래그를 요구해 실패한다.
                AllowPortrait = false,
                AllowPortraitUpsideDown = false,
                AllowLandscapeLeft = true,
                AllowLandscapeRight = true,
                AndroidMinSdkVersion = AndroidSdkVersions.AndroidApiLevel26,
                AndroidScriptingBackend = ScriptingImplementation.IL2CPP,
                AndroidArchitectures = AndroidArchitecture.ARM64,
                IosDeploymentTarget = "15.0",
                IosTargetDevice = iOSTargetDevice.iPhoneAndiPad
            };
        }

        private static void ApplyDrift(MobileBuildPreflightState state, string drift)
        {
            switch (drift)
            {
                case "unity":
                    state.UnityVersion = "6000.4.2f1";
                    break;
                case "company":
                    state.CompanyName = "Wrong";
                    break;
                case "product":
                    state.ProductName = "Wrong";
                    break;
                case "target":
                    state.ActiveBuildTarget = BuildTarget.iOS;
                    break;
                case "support":
                    state.TargetSupported = false;
                    break;
                case "app-id":
                    state.ApplicationIdentifier = "com.example.wrong";
                    break;
                case "scenes":
                    state.EnabledScenes = state.EnabledScenes.Reverse().ToArray();
                    break;
                case "orientation":
                    state.DefaultOrientation = UIOrientation.Portrait;
                    break;
                case "min-sdk":
                    state.AndroidMinSdkVersion = (AndroidSdkVersions)25;
                    break;
                case "backend":
                    state.AndroidScriptingBackend = ScriptingImplementation.Mono2x;
                    break;
                case "architecture":
                    state.AndroidArchitectures = AndroidArchitecture.ARMv7;
                    break;
                default:
                    Assert.Fail($"Unknown drift: {drift}");
                    break;
            }
        }

        private static MobilePlayerSettingsState CreateInitialSettingsState()
        {
            return new MobilePlayerSettingsState
            {
                BundleVersion = "9.9.9",
                AndroidBundleVersionCode = 9,
                AndroidUseCustomKeystore = false,
                AndroidKeystoreName = "old.keystore",
                AndroidKeystorePassword = "old store",
                AndroidKeyAlias = "old alias",
                AndroidKeyPassword = "old key",
                AndroidBuildAppBundle = true,
                AndroidExportAsGoogleProject = true,
                IosBuildNumber = "9"
            };
        }

        private static void AssertStatesEqual(
            MobilePlayerSettingsState expected,
            MobilePlayerSettingsState actual)
        {
            Assert.That(actual.BundleVersion, Is.EqualTo(expected.BundleVersion));
            Assert.That(
                actual.AndroidBundleVersionCode,
                Is.EqualTo(expected.AndroidBundleVersionCode));
            Assert.That(
                actual.AndroidUseCustomKeystore,
                Is.EqualTo(expected.AndroidUseCustomKeystore));
            Assert.That(actual.AndroidKeystoreName, Is.EqualTo(expected.AndroidKeystoreName));
            Assert.That(
                actual.AndroidKeystorePassword,
                Is.EqualTo(expected.AndroidKeystorePassword));
            Assert.That(actual.AndroidKeyAlias, Is.EqualTo(expected.AndroidKeyAlias));
            Assert.That(actual.AndroidKeyPassword, Is.EqualTo(expected.AndroidKeyPassword));
            Assert.That(
                actual.AndroidBuildAppBundle,
                Is.EqualTo(expected.AndroidBuildAppBundle));
            Assert.That(
                actual.AndroidExportAsGoogleProject,
                Is.EqualTo(expected.AndroidExportAsGoogleProject));
            Assert.That(actual.IosBuildNumber, Is.EqualTo(expected.IosBuildNumber));
        }
    }
}
