using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Wassup.Tests.EditMode
{
    /// <summary>
    /// battle-sim-extraction — sim 의 **엔진 무참조**를 지키는 게이트.
    ///
    /// unit 17 이후 이 파일의 역할이 바뀌었다. `Scripts/Sim/Lib/` 는 `Wassup.Sim.asmdef`
    /// (`noEngineReferences: true`)가 감싸므로 **컴파일러가 정본**이다 — 텍스트 검사로 중복
    /// 감시하지 않는다(실측: sim 에 `using UnityEngine;` 을 넣으면 CS0246 으로 빌드가 깨진다).
    ///
    /// 텍스트 게이트가 남아야 하는 곳은 **스테이징 층**(`Sim/` 중 `Sim/Lib/` 밖)이다. 그쪽은 아직
    /// `Wassup.Runtime` 소속이라 컴파일러가 아무것도 막지 않는다. 실제로 그래서 한 번 샜다:
    /// unit 14 시점엔 폴더 전체가 무참조였는데 unit 15-B 의 `MatchPlacementRules` 가
    /// `using UnityEngine` 을 들여왔고, 그것을 부정하는 문서·주석·커밋 메시지 3곳이 살아남았다.
    ///
    /// 세 번째 테스트는 **게이트 자신의 설정**을 지킨다. `noEngineReferences` 를 false 로 되돌리면
    /// 컴파일러 게이트가 조용히 죽는데, 그걸 알아챌 다른 수단이 없다.
    /// </summary>
    public class SimEngineIndependenceTests
    {
        // 스테이징 층의 알려진 잔재. **여기에 파일을 추가하는 것은 졸업 부채를 늘리는 것**이므로,
        // 추가하려면 `17_sim_lib_skeleton.md` 의 이식 대상 목록도 같이 갱신해야 한다.
        private static readonly HashSet<string> AllowedEngineUsers = new HashSet<string>
        {
            // `HashSet<Vector2Int> occupied` + `new Vector2Int(...)` 때문. `int2` 로 가려면
            // `BattleBridge._occupiedTiles` 까지 함께 바꿔야 해서 unit 18 이 처분한다.
            "MatchPlacementRules.cs",
        };

        private static string SimRoot =>
            Path.Combine(Application.dataPath, "_Project", "Scripts", "Sim");

        /// 졸업 층 = 컴파일러 관할. 텍스트 게이트는 여기를 보지 않는다.
        private static string GraduatedRoot => Path.Combine(SimRoot, "Lib");

        private static string SimAsmdefPath => Path.Combine(GraduatedRoot, "Wassup.Sim.asmdef");

        private static IEnumerable<string> StagingFiles()
        {
            string graduated = GraduatedRoot + Path.DirectorySeparatorChar;
            foreach (string path in Directory.GetFiles(SimRoot, "*.cs", SearchOption.AllDirectories))
                if (!path.StartsWith(graduated, System.StringComparison.OrdinalIgnoreCase))
                    yield return path;
        }

        /// 줄에서 `//` 이후를 잘라낸다. 문자열 리터럴 안의 `//` 까지 고려하지는 않는다 —
        /// 이 게이트의 대상(엔진 참조 탐지)에서는 과탐이 미탐보다 낫다.
        private static string StripComment(string line)
        {
            int idx = line.IndexOf("//", System.StringComparison.Ordinal);
            return idx >= 0 ? line.Substring(0, idx) : line;
        }

        [Test]
        public void StagingSimModules_DoNotReferenceUnityEngine_BeyondTheKnownResidue()
        {
            Assert.IsTrue(Directory.Exists(SimRoot), $"Sim 폴더가 없다: {SimRoot}");

            var offenders = new List<string>();
            foreach (string path in StagingFiles())
            {
                string name = Path.GetFileName(path);
                if (AllowedEngineUsers.Contains(name)) continue;
                // 두 형태를 모두 본다:
                //   ① `using UnityEngine;` 지시문
                //   ② **정규화 참조** `UnityEngine.Random.Range(...)` — using 없이도 엔진에 닿는다.
                //      실존 사례: `Core/MatchSeed.cs:25`(`UnityEngine.Random`)와
                //      `Core/Session/MatchSession.cs`(`UnityEngine.Debug`). 둘 다 이주 후보인데
                //      `using` 만 보는 게이트는 **통과시킨다** — unit 17 정찰이 잡은 구멍이다.
                //
                // 주석은 제외한다. 파일 전체 문자열 검색으로 시작했다가 "이 파일은 using UnityEngine
                // 을 갖는다" 는 **설명 주석까지 잡는 오탐**이 실제로 났다.
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = StripComment(raw);
                    if (line.Length == 0) continue;
                    if (line.TrimStart().StartsWith("using UnityEngine")
                        || line.TrimStart().StartsWith("using Unity.Entities")
                        || line.TrimStart().StartsWith("using Unity.Collections")
                        || line.Contains("UnityEngine.")
                        || line.Contains("Unity.Entities.")
                        || line.Contains("Unity.Collections."))
                    {
                        offenders.Add(name);
                        break;
                    }
                }
            }

            CollectionAssert.IsEmpty(offenders,
                "스테이징 sim 모듈이 엔진을 참조한다. 이 층은 아직 Wassup.Runtime 소속이라 컴파일러가 " +
                "막아주지 않으므로, 새면 졸업이 그만큼 미뤄진다. 잔재를 의도했다면 AllowedEngineUsers 와 "
                + "17_sim_lib_skeleton.md 를 함께 갱신할 것: " + string.Join(", ", offenders));
        }

        /// sim 은 Bridge 를 모른다 — 제약 1 후계(의존 방향). 주석의 언급은 허용한다.
        /// 졸업 층은 어셈블리 순환 금지로 이미 불가능하므로 스테이징만 본다.
        [Test]
        public void StagingSimModules_DoNotReferenceTheBridge()
        {
            var offenders = new List<string>();
            foreach (string path in StagingFiles())
            {
                foreach (string line in File.ReadAllLines(path))
                {
                    string trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("///")) continue;
                    // `GameManager`·`TimeManager` 도 함께 막는다(리뷰 L6): 스테이징은
                    // `Wassup.Runtime` 안에 있어 그 네임스페이스의 Mono 싱글턴에 손이 닿는다.
                    if (trimmed.Contains("Wassup.Bridge") || trimmed.Contains("BattleBridge")
                        || trimmed.Contains("GameManager") || trimmed.Contains("TimeManager"))
                    {
                        offenders.Add($"{Path.GetFileName(path)}: {trimmed}");
                        break;
                    }
                }
            }

            CollectionAssert.IsEmpty(offenders,
                "sim 후보 모듈이 Bridge 를 참조한다 — 의존 방향이 뒤집혔다(CLAUDE.md 제약 1 후계): "
                + string.Join(" | ", offenders));
        }

        /// <summary>
        /// unit 17 — **게이트 자신을 지킨다.** `noEngineReferences` 가 false 로 돌아가면 졸업 층의
        /// 엔진 무참조는 그 순간 아무도 강제하지 않게 되고(텍스트 게이트는 이제 그쪽을 안 본다),
        /// 그 사실이 조용하다. 이 단정이 그 침묵을 깬다.
        ///
        /// 참조 목록도 함께 본다: `Wassup.Runtime` 을 넣으면 어셈블리 순환으로 Unity 가 거부하지만,
        /// 엔진 어셈블리(`Unity.Entities`·`Unity.Collections`)는 **순환이 아니라서 그냥 들어온다**.
        /// `Unity.Mathematics` 는 UnityEngine 을 참조하지 않아 허용이다(unit 17 결정 (a)).
        /// </summary>
        [Test]
        public void SimAsmdef_KeepsTheCompilerGateArmed()
        {
            Assert.IsTrue(File.Exists(SimAsmdefPath), $"sim asmdef 가 없다: {SimAsmdefPath}");
            var def = JsonUtility.FromJson<AsmdefShape>(File.ReadAllText(SimAsmdefPath));

            Assert.AreEqual("Wassup.Sim", def.name, "어셈블리 이름");
            Assert.IsTrue(def.noEngineReferences,
                "noEngineReferences 가 꺼졌다 — 졸업 층의 엔진 무참조를 강제하는 것이 하나도 없어진다.");
            Assert.IsFalse(def.allowUnsafeCode, "sim 은 unsafe 를 쓰지 않는다(이식 가능성).");

            var banned = new[] { "Unity.Entities", "Unity.Collections", "Unity.Burst", "Wassup.Runtime" };
            foreach (string r in def.references ?? new string[0])
                CollectionAssert.DoesNotContain(banned, r,
                    $"sim asmdef 가 '{r}' 를 참조한다 — 신 sim 은 관리 컬렉션만 쓰고 소비자를 모른다.");
        }

        [System.Serializable]
        private class AsmdefShape
        {
            public string name;
            public string[] references;
            public bool allowUnsafeCode;
            public bool noEngineReferences;
        }
    }
}
