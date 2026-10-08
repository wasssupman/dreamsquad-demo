using NUnit.Framework;
using UnityEngine;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCoreUnity;
using Somnia.Battle.Data;
using CoreRule = Somnia.Battle.BattleCore.Combat.Emission.PatternSelectionRule;

namespace Somnia.Battle.Tests.EditMode
{
    // battle-core-rebuild unit 3 결함 — **발사 명세 선정 규칙의 번호가 저작과 어긋났다.**
    //
    // 코어 enum 이 번호를 「읽기 좋게」 재배열(`None = 0`)해 두었는데 빌더가 통짜 캐스트로
    // 옮기고 있었다. 저작 값은 이미 구워진 `ProjectilePatternData` 에셋에 들어 있으므로
    // **12개 중 11개가 다른 규칙으로 읽혔다** — 0(캐논·나이트메어 탄막 = 순회 폭격)이
    // 「선택 안 함」이 되고, 2(샷거너·머신거너·관통·저격·마크스맨 = 방향 발사)가
    // 「무작위 저격」이 됐다. 어셈블리가 갈려 컴파일러가 못 잡는 자리라 테스트가 그물이다.
    [TestFixture]
    public class PatternSelectionRulePinTests
    {
        [Test]
        public void 이름과_값이_저작과_코어에서_같다()
        {
            Assert.AreEqual((byte)PatternSelectionRule.RoundRobin, (byte)CoreRule.RoundRobin);
            Assert.AreEqual((byte)PatternSelectionRule.DeterministicShuffle, (byte)CoreRule.DeterministicShuffle);
            Assert.AreEqual((byte)PatternSelectionRule.None, (byte)CoreRule.None);
            Assert.AreEqual((byte)PatternSelectionRule.Nearest, (byte)CoreRule.Nearest);

            Assert.AreEqual(System.Enum.GetValues(typeof(PatternSelectionRule)).Length,
                            System.Enum.GetValues(typeof(CoreRule)).Length,
                            "한쪽에만 규칙이 늘었다 — append-only 계약이다");
        }

        [Test]
        public void 저작_0_은_순회_폭격이다()
        {
            // 재현 단언. 수정 전에는 `None`(선택 안 함)으로 들어가 캐논·나이트메어 탄막이
            // **아무도 안 겨누는 방향 발사**가 됐다.
            Assert.AreEqual(0, (int)PatternSelectionRule.RoundRobin);
            Assert.AreEqual(CoreRule.RoundRobin,
                            CombatDefinitionBuilder.ToCoreSelection(PatternSelectionRule.RoundRobin));
        }

        [Test]
        public void 저작_2_는_방향_발사다()
        {
            // 수정 전에는 `DeterministicShuffle`(무작위 저격)로 들어가 샷거너·머신거너·
            // 관통·저격·마크스맨이 **엉뚱한 적을 겨눴다.**
            Assert.AreEqual(2, (int)PatternSelectionRule.None);
            Assert.AreEqual(CoreRule.None,
                            CombatDefinitionBuilder.ToCoreSelection(PatternSelectionRule.None));
        }

        [Test]
        public void 네_규칙_전부_이름으로_옮겨진다()
        {
            foreach (PatternSelectionRule authored in System.Enum.GetValues(typeof(PatternSelectionRule)))
                Assert.AreEqual(authored.ToString(),
                                CombatDefinitionBuilder.ToCoreSelection(authored).ToString(),
                                $"저작 {authored}({(int)authored}) 가 다른 규칙으로 옮겨진다");
        }

        [Test]
        public void 빌더를_통째로_지나도_규칙이_유지된다()
        {
            // 매핑 함수만 맞고 호출부가 캐스트로 남아 있으면 위 단언은 통과하고 판만 틀린다.
            // 그래서 **정의표까지** 간 값을 본다.
            var barrel = ScriptableObject.CreateInstance<ProjectileData>();
            var pattern = ScriptableObject.CreateInstance<ProjectilePatternData>();
            var volley = ScriptableObject.CreateInstance<DirectionalVolleyAbility>();
            var defender = ScriptableObject.CreateInstance<DefenderUnitData>();
            try
            {
                pattern.barrel = barrel;
                // 다연발의 탄은 **유닛의 탄**이어야 붙는다(옛 `BakeDefenderDirectionalPattern` 거절 —
                // 2026-09-24 드리프트 감사로 복원). 유효한 저작이어야 규칙까지 간다.
                defender.projectile = barrel;
                pattern.selection = PatternSelectionRule.RoundRobin;   // 저작 0
                volley.pattern = pattern;
                defender.abilities.Add(volley);

                var def = MatchDefinitionBuilder.Build(
                    new[] { defender }, System.Array.Empty<AttackUnitData>(), 1, ModeDef.Default());

                Assert.AreEqual(1, def.Patterns.Length, "발사 명세가 정의표에 안 실렸다");
                Assert.AreEqual((int)CoreRule.RoundRobin, def.Patterns[0].Selection,
                    "저작 0 이 정의표에서 순회 폭격이 아니다");
            }
            finally
            {
                Object.DestroyImmediate(defender);
                Object.DestroyImmediate(volley);
                Object.DestroyImmediate(pattern);
                Object.DestroyImmediate(barrel);
            }
        }
    }
}
