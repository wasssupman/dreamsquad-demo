using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // battle-core-rebuild unit 7d — 사망 seam 의 두 코어 규칙(분열 · 길막 폭발)이 **라이브 저작에서 정의표로** 들어오나.
    // 코어 쪽 규칙은 `SplitTests`·`BlockingHazardTests` 가 고정구로 증언한다 — 여기는 빌더 매핑이 조용히 빠지지 않았나만 본다
    // (handoff 함정 8 「빌더 매핑 누락은 조용히 죽는다」). 값은 에셋에서 읽는다(밸런스 리터럴 금지).
    [TestFixture]
    public class DeathSiteBakeTests
    {
        private static T[] All<T>(string filter) where T : Object
        {
            var guids = AssetDatabase.FindAssets(filter, new[] { "Assets/_Project/Data" });
            var list = new List<T>(guids.Length);
            foreach (var g in guids)
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g));
                if (a != null) list.Add(a);
            }
            return list.ToArray();
        }

        [Test]
        public void 라이브_분열_상한은_옛_브리지_상수_그대로_코어_기본값과_같다()
        {
            // M2 — 상한은 저작 사고 방어선이다(옛 `BattleBridge.MaxSplitChildren` = 코어 `MovementTuningDef.Default()`).
            // 라이브 SO 가 그 값과 다르면 「기본값 = 라이브」 계약이 깨지고 canonical 줄이 생긴다.
            var configs = All<MovementTuningConfig>("t:MovementTuningConfig");
            Assert.IsNotEmpty(configs, "라이브 이동 저작이 없다면 테스트가 공허하다");
            int expected = MovementTuningDef.Default().SplitMaxChildren;
            foreach (var c in configs)
            {
                Assert.AreEqual(expected, c.SplitMaxChildren, c.name);
                Assert.AreEqual(expected, MatchDefinitionBuilder.ToMovementDef(c).SplitMaxChildren, c.name + " — 빌더 매핑");
            }
        }

        [Test]
        public void 라이브_분열체는_자식_줄과_자식_수를_싣고_자식은_표에_편입된다()
        {
            var slimes = new List<AttackUnitData>();
            foreach (var e in All<AttackUnitData>("t:AttackUnitData"))
                if (SplitChain.NextInChain(e) != null) slimes.Add(e);
            Assert.IsNotEmpty(slimes, "라이브 분열체가 없다면 테스트가 공허하다");

            foreach (var slime in slimes)
            {
                // 보너스 한 종만 가리키는 판 — 자식은 **어느 풀에도 없어도** 표에 들어와야 한다.
                var bonus = ScriptableObject.CreateInstance<BonusWaveData>();
                bonus.enemyUnit = slime;
                var enemies = MatchDefinitionBuilder.CollectEnemies(null, null, bonus);
                Object.DestroyImmediate(bonus);

                var def = MatchDefinitionBuilder.Build(System.Array.Empty<DefenderUnitData>(), enemies, 1, ModeDef.Default());
                int row = System.Array.IndexOf(enemies, slime);
                Assert.AreEqual(SplitChain.CountAt(slime), def.Enemies[row].SplitCount, slime.name);
                var child = SplitChain.NextInChain(slime);
                Assert.AreEqual(child.id, def.Enemies[def.Enemies[row].SplitChildDefIndex].Id, slime.name + " — 자식 줄");
            }
        }

        [Test]
        public void 폭탄_배럴의_폭발_탄은_탄_표에_편입된다()
        {
            var def = MatchDefinitionBuilder.Build(All<DefenderUnitData>("t:DefenderUnitData"),
                                                   System.Array.Empty<AttackUnitData>(), 1, ModeDef.Default());
            int exploding = 0;
            foreach (var b in def.BlockingHazards)
            {
                // U10 — 폭발 **피해**는 효과 줄에 있다(길막 줄은 폭발 탄·반경만). 폭발 탄이 배선된 줄이 폭발 길막이다 —
                // 탄이 표 밖이면 빌더가 오류를 낸다(`ToBlockingHazardDef` · Unity 러너는 예상 밖 오류 로그를 실패로 친다).
                if (b.ExplodeProjectileDefIndex < 0) continue;
                exploding++;
                Assert.GreaterOrEqual(b.ExplodeProjectileDefIndex, 0, b.Id + " — 폭발 탄이 표 밖이면 부서져도 안 터진다");
                Assert.Less(b.ExplodeProjectileDefIndex, def.Projectiles.Length);
            }
            Assert.Greater(exploding, 0, "라이브 폭발 길막(폭탄 배럴)이 없다면 테스트가 공허하다");
        }
    }
}
