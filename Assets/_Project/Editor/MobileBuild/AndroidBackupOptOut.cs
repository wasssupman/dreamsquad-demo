using System.IO;
using System.Text;
using System.Xml;
#if UNITY_ANDROID
using UnityEditor.Android;
using UnityEngine;
#endif

namespace Wassup.Editor.MobileBuild
{
    // "앱을 지우면 저장 데이터도 같이 지워진다" 를 성립시키는 반쪽. 나머지 반쪽은
    // PlayerSettings.Android.preferredDataLocation = ForceInternal 이다(공유 저장소
    // Android/data/&lt;패키지&gt; 는 삭제 후에도 남는 기기가 있다). 둘 중 하나만 막으면
    // 다른 경로로 같은 증상이 재발한다.
    //
    // 왜 이게 게임 문제인가: 신규 유저가 받는 기본 편성(스쿼드·덱)은 profile.json 이
    // **처음 만들어질 때 한 번만** 들어간다 — ProfileStore 의 시딩은 이미 채워진 편성을
    // 절대 덮어쓰지 않는다(플레이어가 고른 편성을 지우지 않기 위한 의도된 규칙). 그래서
    // 재설치가 초기화가 아니면 새로 저작한 기본 편성은 영영 화면에 나오지 않는다.
    // 2026-08-18 에 실제로 그랬다 — 삭제 후 재설치인데 옛 스쿼드와 숨김 카드가 잘려나가
    // 4장만 남은 덱이 그대로 나왔고, 로비 온보딩도 다시 뜨지 않았다(진행 플래그가 같은
    // 파일에 있다 = 파일이 살아남았다는 증거).
    //
    // Unity 가 생성하는 매니페스트에는 android:allowBackup 이 없어 안드로이드 기본값인
    // true 가 먹는다 → 구글 자동 백업이 앱 데이터를 떠 두었다가 재설치 때 되돌린다.
    // PlayerSettings 에 이 스위치가 없어서 생성된 gradle 매니페스트를 후처리로 못 박는다.
    // (커스텀 메인 매니페스트로 갈아끼우지 않는 이유: Unity 템플릿을 통째로 동결시켜
    // 엔진 업그레이드 때 액티비티·노치·스플래시 설정이 조용히 낡는다.)
    public static class AndroidBackupOptOut
    {
        internal const string AndroidNamespace = "http://schemas.android.com/apk/res/android";

        // 순수 변환(문자열 in → 문자열 out). 에디터 API 를 타지 않아 EditMode 로 검증한다.
        // API 31+ 의 dataExtractionRules 는 따로 두지 않는다 — allowBackup=false 면
        // 클라우드 백업과 기기 간 이전이 둘 다 꺼진다.
        internal static string DisableBackup(string manifestXml)
        {
            var document = new XmlDocument();
            document.LoadXml(manifestXml);

            if (!(document.SelectSingleNode("/manifest/application") is XmlElement application))
            {
                throw new InvalidDataException(
                    "AndroidManifest.xml 에 <application> 요소가 없다 — 백업 차단을 박을 곳이 없다.");
            }

            application.SetAttribute("allowBackup", AndroidNamespace, "false");

            using (var writer = new Utf8StringWriter())
            {
                document.Save(writer);
                return writer.ToString();
            }
        }

        // XmlDocument.Save(TextWriter) 는 선언부의 encoding 을 **writer 의 Encoding** 에서
        // 가져온다. 기본 StringWriter 는 UTF-16 이라 결과물이 `encoding="utf-16"` 으로
        // 시작하는데 파일은 UTF-8 로 저장된다 — 선언과 실제가 어긋나 안드로이드 매니페스트
        // 병합기가 `Error parsing …/AndroidManifest.xml` 로 빌드를 죽인다(2026-08-18 실측).
        // 선언 인코딩과 저장 인코딩을 한 타입에 묶어 둘이 갈릴 수 없게 한다.
        private sealed class Utf8StringWriter : StringWriter
        {
            public override Encoding Encoding => new UTF8Encoding(false);
        }

#if UNITY_ANDROID
        // gradle 프로젝트가 만들어진 직후, 빌드가 돌기 전에 끼어든다. APK 직접 빌드와
        // 프로젝트 export 양쪽 모두 이 지점을 지난다.
        public sealed class PostProcessor : IPostGenerateGradleAndroidProject
        {
            public int callbackOrder => 0;

            public void OnPostGenerateGradleAndroidProject(string path)
            {
                var manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
                if (!File.Exists(manifestPath))
                {
                    // 조용히 넘기지 않는다 — 백업이 켜진 APK 가 나가면 다음 QA 가
                    // "재설치했는데 옛 데이터" 를 또 겪는다.
                    throw new FileNotFoundException(
                        "생성된 AndroidManifest.xml 을 찾지 못해 백업 차단을 넣을 수 없다.",
                        manifestPath);
                }

                File.WriteAllText(
                    manifestPath,
                    DisableBackup(File.ReadAllText(manifestPath)),
                    new UTF8Encoding(false));

                Debug.Log($"[AndroidBackupOptOut] android:allowBackup=false 적용 — {manifestPath}");
            }
        }
#endif
    }
}
