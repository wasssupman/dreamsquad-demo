using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Wassup.Tests.EditMode
{
    /// <summary>
    /// battle-sim-extraction — `Scripts/Sim/` 의 **엔진 의존 드리프트 게이트**.
    ///
    /// unit 17 이 asmdef 로 `UnityEngine` 참조를 컴파일 에러로 만들 때까지, sim 후보 모듈의 엔진
    /// 무참조를 강제하는 수단이 **하나도 없다**. 실제로 그래서 새어 나갔다: unit 14 시점에는 폴더
    /// 전체가 무참조였는데 unit 15-B 의 `MatchPlacementRules` 가 `using UnityEngine` 을 들여왔고,
    /// 그것을 부정하는 문서·주석·커밋 메시지 3곳이 그대로 살아 있었다(리뷰 양측 H1).
    ///
    /// 이 테스트는 **허용 목록**이다. 새 파일이 엔진을 참조하면 실패하고, 잔재를 없애면 통과한다
    /// (목록은 줄어들기만 한다 — 늘리려면 unit 17 계획을 함께 고쳐야 한다는 뜻이다).
    /// </summary>
    public class SimEngineIndependenceTests
    {
        // 알려진 잔재. **여기에 파일을 추가하는 것은 unit 17 의 이식 부채를 늘리는 것**이므로,
        // 추가하려면 `17_sim_lib_skeleton.md` 의 이식 대상 목록도 같이 갱신해야 한다.
        private static readonly HashSet<string> AllowedEngineUsers = new HashSet<string>
        {
            // `HashSet<Vector2Int> occupied` + `new Vector2Int(...)` 때문. `int2` 로 가려면
            // `_occupiedTiles` 를 포함한 넓은 리팩터라 unit 18 이 처분한다.
            "MatchPlacementRules.cs",
        };

        /// 줄에서 `//` 이후를 잘라낸다. 문자열 리터럴 안의 `//` 까지 고려하지는 않는다 —
        /// 이 게이트의 대상(엔진 참조 탐지)에서는 과탐이 미탐보다 낫다.
        private static string StripComment(string line)
        {
            int idx = line.IndexOf("//", System.StringComparison.Ordinal);
            return idx >= 0 ? line.Substring(0, idx) : line;
        }

        private static string SimRoot =>
            Path.Combine(Application.dataPath, "_Project", "Scripts", "Sim");

        [Test]
        public void SimModules_DoNotReferenceUnityEngine_BeyondTheKnownResidue()
        {
            Assert.IsTrue(Directory.Exists(SimRoot), $"Sim 폴더가 없다: {SimRoot}");

            var offenders = new List<string>();
            foreach (string path in Directory.GetFiles(SimRoot, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(path);
                if (AllowedEngineUsers.Contains(name)) continue;
                // 두 형태를 모두 본다:
                //   ① `using UnityEngine;` 지시문
                //   ② **정규화 참조** `UnityEngine.Random.Range(...)` — using 없이도 엔진에 닿는다.
                //      실존 사례: `Core/MatchSeed.cs:25`(`UnityEngine.Random`)와
                //      `Core/Session/MatchSession.cs:28,110`(`UnityEngine.Debug`). 둘 다 sim 이주
                //      후보인데 `using` 만 보는 게이트는 **통과시킨다** — unit 17 정찰이 잡은 구멍이다.
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
                "sim 후보 모듈이 엔진을 참조한다. unit 17 은 이 참조를 컴파일 에러로 만드는 것이 " +
                "완료 기준이므로, 지금 새는 것은 잘못된 기준선 위에 unit 17 을 계획하게 만든다. " +
                "잔재를 의도했다면 AllowedEngineUsers 와 17_sim_lib_skeleton.md 를 함께 갱신할 것: "
                + string.Join(", ", offenders));
        }

        /// sim 은 Bridge 를 모른다 — 제약 1 후계(의존 방향). 주석의 언급은 허용한다.
        [Test]
        public void SimModules_DoNotReferenceTheBridge()
        {
            var offenders = new List<string>();
            foreach (string path in Directory.GetFiles(SimRoot, "*.cs", SearchOption.AllDirectories))
            {
                foreach (string line in File.ReadAllLines(path))
                {
                    string trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("///")) continue;
                    // `GameManager`·`TimeManager` 도 함께 막는다(리뷰 L6): `MatchOutcomeRules` 가
                    // `ScoreMath` 하나 때문에 `using Wassup.Core;` 를 갖고 있어, asmdef 가 없는
                    // 동안에는 그 네임스페이스의 Mono 싱글턴에 손이 닿아도 컴파일된다.
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
    }
}
