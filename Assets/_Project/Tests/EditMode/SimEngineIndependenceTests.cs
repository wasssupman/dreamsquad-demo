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
                // **실제 using 지시문만** 본다. 파일 전체 문자열 검색은 "이 파일은 using
                // UnityEngine 을 갖는다" 같은 **주석 언급까지 잡는다** — 실제로 이 테스트를 처음
                // 돌렸을 때 그 오탐이 났다(그 주석을 방금 내가 썼다).
                foreach (string line in File.ReadAllLines(path))
                {
                    string trimmed = line.TrimStart();
                    if (!trimmed.StartsWith("using ")) continue;
                    if (trimmed.StartsWith("using UnityEngine") || trimmed.StartsWith("using Unity.Entities"))
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
