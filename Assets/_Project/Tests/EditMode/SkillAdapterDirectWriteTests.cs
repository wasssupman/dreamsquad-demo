using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Wassup.Tests.EditMode
{
    // 토대 계약 3 — 어댑터의 intent 적용은 **채널 쓰기**(큐 enqueue / 인박스 버퍼 append)
    // 또는 **ECB 스테이징**이고, 컴포넌트 직접 쓰기는 금지다. 예외는 ECB 가 구조적으로
    // 표현할 수 없는 셋뿐이고 그 목록은 닫혀 있다(재리뷰 H-2).
    //
    // ⚠ **이 그물이 없으면 「폐쇄 목록」은 주장일 뿐이다.** 네 번째 직접 쓰기가 끼어들어도
    // 컴파일도 되고 테스트도 초록이고 대개 라이브에서도 멀쩡해 보인다 — 틀렸을 때만,
    // 그것도 조용히 어긋난다(재생 한 박자 차이는 대개 눈에 안 띈다).
    //
    // 이 테스트가 **보증하지 않는 것**: 개수만 센다. 하나를 빼고 하나를 더하면 통과한다.
    // 목록의 정합성은 아래 이름 단언이 받치고, 나머지는 리뷰의 몫이다.
    public class SkillAdapterDirectWriteTests
    {
        // ECB 가 못 하는 이유는 README 표에 있다. 요약: 앞의 둘은 읽고-고쳐-쓰기,
        // 마지막 하나는 채널 append 와 원자적으로 같이 붙어야 한다.
        private static readonly string[] AllowedCases =
        {
            "case SimIntentKind.DelaySelfAttack",
            "case SimIntentKind.ScaleKillReward",
            "case SimIntentKind.BeginDreamCocoon",
        };

        private static string AdapterSource()
        {
            var path = Path.Combine(Application.dataPath,
                "_Project/Scripts/Battle/Skills/EcsSkillContext.cs");
            Assert.IsTrue(File.Exists(path), $"어댑터를 옮겼으면 이 경로도 옮겨야 한다: {path}");
            return File.ReadAllText(path);
        }

        [Test]
        public void Adapter_HasExactlyThreeDirectComponentWrites()
        {
            var src = AdapterSource();

            // 구조 변경 + 값 덮어쓰기 전부. `_em.GetBuffer(...).Add` 는 **의도적으로 뺀다** —
            // 그건 인박스 append 라 계약 3 의 첫 갈래이지 예외가 아니다.
            var direct = new Regex(
                @"_em\.(SetComponentData|AddComponentData|AddComponent\b|AddComponent<|RemoveComponent|SetComponentEnabled)");
            var hits = new List<string>();
            foreach (Match m in direct.Matches(src)) hits.Add(m.Value);

            Assert.AreEqual(3, hits.Count,
                "어댑터의 컴포넌트 직접 쓰기는 3건이어야 한다(토대 계약 3 의 폐쇄 목록). " +
                $"지금 {hits.Count}건: [{string.Join(", ", hits)}]. " +
                "정당하게 늘렸다면 docs/spec/skill-layer-foundation/README.md 계약 3 의 표와 " +
                "이 테스트를 같은 커밋에서 갱신하라. 아니면 ECB 로 보내라.");
        }

        [Test]
        public void Adapter_DirectWriteCases_AreTheAuthoredOnes()
        {
            var src = AdapterSource();
            foreach (var label in AllowedCases)
                Assert.IsTrue(src.Contains(label),
                    $"폐쇄 목록의 «{label}» 이 어댑터에서 사라졌다 — 목록과 README 표를 같이 고쳐라.");
        }
    }
}
