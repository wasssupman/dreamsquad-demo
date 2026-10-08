using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using Somnia.Battle.Data;
using Somnia.Battle.BattleCore.Trigger;

namespace Somnia.Battle.Tests.EditMode
{
    // dreamcatcher-attack-decoupling unit 1 — 실제 에셋 가드 둘(캐스터 아키타입 분리 · 비수 폴백 반경).
    //
    // skill-data-table 4-정리(B21) — 옛 사본(`DcApplicability` · SO 로 근사한 host profile)으로 돌던 전수 행렬 넷(미분류 0 ·
    // 잠금 표 · 통통구슬 경로)은 사본과 함께 지웠다. 정본 판정 = 코어 `Applicability`(런타임 host profile ·
    // `EditModeCore/ApplicabilityTests` — preflight 와 커밋이 같은 답).
    public class DcApplicabilityMatrixTests
    {
        private const string CardsRoot = "Assets/_Project/Data/Dreamcatcher";
        private const string DefendersRoot = "Assets/_Project/Data/Defenders";

        private static List<T> LoadAll<T>(string root) where T : UnityEngine.Object
        {
            var list = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { root }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }
            return list;
        }

        // ── unit 4: host 당 사건 지점 1개 (계약 2 상호배타) ────────────────
        // **attackRange 절은 은퇴했다 (2026-07-29).** 원래 이 테스트는 "캐스터가 RESOLVE 에
        // 못 가는 것은 에셋 값의 우연(attackRange 0)이다" 를 지키는 카나리아였는데, 유닛 스탯
        // 시트가 캐스터 attackRange 를 3 으로 확정했고 **시트가 정본**이다. 그래서 계약 2 를
        // 데이터 모양이 아니라 코드가 보장하도록 바꿨다 — `AttackSystem` 의 캐스트 드레인이
        // 이번 프레임 카운트한 host 를 기록하고 RESOLVE 카운팅 블록이 그 host 를 건너뛴다
        // (`castCountedHosts`). attackRange 가 얼마든 host 당 프레임 1카운트다.
        //
        // outputs 절은 남긴다 — 이건 이중 카운트 가드가 아니라 **설계 가드**다(캐스터가 일반
        // 공격 피해까지 갖는 것은 아키타입 혼선). 이중 카운트 회귀는 PlayMode 로 잡아야 한다.
        [Test]
        public void HazardCasters_DoNotAlsoDealBasicAttackDamage()
        {
            var units = LoadAll<DefenderUnitData>(DefendersRoot);
            var offenders = new StringBuilder();
            var checkedAny = false;

            foreach (var unit in units)
            {
                if (unit.GetAbility<HazardCastAbility>() == null) continue;
                checkedAny = true;
                if (unit.outputs != null && unit.outputs.Length > 0)
                    offenders.AppendLine($"{unit.id}: outputs {unit.outputs.Length}개 (일반 공격을 갖는다)");
            }

            Assert.IsTrue(checkedAny, "HazardCastAbility 보유 유닛을 찾지 못했다");
            Assert.IsEmpty(offenders.ToString(),
                "캐스터는 캐스트로만 피해를 준다(아키타입 분리):\n" + offenders);
        }

        // ── unit 2: 비수 폴백 반경이 에셋·bake 에 살아 있는지 ──────────────
        // 시트 Skills 탭의 range_tiles 셀이 명시적 0 이면 로그인 import 가 효과 에셋을
        // 되돌린다(blank 만 keep) → 폴백이 조용히 죽는다. 이 테스트가 그 회귀를 잡는다.
        [Test]
        public void PokeNeedle_HasPositiveFallbackRange()
        {
            var card = AssetDatabase.LoadAssetAtPath<DreamcatcherCard>(
                CardsRoot + "/Card_PokeNeedle.asset");
            Assert.IsNotNull(card, "Card_PokeNeedle.asset 을 찾지 못했다");

            var found = false;
            foreach (var m in card.RuleView())
            {
                if (m.payload.kind != EffectKind.ProjectileToTarget) continue;
                found = true;
                Assert.Greater(m.payload.tileRange, 0,
                    "폴백 탐색 반경이 0 이면 host 타겟이 없는 유닛에서 니들이 영영 안 나간다. "
                    + "시트 Skills 탭에서 poke_needle 이 가리키는 효과 줄의 range_tiles 셀도 함께 확인할 것");
            }
            Assert.IsTrue(found, "ProjectileToTarget 메커닉이 없다");
        }
    }
}
