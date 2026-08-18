using System.Xml;
using NUnit.Framework;
using UnityEditor;
using Wassup.Editor.MobileBuild;

namespace Wassup.Tests.EditMode.MobileBuild
{
    // 증상 회귀 방지: "앱을 지우고 다시 깔았는데 기존 스쿼드/덱이 그대로 나온다."
    // 원인은 저장 파일이 앱과 같이 지워지지 않는 것이었다. 두 경로를 다 막는다 —
    // (1) 구글 자동 백업이 되돌리는 경로, (2) 공유 저장소 폴더가 남는 경로.
    public sealed class AndroidBackupOptOutTests
    {
        private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";

        // Unity 가 생성하는 매니페스트의 축약형 — allowBackup 을 아예 적지 않아
        // 안드로이드 기본값(true)이 먹던 그 모양이다.
        private const string GeneratedManifest =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\"\n" +
            "    package=\"com.playlinks.somnia.dev\" android:installLocation=\"preferExternal\">\n" +
            "  <application android:label=\"@string/app_name\" android:icon=\"@mipmap/app_icon\"\n" +
            "      android:debuggable=\"true\">\n" +
            "    <activity android:name=\"com.unity3d.player.UnityPlayerGameActivity\" />\n" +
            "  </application>\n" +
            "</manifest>";

        private static XmlElement ApplicationOf(string manifestXml)
        {
            var document = new XmlDocument();
            document.LoadXml(manifestXml);
            return (XmlElement)document.SelectSingleNode("/manifest/application");
        }

        [Test]
        public void DisableBackup_StampsAllowBackupFalse_WhenAttributeIsAbsent()
        {
            Assert.IsFalse(ApplicationOf(GeneratedManifest).HasAttribute("allowBackup", AndroidNamespace),
                "픽스처 전제: Unity 생성 매니페스트에는 allowBackup 이 없다");

            var patched = AndroidBackupOptOut.DisableBackup(GeneratedManifest);

            Assert.AreEqual("false",
                ApplicationOf(patched).GetAttribute("allowBackup", AndroidNamespace));
        }

        [Test]
        public void DisableBackup_OverridesAllowBackupTrue()
        {
            var manifest = GeneratedManifest.Replace(
                "<application ", "<application android:allowBackup=\"true\" ");

            var patched = AndroidBackupOptOut.DisableBackup(manifest);

            Assert.AreEqual("false",
                ApplicationOf(patched).GetAttribute("allowBackup", AndroidNamespace));
        }

        [Test]
        public void DisableBackup_LeavesTheRestOfTheManifestAlone()
        {
            var patched = AndroidBackupOptOut.DisableBackup(GeneratedManifest);
            var application = ApplicationOf(patched);

            Assert.AreEqual("@string/app_name", application.GetAttribute("label", AndroidNamespace));
            Assert.AreEqual("true", application.GetAttribute("debuggable", AndroidNamespace));
            Assert.AreEqual(1, application.GetElementsByTagName("activity").Count);

            var document = new XmlDocument();
            document.LoadXml(patched);
            Assert.AreEqual("preferExternal",
                ((XmlElement)document.DocumentElement).GetAttribute("installLocation", AndroidNamespace),
                "installLocation 은 APK 설치 위치라 저장 데이터와 무관하다 — 건드리지 않는다");
        }

        // 백업 차단의 짝. 공유 저장소(Android/data/<패키지>)에 세이브를 두면 기기·롬에 따라
        // 앱을 지워도 폴더가 남아 재설치가 초기화가 되지 않는다. 세이브는 앱 전용 저장소에 둔다.
        [Test]
        public void SaveData_LivesInAppPrivateStorage()
        {
            Assert.AreEqual(AndroidPreferredDataLocation.ForceInternal,
                PlayerSettings.Android.preferredDataLocation,
                "앱을 지우면 profile.json 도 같이 지워져야 한다 — 그래야 재설치가 신규 유저 경로를 탄다");
        }
    }
}
