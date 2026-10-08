using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCore.Combat.Emission;
using Somnia.Battle.BattleCore.Combat.Projectile;
using static Somnia.Battle.Tests.EditMode.Core.CoreCombatFixtures;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild 2026-09-24 드리프트 감사 — **발사 명세(연발) 유닛의 평타.**
    //
    // 머신거너·샷거너가 겪던 세 증상을 판 위에서 증언한다:
    //   H1 연발탄 피해가 저작 패턴 값(0)이다 — 공격 산출물이 정해야 한다.
    //   H2 연발 유닛이 평타 단발탄까지 같이 쏜다 — 패턴이 단발을 **대체**한다.
    //   H3 선정 규칙 없는(방향 발사) 연발의 기준 방향이 늘 +Z(북쪽)다 — 조준 방향이어야 한다.
    //
    // ⚠ 수치는 픽스처다(`CoreCombatFixtures` 규율).
    [TestFixture]
    public class PatternAttackTests
    {
        private const float AttackDamage = 10f;
        private const int Shots = 3;

        private static BattleMatch Match(MatchDefinition def)
        {
            var m = new BattleMatch(def);
            m.Begin();
            return m;
        }

        /// <summary>
        /// 방향 직선탄 연발 유닛. 패턴 저작 피해는 **0**(라이브 머신거너 저작과 같은 모양)이고,
        /// 발은 전부 간격 0 이라 트리거 틱에 한꺼번에 나간다. 선정 규칙은 없음(방향 발사).
        /// </summary>
        private static MatchDefinition Fixture()
        {
            var def = Definition(defenderDamage: AttackDamage, defenderRange: 4f, enemyHealth: 100000f);
            GiveProjectile(def, MovementKind.DirectionalLinear, PayloadKind.PathHit,
                           speed: 0.01f, maxDistance: 20f);
            var shots = new PatternShotDef[Shots];
            for (int i = 0; i < Shots; i++)
                shots[i] = new PatternShotDef { DirectionT = i / (float)(Shots - 1), IntervalAfterPreviousSec = 0f };
            def.Patterns = new[]
            {
                new PatternDef
                {
                    Id = "fixture_volley",
                    BarrelProjectileDefIndex = 0,
                    Selection = (int)PatternSelectionRule.None,
                    MinAngleDeg = -20f,
                    MaxAngleDeg = 20f,
                    Shots = shots,
                    ReselectPerShot = true,
                },
            };
            def.Units[0].Attack.PatternDefIndices = new[] { 0 };
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        /// <summary>방어유닛 동쪽 칸, 적 서쪽 칸 — 조준 방향은 −X(서쪽)다.</summary>
        private static (BattleMatch m, List<CoreEvent> spawned) FirstVolley()
        {
            var m = Match(Fixture());
            var spawned = Listen(m, CoreEventKind.ProjectileSpawned);
            m.Apply(Command.DebugSpawnDefender(0, new int2(6, 2)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(4, 2)));
            // 첫 틱 = 트리거(요청), 둘째 틱 = 탄 물질화(탄 단계가 전투 단계 앞에 돈다).
            Tick(m, 2);
            return (m, spawned);
        }

        [Test]
        public void 연발탄의_피해는_패턴_저작값이_아니라_공격_피해다()
        {
            var (_, spawned) = FirstVolley();
            Assert.Greater(spawned.Count, 0, "첫 트리거에 탄이 한 발도 안 나갔다");
            foreach (var e in spawned)
                Assert.AreEqual(AttackDamage, e.Amount, 1e-4f,
                    "연발탄 피해가 패턴 저작값(0)이다 — 머신거너가 아무도 못 죽인다");
        }

        [Test]
        public void 연발_유닛은_평타_단발을_따로_쏘지_않는다()
        {
            var (_, spawned) = FirstVolley();
            Assert.AreEqual(Shots, spawned.Count,
                "한 번의 공격에 패턴 발수만큼만 나가야 한다 — 단발탄이 한 발 더 섞였다");
        }

        [Test]
        public void 방향_발사_연발의_기준은_조준_방향이다()
        {
            var (m, _) = FirstVolley();
            var ps = m.World.Projectiles;
            Assert.Greater(ps.Count, 0, "연발이 안 나갔다");
            float2 sum = float2.zero;
            for (int i = 0; i < ps.Count; i++) sum += ps[i].Direction;
            float2 center = math.normalizesafe(sum);
            Assert.AreEqual(-1f, center.x, 1e-3f,
                $"연발 중심이 적(서쪽)을 향해야 한다 — 실제 {center}(북쪽 +Z 면 적 위치와 무관하게 쏜 것)");
            for (int i = 0; i < ps.Count; i++)
                Assert.Less(ps[i].Direction.x, 0f, $"{i}번 탄이 적 반대편으로 나갔다: {ps[i].Direction}");
        }
    }
}
