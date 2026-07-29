using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace Wassup.Editor.MobileBuild
{
    /// <summary>
    /// Batch-mode entry points used by scripts/mobile/build.sh.
    /// Secrets are accepted only through the child Unity process environment.
    /// </summary>
    public static class DreamSquadMobileBuildCli
    {
        internal const string ExpectedUnityVersion = "6000.4.3f1";
        internal const string ExpectedCompanyName = "Playlinks";
        internal const string ExpectedProductName = "DreamSquad Demo";
        internal const string ExpectedApplicationIdentifier = "com.playlinks.somnia.dev";
        internal const string ExpectedAndroidKeyAlias = "somnia-dev";
        internal const string ExpectedIosTeamId = "69DK98XF77";
        internal const string ExpectedIosProfileName = "somnia_dev_adhoc";
        internal const string ExpectedIosSigningIdentity = "Apple Distribution";

        internal static readonly string[] ExpectedScenes =
        {
            "Assets/_Project/Scenes/OutgameScene.unity",
            "Assets/_Project/Scenes/BattleScene.unity"
        };

        public static void BuildAndroidQa()
        {
            Execute(MobileBuildPlatform.Android);
        }

        public static void ExportIosQa()
        {
            Execute(MobileBuildPlatform.Ios);
        }

        private static void Execute(MobileBuildPlatform platform)
        {
            try
            {
                ExecuteCore(platform);
            }
            catch (MobileBuildException exception)
            {
                Debug.LogError($"DreamSquad mobile build failed: {exception.Message}");
                EditorApplication.Exit(1);
            }
            catch (Exception exception)
            {
                // Do not print exception messages or stacks here. Unity/Xcode exceptions can
                // contain the machine-local signing path.
                Debug.LogError(
                    $"DreamSquad mobile build failed with an unexpected {exception.GetType().Name}. " +
                    "Review the surrounding sanitized build diagnostics.");
                EditorApplication.Exit(1);
            }
        }

        private static void ExecuteCore(MobileBuildPlatform platform)
        {
            var projectRoot = Path.GetFullPath(Path.GetDirectoryName(Application.dataPath) ?? ".");
            var request = MobileBuildRequest.FromEnvironment(
                platform,
                Environment.GetEnvironmentVariable,
                File.Exists,
                projectRoot);

            var preflight = MobileBuildPreflightState.Capture(platform);
            preflight.Validate(platform);
            request.EnsureOutputDoesNotExist(File.Exists, Directory.Exists);

            var parentDirectory = Path.GetDirectoryName(request.OutputPath);
            if (string.IsNullOrEmpty(parentDirectory))
            {
                throw new MobileBuildException("DREAMSQUAD_BUILD_OUTPUT has no parent directory.");
            }

            Directory.CreateDirectory(parentDirectory);

            var snapshot = MobilePlayerSettingsSnapshot.Capture(platform);
            try
            {
                snapshot.Apply(request);
                var options = CreateBuildPlayerOptions(request);
                var report = BuildPipeline.BuildPlayer(options);
                ValidateBuildReport(platform, report);

                if (platform == MobileBuildPlatform.Ios)
                {
                    PatchIosMainTargetReleaseSigning(request.OutputPath);
                }

                ValidateBuildOutput(request);
                WriteSafeSuccessSummary(request, report);
            }
            finally
            {
                snapshot.Restore();
                AssetDatabase.SaveAssets();
            }
        }

        internal static BuildPlayerOptions CreateBuildPlayerOptions(MobileBuildRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var target = request.Platform == MobileBuildPlatform.Android
                ? BuildTarget.Android
                : BuildTarget.iOS;
            var targetGroup = request.Platform == MobileBuildPlatform.Android
                ? BuildTargetGroup.Android
                : BuildTargetGroup.iOS;
            var options = BuildOptions.Development;
            if (request.Platform == MobileBuildPlatform.Android)
            {
                options |= BuildOptions.AllowDebugging;
            }

            return new BuildPlayerOptions
            {
                scenes = (string[])ExpectedScenes.Clone(),
                locationPathName = request.OutputPath,
                target = target,
                targetGroup = targetGroup,
                options = options
            };
        }

        private static void ValidateBuildReport(MobileBuildPlatform platform, BuildReport report)
        {
            if (report != null && report.summary.result == BuildResult.Succeeded)
            {
                return;
            }

            var result = report == null ? "<no report>" : report.summary.result.ToString();
            var errors = report == null ? -1 : report.summary.totalErrors;
            throw new MobileBuildException(
                $"Unity {platform} build did not succeed (result={result}, errors={errors}).");
        }

        private static void ValidateBuildOutput(MobileBuildRequest request)
        {
            if (request.Platform == MobileBuildPlatform.Android)
            {
                if (!File.Exists(request.OutputPath))
                {
                    throw new MobileBuildException(
                        "Unity reported success but the expected Android APK is missing.");
                }

                return;
            }

            var projectPath = Path.Combine(request.OutputPath, "Unity-iPhone.xcodeproj", "project.pbxproj");
            if (!Directory.Exists(request.OutputPath) || !File.Exists(projectPath))
            {
                throw new MobileBuildException(
                    "Unity reported success but the expected iOS Xcode project is missing.");
            }
        }

        private static void WriteSafeSuccessSummary(MobileBuildRequest request, BuildReport report)
        {
            Debug.Log(
                $"DreamSquad mobile build succeeded: platform={request.Platform}, " +
                $"appId={ExpectedApplicationIdentifier}, version={request.Version}, " +
                $"build={request.BuildNumber}, outputName={request.SafeOutputName}, " +
                $"result={report.summary.result}, warnings={report.summary.totalWarnings}.");
        }

        private static void PatchIosMainTargetReleaseSigning(string xcodeExportPath)
        {
#if UNITY_IOS
            var projectPath = PBXProject.GetPBXProjectPath(xcodeExportPath);
            if (!File.Exists(projectPath))
            {
                throw new MobileBuildException(
                    "The generated Unity-iPhone Xcode project is missing before signing configuration.");
            }

            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            var mainTargetGuid = project.GetUnityMainTargetGuid();
            if (string.IsNullOrEmpty(mainTargetGuid))
            {
                throw new MobileBuildException("The generated Xcode project has no Unity-iPhone main target.");
            }

            var releaseConfigGuid = project.BuildConfigByName(mainTargetGuid, "Release");
            if (string.IsNullOrEmpty(releaseConfigGuid))
            {
                throw new MobileBuildException(
                    "The generated Unity-iPhone main target has no Release configuration.");
            }

            project.SetBuildPropertyForConfig(releaseConfigGuid, "CODE_SIGN_STYLE", "Manual");
            project.SetBuildPropertyForConfig(releaseConfigGuid, "DEVELOPMENT_TEAM", ExpectedIosTeamId);
            // Unity can carry the UUID serialized in PlayerSettings into the generated project.
            // Xcode must select the current profile by its stable name below; a stale legacy UUID
            // otherwise takes precedence and makes archive fail after a profile renewal.
            project.SetBuildPropertyForConfig(releaseConfigGuid, "PROVISIONING_PROFILE", "");
            project.SetBuildPropertyForConfig(
                releaseConfigGuid,
                "CODE_SIGN_IDENTITY",
                ExpectedIosSigningIdentity);
            project.SetBuildPropertyForConfig(
                releaseConfigGuid,
                "CODE_SIGN_IDENTITY[sdk=iphoneos*]",
                ExpectedIosSigningIdentity);
            project.SetBuildPropertyForConfig(
                releaseConfigGuid,
                "PROVISIONING_PROFILE_SPECIFIER",
                ExpectedIosProfileName);
            project.WriteToFile(projectPath);
#else
            throw new MobileBuildException(
                "iOS signing configuration requires Unity to start with -buildTarget iOS.");
#endif
        }
    }

    internal enum MobileBuildPlatform
    {
        Android,
        Ios
    }

    internal sealed class MobileBuildException : Exception
    {
        internal MobileBuildException(string message)
            : base(message)
        {
        }
    }

    internal sealed class MobileBuildRequest
    {
        internal const string VersionEnvironmentName = "DREAMSQUAD_BUILD_VERSION";
        internal const string BuildNumberEnvironmentName = "DREAMSQUAD_BUILD_NUMBER";
        internal const string OutputEnvironmentName = "DREAMSQUAD_BUILD_OUTPUT";
        internal const string AndroidKeystoreEnvironmentName = "DREAMSQUAD_ANDROID_KEYSTORE";
        internal const string AndroidKeystorePasswordEnvironmentName =
            "DREAMSQUAD_ANDROID_KEYSTORE_PASSWORD";
        internal const string AndroidKeyPasswordEnvironmentName =
            "DREAMSQUAD_ANDROID_KEY_PASSWORD";

        private static readonly Regex VersionPattern =
            new Regex(@"^[0-9]+\.[0-9]+(?:\.[0-9]+)?$", RegexOptions.CultureInvariant);

        private MobileBuildRequest()
        {
        }

        internal MobileBuildPlatform Platform { get; private set; }
        internal string Version { get; private set; }
        internal int BuildNumber { get; private set; }
        internal string OutputPath { get; private set; }
        internal string AndroidKeystorePath { get; private set; }
        internal string AndroidKeystorePassword { get; private set; }
        internal string AndroidKeyPassword { get; private set; }
        internal string SafeOutputName =>
            Platform == MobileBuildPlatform.Android
                ? Path.GetFileName(OutputPath)
                : new DirectoryInfo(OutputPath).Name;

        internal static MobileBuildRequest FromEnvironment(
            MobileBuildPlatform platform,
            Func<string, string> getEnvironmentVariable,
            Func<string, bool> fileExists,
            string projectRoot)
        {
            if (getEnvironmentVariable == null)
            {
                throw new ArgumentNullException(nameof(getEnvironmentVariable));
            }

            if (fileExists == null)
            {
                throw new ArgumentNullException(nameof(fileExists));
            }

            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException("Project root is required.", nameof(projectRoot));
            }

            var version = ReadRequiredText(getEnvironmentVariable, VersionEnvironmentName);
            if (!VersionPattern.IsMatch(version))
            {
                throw new MobileBuildException(
                    $"{VersionEnvironmentName} must contain two or three dot-separated numeric components.");
            }

            var buildNumberText = ReadRequiredText(getEnvironmentVariable, BuildNumberEnvironmentName);
            if (!int.TryParse(
                    buildNumberText,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var buildNumber) ||
                buildNumber <= 0)
            {
                throw new MobileBuildException(
                    $"{BuildNumberEnvironmentName} must be a positive 32-bit integer.");
            }

            var normalizedProjectRoot = Path.GetFullPath(projectRoot);
            var outputValue = ReadRequiredText(getEnvironmentVariable, OutputEnvironmentName);
            var outputPath = NormalizePathWithoutEcho(
                outputValue,
                normalizedProjectRoot,
                OutputEnvironmentName);
            var allowedOutputRoot = Path.GetFullPath(
                Path.Combine(normalizedProjectRoot, "Builds", "Mobile"));
            EnsureChildPath(allowedOutputRoot, outputPath, OutputEnvironmentName);

            if (platform == MobileBuildPlatform.Android &&
                !string.Equals(Path.GetExtension(outputPath), ".apk", StringComparison.OrdinalIgnoreCase))
            {
                throw new MobileBuildException(
                    $"{OutputEnvironmentName} must name an .apk file for Android.");
            }

            if (platform == MobileBuildPlatform.Ios &&
                string.Equals(Path.GetExtension(outputPath), ".ipa", StringComparison.OrdinalIgnoreCase))
            {
                throw new MobileBuildException(
                    $"{OutputEnvironmentName} must name an Xcode export directory for iOS.");
            }

            var request = new MobileBuildRequest
            {
                Platform = platform,
                Version = version,
                BuildNumber = buildNumber,
                OutputPath = outputPath
            };

            if (platform == MobileBuildPlatform.Android)
            {
                var keystoreValue =
                    ReadRequiredText(getEnvironmentVariable, AndroidKeystoreEnvironmentName);
                var keystorePath = NormalizePathWithoutEcho(
                    keystoreValue,
                    normalizedProjectRoot,
                    AndroidKeystoreEnvironmentName);
                if (!fileExists(keystorePath))
                {
                    throw new MobileBuildException(
                        $"{AndroidKeystoreEnvironmentName} does not reference a readable file.");
                }

                var keystorePassword =
                    ReadRequiredSecret(getEnvironmentVariable, AndroidKeystorePasswordEnvironmentName);
                var keyPassword = getEnvironmentVariable(AndroidKeyPasswordEnvironmentName);

                request.AndroidKeystorePath = keystorePath;
                request.AndroidKeystorePassword = keystorePassword;
                request.AndroidKeyPassword = string.IsNullOrEmpty(keyPassword)
                    ? keystorePassword
                    : keyPassword;
            }

            return request;
        }

        internal void EnsureOutputDoesNotExist(
            Func<string, bool> fileExists,
            Func<string, bool> directoryExists)
        {
            if (fileExists == null)
            {
                throw new ArgumentNullException(nameof(fileExists));
            }

            if (directoryExists == null)
            {
                throw new ArgumentNullException(nameof(directoryExists));
            }

            if (fileExists(OutputPath) || directoryExists(OutputPath))
            {
                throw new MobileBuildException(
                    "Refusing to overwrite an existing DreamSquad mobile build output.");
            }
        }

        internal string DescribeSafe()
        {
            return $"platform={Platform}, version={Version}, build={BuildNumber}, " +
                   $"outputName={SafeOutputName}, signingConfigured={Platform == MobileBuildPlatform.Android}";
        }

        private static string ReadRequiredText(Func<string, string> getter, string name)
        {
            var value = getter(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new MobileBuildException($"Required environment variable {name} is missing.");
            }

            return value.Trim();
        }

        private static string ReadRequiredSecret(Func<string, string> getter, string name)
        {
            var value = getter(name);
            if (string.IsNullOrEmpty(value))
            {
                throw new MobileBuildException($"Required environment variable {name} is missing.");
            }

            return value;
        }

        private static string NormalizePathWithoutEcho(
            string value,
            string projectRoot,
            string variableName)
        {
            try
            {
                return Path.GetFullPath(
                    Path.IsPathRooted(value)
                        ? value
                        : Path.Combine(projectRoot, value));
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is NotSupportedException ||
                exception is PathTooLongException)
            {
                throw new MobileBuildException($"{variableName} is not a valid filesystem path.");
            }
        }

        private static void EnsureChildPath(string root, string candidate, string variableName)
        {
            var rootWithSeparator =
                root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                throw new MobileBuildException(
                    $"{variableName} must stay inside the project's Builds/Mobile directory.");
            }
        }
    }

    internal sealed class MobileBuildPreflightState
    {
        internal const int SerializedScreenAutoRotationValue =
            (int)ScreenOrientation.AutoRotation;

        internal string UnityVersion { get; set; }
        internal string CompanyName { get; set; }
        internal string ProductName { get; set; }
        internal BuildTarget ActiveBuildTarget { get; set; }
        internal bool TargetSupported { get; set; }
        internal string ApplicationIdentifier { get; set; }
        internal IReadOnlyList<string> EnabledScenes { get; set; }
        internal UIOrientation DefaultOrientation { get; set; }
        internal bool AllowPortrait { get; set; }
        internal bool AllowPortraitUpsideDown { get; set; }
        internal bool AllowLandscapeLeft { get; set; }
        internal bool AllowLandscapeRight { get; set; }
        internal AndroidSdkVersions AndroidMinSdkVersion { get; set; }
        internal ScriptingImplementation AndroidScriptingBackend { get; set; }
        internal AndroidArchitecture AndroidArchitectures { get; set; }
        internal string IosDeploymentTarget { get; set; }
        internal iOSTargetDevice IosTargetDevice { get; set; }

        internal static MobileBuildPreflightState Capture(MobileBuildPlatform platform)
        {
            var target = platform == MobileBuildPlatform.Android
                ? BuildTarget.Android
                : BuildTarget.iOS;
            var targetGroup = platform == MobileBuildPlatform.Android
                ? BuildTargetGroup.Android
                : BuildTargetGroup.iOS;
            var namedTarget = platform == MobileBuildPlatform.Android
                ? NamedBuildTarget.Android
                : NamedBuildTarget.iOS;

            return new MobileBuildPreflightState
            {
                UnityVersion = Application.unityVersion,
                CompanyName = PlayerSettings.companyName,
                ProductName = PlayerSettings.productName,
                ActiveBuildTarget = EditorUserBuildSettings.activeBuildTarget,
                TargetSupported = BuildPipeline.IsBuildTargetSupported(targetGroup, target),
                ApplicationIdentifier = PlayerSettings.GetApplicationIdentifier(namedTarget),
                EnabledScenes = EditorBuildSettings.scenes
                    .Where(scene => scene.enabled)
                    .Select(scene => scene.path)
                    .ToArray(),
                DefaultOrientation = PlayerSettings.defaultInterfaceOrientation,
                AllowPortrait = PlayerSettings.allowedAutorotateToPortrait,
                AllowPortraitUpsideDown = PlayerSettings.allowedAutorotateToPortraitUpsideDown,
                AllowLandscapeLeft = PlayerSettings.allowedAutorotateToLandscapeLeft,
                AllowLandscapeRight = PlayerSettings.allowedAutorotateToLandscapeRight,
                AndroidMinSdkVersion = PlayerSettings.Android.minSdkVersion,
                AndroidScriptingBackend =
                    PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android),
                AndroidArchitectures = PlayerSettings.Android.targetArchitectures,
                IosDeploymentTarget = PlayerSettings.iOS.targetOSVersionString,
                IosTargetDevice = PlayerSettings.iOS.targetDevice
            };
        }

        internal void Validate(MobileBuildPlatform platform)
        {
            var expectedTarget = platform == MobileBuildPlatform.Android
                ? BuildTarget.Android
                : BuildTarget.iOS;

            if (!string.Equals(
                    UnityVersion,
                    DreamSquadMobileBuildCli.ExpectedUnityVersion,
                    StringComparison.Ordinal))
            {
                throw new MobileBuildException(
                    $"Unity {DreamSquadMobileBuildCli.ExpectedUnityVersion} is required.");
            }

            if (!string.Equals(
                    CompanyName,
                    DreamSquadMobileBuildCli.ExpectedCompanyName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    ProductName,
                    DreamSquadMobileBuildCli.ExpectedProductName,
                    StringComparison.Ordinal))
            {
                throw new MobileBuildException(
                    "PlayerSettings Company/Product do not match the DreamSquad distribution contract.");
            }

            if (ActiveBuildTarget != expectedTarget)
            {
                throw new MobileBuildException(
                    $"Unity must start with -buildTarget {expectedTarget}.");
            }

            if (!TargetSupported)
            {
                throw new MobileBuildException(
                    $"Unity Build Support is not installed for {expectedTarget}.");
            }

            if (!string.Equals(
                    ApplicationIdentifier,
                    DreamSquadMobileBuildCli.ExpectedApplicationIdentifier,
                    StringComparison.Ordinal))
            {
                throw new MobileBuildException(
                    $"The {expectedTarget} application identifier must be " +
                    $"{DreamSquadMobileBuildCli.ExpectedApplicationIdentifier}.");
            }

            if (EnabledScenes == null ||
                !DreamSquadMobileBuildCli.ExpectedScenes.SequenceEqual(
                    EnabledScenes,
                    StringComparer.Ordinal))
            {
                throw new MobileBuildException(
                    "Enabled scenes must be exactly OutgameScene then BattleScene.");
            }

            if (!IsLandscapeAutorotation(
                    DefaultOrientation,
                    AllowPortrait,
                    AllowPortraitUpsideDown,
                    AllowLandscapeLeft,
                    AllowLandscapeRight))
            {
                throw new MobileBuildException(
                    "DreamSquad mobile builds require landscape autorotation " +
                    "(portrait disabled and both landscape directions enabled).");
            }

            if (platform == MobileBuildPlatform.Android)
            {
                if (AndroidMinSdkVersion != AndroidSdkVersions.AndroidApiLevel26)
                {
                    throw new MobileBuildException("Android minimum API level must be 26.");
                }

                if (AndroidScriptingBackend != ScriptingImplementation.IL2CPP)
                {
                    throw new MobileBuildException("Android must use the IL2CPP scripting backend.");
                }

                if (AndroidArchitectures != AndroidArchitecture.ARM64)
                {
                    throw new MobileBuildException("Android must target exactly ARM64.");
                }
            }
            else if (!string.Equals(IosDeploymentTarget, "15.0", StringComparison.Ordinal))
            {
                throw new MobileBuildException("iOS deployment target must be 15.0.");
            }
            else if (IosTargetDevice != iOSTargetDevice.iPhoneAndiPad)
            {
                throw new MobileBuildException("iOS must target both iPhone and iPad.");
            }
        }

        // 제품 계약은 "가로 전용"뿐 아니라, 기기의 상하가 바뀔 때 두 가로 방향 사이에서
        // 회전하는 것이다. 고정 LandscapeLeft/Right는 세로를 막아도 이 계약을 만족하지 않는다.
        internal static bool IsLandscapeAutorotation(
            UIOrientation defaultOrientation,
            bool allowPortrait,
            bool allowPortraitUpsideDown,
            bool allowLandscapeLeft,
            bool allowLandscapeRight)
        {
            var isAutoRotation =
                defaultOrientation == UIOrientation.AutoRotation ||
                (int)defaultOrientation == SerializedScreenAutoRotationValue;
            return isAutoRotation &&
                   !allowPortrait &&
                   !allowPortraitUpsideDown &&
                   allowLandscapeLeft &&
                   allowLandscapeRight;
        }
    }

    internal sealed class MobilePlayerSettingsState
    {
        internal string BundleVersion { get; set; }
        internal int AndroidBundleVersionCode { get; set; }
        internal bool AndroidUseCustomKeystore { get; set; }
        internal string AndroidKeystoreName { get; set; }
        internal string AndroidKeystorePassword { get; set; }
        internal string AndroidKeyAlias { get; set; }
        internal string AndroidKeyPassword { get; set; }
        internal bool AndroidBuildAppBundle { get; set; }
        internal bool AndroidExportAsGoogleProject { get; set; }
        internal string IosBuildNumber { get; set; }

        internal MobilePlayerSettingsState Clone()
        {
            return (MobilePlayerSettingsState)MemberwiseClone();
        }
    }

    internal sealed class MobilePlayerSettingsSnapshot
    {
        private readonly MobileBuildPlatform platform;
        private readonly MobilePlayerSettingsState original;
        private readonly Action<MobileBuildPlatform, MobilePlayerSettingsState> write;
        private bool restored;

        internal MobilePlayerSettingsSnapshot(
            MobileBuildPlatform platform,
            MobilePlayerSettingsState original,
            Action<MobileBuildPlatform, MobilePlayerSettingsState> write)
        {
            this.platform = platform;
            this.original = original?.Clone() ?? throw new ArgumentNullException(nameof(original));
            this.write = write ?? throw new ArgumentNullException(nameof(write));
        }

        internal static MobilePlayerSettingsSnapshot Capture(MobileBuildPlatform platform)
        {
            var state = new MobilePlayerSettingsState
            {
                BundleVersion = PlayerSettings.bundleVersion,
                AndroidBundleVersionCode = PlayerSettings.Android.bundleVersionCode,
                AndroidUseCustomKeystore = PlayerSettings.Android.useCustomKeystore,
                AndroidKeystoreName = PlayerSettings.Android.keystoreName,
                AndroidKeystorePassword = PlayerSettings.Android.keystorePass,
                AndroidKeyAlias = PlayerSettings.Android.keyaliasName,
                AndroidKeyPassword = PlayerSettings.Android.keyaliasPass,
                AndroidBuildAppBundle = EditorUserBuildSettings.buildAppBundle,
                AndroidExportAsGoogleProject =
                    EditorUserBuildSettings.exportAsGoogleAndroidProject,
                IosBuildNumber = PlayerSettings.iOS.buildNumber
            };
            return new MobilePlayerSettingsSnapshot(platform, state, WriteToUnity);
        }

        internal void Apply(MobileBuildRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.Platform != platform)
            {
                throw new InvalidOperationException("Build request platform does not match the snapshot.");
            }

            if (restored)
            {
                throw new InvalidOperationException("A restored PlayerSettings snapshot cannot be reused.");
            }

            var applied = original.Clone();
            applied.BundleVersion = request.Version;
            if (platform == MobileBuildPlatform.Android)
            {
                applied.AndroidBundleVersionCode = request.BuildNumber;
                applied.AndroidUseCustomKeystore = true;
                applied.AndroidKeystoreName = request.AndroidKeystorePath;
                applied.AndroidKeystorePassword = request.AndroidKeystorePassword;
                applied.AndroidKeyAlias = DreamSquadMobileBuildCli.ExpectedAndroidKeyAlias;
                applied.AndroidKeyPassword = request.AndroidKeyPassword;
                applied.AndroidBuildAppBundle = false;
                applied.AndroidExportAsGoogleProject = false;
            }
            else
            {
                applied.IosBuildNumber =
                    request.BuildNumber.ToString(CultureInfo.InvariantCulture);
            }

            write(platform, applied);
        }

        internal void Restore()
        {
            if (restored)
            {
                return;
            }

            write(platform, original.Clone());
            restored = true;
        }

        private static void WriteToUnity(
            MobileBuildPlatform platform,
            MobilePlayerSettingsState state)
        {
            PlayerSettings.bundleVersion = state.BundleVersion;
            if (platform == MobileBuildPlatform.Android)
            {
                PlayerSettings.Android.bundleVersionCode = state.AndroidBundleVersionCode;
                PlayerSettings.Android.useCustomKeystore = state.AndroidUseCustomKeystore;
                PlayerSettings.Android.keystoreName = state.AndroidKeystoreName;
                PlayerSettings.Android.keystorePass = state.AndroidKeystorePassword;
                PlayerSettings.Android.keyaliasName = state.AndroidKeyAlias;
                PlayerSettings.Android.keyaliasPass = state.AndroidKeyPassword;
                EditorUserBuildSettings.buildAppBundle = state.AndroidBuildAppBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject =
                    state.AndroidExportAsGoogleProject;
            }
            else
            {
                PlayerSettings.iOS.buildNumber = state.IosBuildNumber;
            }
        }
    }
}
