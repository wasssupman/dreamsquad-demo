using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat;
using Wassup.BattleCore.Combat.Emission;
using Wassup.BattleCore.Combat.Projectile;
using Wassup.BattleCore.Map;
using Wassup.Skills;
using Wassup.UnitAi;
using static Wassup.Tests.EditMode.Core.CoreCombatFixtures;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 9 구현 2 — **옛 테스트가 증언하던 전투 규칙**을 코어로 옮긴다.
    //
    // 대상: `ledgers/retire-test-pairs.md` 「규칙 누락 의심 — 모아 보기」 1·2·3·4·9·10·11.
    // (지속 락 5~8 은 `RetiredTargetLockPortTests`.) 옛 기계(엔티티·ECB·시스템)는 옮기지 않았다 —
    // 옛 단언이 말하던 **게임 규칙**만 `BattleMatch` 로 굴려 다시 묻는다.
    //
    // ⚠ 수치는 **픽스처**다(`CoreCombatFixtures` 규율). 기대값은 픽스처 값에서 파생한다.
    public class RetiredCombatRulePortTests
    {
        private static BattleMatch Match(MatchDefinition def)
        {
            var m = new BattleMatch(def);
            m.Begin();
            return m;
        }

        private static void Rehash(MatchDefinition def) => def.ConfigHash = def.ComputeConfigHash();

        private static float3 Center(int x, int z) => new float3(x + 0.5f, 0f, z + 0.5f);

        private static Unit SpawnDefender(BattleMatch m, int2 cell, int defIndex = 0)
        {
            m.Apply(Command.DebugSpawnDefender(defIndex, cell));
            return m.World.Units[m.World.Units.Count - 1];
        }

        private static Unit SpawnEnemy(BattleMatch m, int2 cell, int defIndex = 0)
        {
            m.Apply(Command.DebugSpawnEnemy(defIndex, cell));
            return m.World.Units[m.World.Units.Count - 1];
        }

        private static bool Hurt(Unit u) => u.Health < u.MaxHealth - 1e-4f;

        // 공격하지 않는 방어유닛 한 줄(직업만 다르다). `SecondaryTargetingTests.AddPassiveUnit` 과 같은 모양.
        private static int AddPassiveDefender(MatchDefinition def, int role)
        {
            var row = def.Units[0];
            row.Id = "port_passive_" + role;
            row.Role = role;
            row.TargetFactions = 0;
            var atk = AttackDef.Default();
            atk.Outputs = System.Array.Empty<AttackOutputDef>();
            row.Attack = atk;
            var units = new UnitDef[def.Units.Length + 1];
            System.Array.Copy(def.Units, units, def.Units.Length);
            units[def.Units.Length] = row;
            def.Units = units;
            return def.Units.Length - 1;
        }

        // ═════════════════════════════════════════════════════════════════════
        // 1. AggroAoeWidth — 어그로는 주 대상만 지배하고 광역 폭은 건드리지 않는다
        // ═════════════════════════════════════════════════════════════════════

        private static MatchDefinition AoeEnemy(int targetCount)
        {
            var def = Definition(defenderDamage: 0f, enemyDamage: 7f, enemyRange: 4f);
            def.Enemies[0].AttackTargetCount = targetCount;
            Rehash(def);
            return def;
        }

        // 첫 틱은 장애물 시그니처가 바뀌는 틱이라 어그로가 통째로 풀린다(`CombatRulesTests::끌려간_적은_가디언만_본다`).
        // 그래서 한 틱 굴린 **뒤에** 물리고, 다음 공격(쿨 1초)까지 굴려 그 한 번만 본다.
        private static void BiteThenNextSwing(BattleMatch m, Unit enemy, Unit guardian)
        {
            Tick(m, 1);
            enemy.Aggro = new Aggro { Target = guardian.Id, Capacity = 0 };
        }

        // 옛 AggroAoeWidthTests::AggroedEnemy_AoeStillReachesNeighbor_NotFoldedToSingleTarget — 물린 광역 적도 이웃을 친다
        [Test]
        public void 물린_광역_적도_가디언_옆의_이웃을_같이_친다()
        {
            var m = Match(AoeEnemy(2));
            var e = SpawnEnemy(m, new int2(2, 2));
            var neighbor = SpawnDefender(m, new int2(3, 2));   // 최근접 — 주 대상은 못 된다(물렸으니)
            var guardian = SpawnDefender(m, new int2(5, 2));   // 더 멀다 — 주 대상
            BiteThenNextSwing(m, e, guardian);
            float nBefore = neighbor.Health, gBefore = guardian.Health;

            Tick(m, 61);
            Assert.Less(guardian.Health, gBefore, "물린 적의 주 대상은 가디언이다(sticky)");
            Assert.Less(neighbor.Health, nBefore,
                "어그로여도 광역 폭은 줄지 않는다 — 대상 수를 1 로 접는 로직이 되살아났다");
        }

        // 옛 AggroAoeWidthTests::NotAggroed_Aoe_HitsBoth_Control — 대조군: 안 물린 광역도 둘 다
        [Test]
        public void 안_물린_광역_적은_최근접과_둘째를_친다()
        {
            var m = Match(AoeEnemy(2));
            SpawnEnemy(m, new int2(2, 2));
            var near = SpawnDefender(m, new int2(3, 2));
            var far = SpawnDefender(m, new int2(5, 2));

            Tick(m, 1);
            Assert.IsTrue(Hurt(near), "비어그로 주 대상 = 최근접");
            Assert.IsTrue(Hurt(far), "광역이 둘째 대상까지 닿는다");
        }

        // 옛 AggroAoeWidthTests::AggroedEnemy_SingleTargetAuthoring_StillHitsOnlyGuardian — 저작 1 은 어그로와 무관하게 1
        [Test]
        public void 단일_저작_적은_물려도_가디언만_친다()
        {
            var m = Match(AoeEnemy(1));
            var e = SpawnEnemy(m, new int2(2, 2));
            var neighbor = SpawnDefender(m, new int2(3, 2));
            var guardian = SpawnDefender(m, new int2(5, 2));
            BiteThenNextSwing(m, e, guardian);
            float nBefore = neighbor.Health, gBefore = guardian.Health;

            Tick(m, 61);
            Assert.Less(guardian.Health, gBefore, "가디언만 맞는다");
            Assert.AreEqual(nBefore, neighbor.Health, 1e-4f, "대상 수 1 은 어그로가 넓히지 않는다");
        }

        // 옛 AggroAoeWidthTests::AggroedEnemy_HoldsFire_WhenGuardianOutOfRange_EvenWithNeighborAdjacent — 가디언이 사거리 밖이면 미발사
        [Test]
        public void 물린_적은_가디언이_사거리_밖이면_옆의_이웃도_안_친다()
        {
            var m = Match(AoeEnemy(2));
            var e = SpawnEnemy(m, new int2(2, 2));
            var neighbor = SpawnDefender(m, new int2(3, 2));
            var guardian = SpawnDefender(m, new int2(10, 2));   // 사거리 4 밖
            BiteThenNextSwing(m, e, guardian);
            float nBefore = neighbor.Health, gBefore = guardian.Health;

            Tick(m, 61);
            Assert.AreEqual(nBefore, neighbor.Health, 1e-4f,
                "가디언이 사거리 밖이면 **아무도** 안 때린다 — 풀리면 적이 가디언에 영영 도착 못 한다");
            Assert.AreEqual(gBefore, guardian.Health, 1e-4f);
        }

        // 몸이 큰 가디언(배스티온형) 픽스처 — 사거리 1 · 몸 1.5 · 적 몸 0.25.
        private const float WideBody = 1.5f;
        private const float WideRange = 1f;

        private static (BattleMatch m, Unit g, Unit e) WideGuardianDuel(float gapTiles, bool guardian)
        {
            var def = Definition(defenderDamage: 9f, defenderRange: WideRange,
                                 aggroCapacity: guardian ? 2 : 0, enemyHealth: 100000f);
            def.Units[0].BodyRadiusTiles = WideBody;
            Rehash(def);
            var m = Match(def);
            var g = SpawnDefender(m, new int2(5, 2));
            var e = SpawnEnemy(m, new int2(6, 2));
            e.Position = g.Position + new float3(gapTiles, 0f, 0f);
            return (m, g, e);
        }

        // 옛 AggroAoeWidthTests::WideGuardian_DamagesEnemyTouchingItsBody_NotJustCellNeighbor — 몸이 맞닿은 적을 때린다
        [Test]
        public void 몸이_큰_가디언은_몸에_맞닿은_적에게_피해를_낸다()
        {
            float touching = WideBody + 0.25f;   // 몸과 몸이 맞닿는 거리
            var (m, g, e) = WideGuardianDuel(touching, guardian: true);
            Tick(m, 1);
            Assert.Greater(g.Attack.CooldownRemaining, 0f, "발사조차 안 했다 — 원인은 게이트다");
            Assert.IsTrue(Hurt(e), "공격은 성사됐는데 피해가 0 — 가디언 분기가 공격자 몸을 모른다");
        }

        // 옛 AggroAoeWidthTests::WideGuardian_ReachCurve_MatchesGate — 가디언 도달 곡선 = 게이트(사거리+내 몸+상대 몸)
        [Test]
        public void 가디언의_도달_곡선은_게이트와_같다()
        {
            float edge = WideRange + WideBody + 0.25f;
            float[] inside = { 0.9f, 1.51f, 1.75f, 2.0f, edge - 0.05f };
            float[] outside = { edge + 0.15f, edge + 0.75f };
            var pattern = new System.Text.StringBuilder();
            foreach (float d in inside)
            {
                var (m, _, e) = WideGuardianDuel(d, guardian: true);
                Tick(m, 1);
                if (!Hurt(e)) pattern.Append("안쪽인데 MISS:").Append(d.ToString("0.00")).Append(' ');
            }
            foreach (float d in outside)
            {
                var (m, _, e) = WideGuardianDuel(d, guardian: true);
                Tick(m, 1);
                if (Hurt(e)) pattern.Append("바깥인데 HIT:").Append(d.ToString("0.00")).Append(' ');
            }
            Assert.AreEqual(string.Empty, pattern.ToString().Trim(),
                $"가디언 도달 곡선이 게이트({edge})와 갈렸다");
        }

        // 옛 AggroAoeWidthTests::WideGuardian_AndPlainDefender_AgreeOnWhoIsHittable — 가디언 여부가 사거리를 바꾸지 않는다
        [Test]
        public void 가디언_여부는_누가_맞는가를_바꾸지_않는다()
        {
            float touching = WideBody + 0.25f;
            var (mg, _, eg) = WideGuardianDuel(touching, guardian: true);
            var (mp, _, ep) = WideGuardianDuel(touching, guardian: false);
            Tick(mg, 1);
            Tick(mp, 1);
            Assert.AreEqual(Hurt(ep), Hurt(eg),
                "도발 능력은 선정 우선순위만 바꿔야지 사거리를 바꾸면 안 된다");
            Assert.IsTrue(Hurt(ep), "전제: 일반 유닛은 맞닿은 적을 때린다");
        }

        // ═════════════════════════════════════════════════════════════════════
        // 2. AttackCommit — 공격 1회 커밋: 겨눈 대상을 때린다
        // ═════════════════════════════════════════════════════════════════════

        private const float CommitHitDelay = 0.3f;
        private const int WindUpTicks = 25;   // 선딜 0.3초(18틱)를 넉넉히 넘긴다

        private static MatchDefinition CommitDef(float hitDelay = CommitHitDelay, float cooldown = 10f, float range = 6f)
            => Definition(defenderDamage: 5f, defenderRange: range, defenderCooldown: cooldown,
                          defenderHitDelay: hitDelay, enemyHealth: 100000f);

        // 옛 AttackCommitTests::WindUp_NearerEnemyAppears_CommittedTargetStillTakesTheHit — A 를 겨누고 B 를 때리지 않는다
        [Test]
        public void 선딜_중_더_가까운_적이_와도_겨눈_대상을_때린다()
        {
            var m = Match(CommitDef());
            var d = SpawnDefender(m, new int2(0, 2));
            var a = SpawnEnemy(m, new int2(2, 2));
            var b = SpawnEnemy(m, new int2(5, 2));

            Tick(m, 1);   // START — a 커밋
            Assert.AreEqual(a.Id, d.Attack.CommittedTarget);
            b.Position = d.Position + new float3(1f, 0f, 0f);   // 선딜 중 b 가 더 가까워진다
            Tick(m, WindUpTicks);

            Assert.IsTrue(Hurt(a), "겨눈 대상이 맞아야 한다");
            Assert.IsFalse(Hurt(b), "선딜 중 끼어든 대상은 맞지 않는다");
        }

        // 옛 AttackCommitTests::WindUp_CommittedTargetDies_StrictLapse_NoReselect — 커밋 대상 사망 = 불발, 재선정 없음
        [Test]
        public void 선딜_중_겨눈_대상이_죽으면_다른_적으로_갈아타지_않는다()
        {
            var m = Match(CommitDef());
            SpawnDefender(m, new int2(0, 2));
            var a = SpawnEnemy(m, new int2(2, 2));
            var b = SpawnEnemy(m, new int2(3, 2));

            Tick(m, 1);
            a.Inbox.Damage.Add(new DamageEntry { Amount = 1e9f, Source = SimEntityId.None });
            Tick(m, WindUpTicks);
            Assert.IsFalse(Hurt(b), "재선정 없음 — 이번 공격은 불발");
        }

        // 옛 AttackCommitTests::WindUp_CommittedTargetLeavesRange_StrictLapse — 사거리 이탈도 불발
        [Test]
        public void 선딜_중_겨눈_대상이_사거리를_벗어나면_빗나간다()
        {
            var m = Match(CommitDef());
            SpawnDefender(m, new int2(0, 2));
            var a = SpawnEnemy(m, new int2(2, 2));
            var b = SpawnEnemy(m, new int2(3, 2));

            Tick(m, 1);
            a.Position = Center(10, 4);   // 사거리 6 밖
            Tick(m, WindUpTicks);
            Assert.IsFalse(Hurt(a), "사거리 밖 대상은 안 맞는다");
            Assert.IsFalse(Hurt(b), "그렇다고 다른 적으로 갈아타지도 않는다");
        }

        // 옛 AttackCommitTests::WindUp_PastGoalIsNotALapseReason — 골을 지난 것은 커밋 해제 사유가 아니다
        [Test]
        public void 선딜_중_골을_지나도_커밋은_유지된다()
        {
            var m = Match(CommitDef());
            SpawnDefender(m, new int2(0, 2));
            var a = SpawnEnemy(m, new int2(2, 2));

            Tick(m, 1);
            a.Move.PastGoal = true;
            Tick(m, WindUpTicks);
            Assert.IsTrue(Hurt(a), "PastGoal 은 커밋 해제 사유가 아니다");
        }

        // 옛 AttackCommitTests::Commit_IsClearedAfterResolve — 커밋 수명은 START→RESOLVE 1회
        [Test]
        public void 커밋은_타격_뒤에_비워진다()
        {
            var m = Match(CommitDef());
            var d = SpawnDefender(m, new int2(0, 2));
            SpawnEnemy(m, new int2(2, 2));

            Tick(m, 1);
            Assert.IsFalse(d.Attack.CommittedTarget.IsNone, "START 직후엔 커밋이 서 있다");
            Tick(m, WindUpTicks);
            Assert.IsTrue(d.Attack.CommittedTarget.IsNone, "RESOLVE 가 커밋을 비운다");
        }

        // 옛 AttackCommitTests::ZeroHitDelay_BehaviorUnchanged — 선딜 0 은 같은 틱에 맞고 같은 틱에 비운다
        [Test]
        public void 선딜이_0이면_같은_틱에_맞고_커밋도_비워진다()
        {
            var m = Match(CommitDef(hitDelay: 0f));
            var d = SpawnDefender(m, new int2(0, 2));
            var a = SpawnEnemy(m, new int2(2, 2));

            Tick(m, 1);
            Assert.IsTrue(Hurt(a));
            Assert.IsTrue(d.Attack.CommittedTarget.IsNone);
        }

        // 옛 AttackCommitTests::NextAttack_ReselectsFreshly — 커밋은 공격 1회 안만 산다
        [Test]
        public void 다음_공격은_그때_새로_고른다()
        {
            var m = Match(CommitDef(cooldown: 0.1f));
            var d = SpawnDefender(m, new int2(0, 2));
            var a = SpawnEnemy(m, new int2(2, 2));
            var b = SpawnEnemy(m, new int2(5, 2));

            Tick(m, 1);
            Tick(m, WindUpTicks);
            Assert.IsTrue(Hurt(a), "1회차는 a");
            float aAfterFirst = a.Health;
            a.Position = Center(10, 4);                          // 사거리 밖
            b.Position = d.Position + new float3(1f, 0f, 0f);    // 최근접
            Tick(m, 80);
            Assert.IsTrue(Hurt(b), "다음 공격은 새로 고른다");
            Assert.AreEqual(aAfterFirst, a.Health, 1e-4f, "사거리 밖이 된 1회차 대상은 더 안 맞는다");
        }

        // 옛 AttackCommitTests::Crowd_OvertakingStream_HitAlwaysMatchesTheCommittedTarget — 추월 스트림에서 겨눈 ≡ 맞은
        [Test]
        public void 추월이_잦은_스트림에서도_겨눈_대상과_맞은_대상은_같다()
        {
            const float Range = 4f;
            var m = Match(CommitDef(cooldown: 0.5f, range: Range));
            var resolved = Listen(m, CoreEventKind.AttackResolved);
            var d = SpawnDefender(m, new int2(3, 2));

            const int N = 12;
            var enemies = new Unit[N];
            var speed = new float[N];
            var relX = new float[N];
            for (int i = 0; i < N; i++)
            {
                enemies[i] = SpawnEnemy(m, new int2(5, 0));
                relX[i] = 5.5f - i * 0.55f;
                speed[i] = 0.6f + 0.11f * (i % 5);   // index 기반 결정론 — 난수 없음
            }

            float reachEdge = Range + d.HitRadius + enemies[0].HitRadius;
            int starts = 0, resolves = 0, mismatchNow = 0, wouldHaveMismatched = 0;
            SimEntityId committed = SimEntityId.None;
            float dt = 1f / 60f;
            for (int f = 0; f < 4000; f++)
            {
                for (int i = 0; i < N; i++)
                {
                    relX[i] -= speed[i] * dt;
                    if (relX[i] < -1.5f) relX[i] = 6f;   // 재진입 — 스트림 유지
                    enemies[i].Position = d.Position + new float3(relX[i], 0f, 0f);
                }
                bool windUpBefore = d.Attack.Swinging;
                resolved.Clear();
                Tick(m, 1);
                if (!windUpBefore && d.Attack.Swinging) { committed = d.Attack.CommittedTarget; starts++; }
                if (!windUpBefore || d.Attack.Swinging) continue;
                resolves++;

                SimEntityId hit = SimEntityId.None;
                for (int k = 0; k < resolved.Count; k++) if (resolved[k].A == d.Id) hit = resolved[k].B;
                if (hit.IsNone) continue;   // 불발(strict lapse)

                SimEntityId nearest = SimEntityId.None;
                float bestSq = float.MaxValue;
                for (int i = 0; i < N; i++)
                {
                    float dx = enemies[i].Position.x - d.Position.x;
                    float dz = enemies[i].Position.z - d.Position.z;
                    float sq = dx * dx + dz * dz;
                    if (sq > reachEdge * reachEdge) continue;
                    if (sq < bestSq) { bestSq = sq; nearest = enemies[i].Id; }
                }
                if (hit != committed) mismatchNow++;
                if (!nearest.IsNone && nearest != committed) wouldHaveMismatched++;
            }

            Assert.Greater(resolves, 50, "시나리오가 충분히 돌아야 통계가 의미 있다");
            Assert.Greater(wouldHaveMismatched, 0, "이 시나리오가 결함을 자극하지 못하면 아래 단언이 공허하다");
            Assert.AreEqual(0, mismatchNow, $"겨눈 대상과 맞은 대상은 항상 같아야 한다(START {starts} · RESOLVE {resolves})");
        }

        // ═════════════════════════════════════════════════════════════════════
        // 3. AttackShapeGate — 부채꼴·띠 게이트의 절대값 · Omni 항등 · 주 대상 방향 회전
        // ═════════════════════════════════════════════════════════════════════

        private const float ShapeTr = 0.5f;     // 픽스처 — 대상 몸
        private const float ShapeSelfR = 0.5f;  // 픽스처 — 내 몸
        private const float ShapeTile = 1f;

        private static float Sin(float deg) => math.sin(math.radians(deg));
        private static float Cos(float deg) => math.cos(math.radians(deg));

        private static bool Sector(float along, float across, float fullAngleDeg, float tr = ShapeTr)
            => SkillMath.SectorGate(along, across, Sin(fullAngleDeg * 0.5f), Cos(fullAngleDeg * 0.5f), tr);

        private static bool Band(float along, float across, float halfWidth, float length, float tr = ShapeTr)
            => SkillMath.BandGate(along, across, halfWidth, length, tr);

        private static AttackShapeBaked SectorShape(float fullAngleDeg) => new AttackShapeBaked
        {
            kind = AttackShapeBaked.SectorKind,
            sinHalf = Sin(fullAngleDeg * 0.5f),
            cosHalf = Cos(fullAngleDeg * 0.5f),
        };

        private static AttackShapeBaked BandShape(float halfWidth) => new AttackShapeBaked
        {
            kind = AttackShapeBaked.BandKind,
            halfWidth = halfWidth,
        };

        private static float3 At(float x, float z) => new float3(x, 0f, z);
        private static readonly float2 Right = new float2(1f, 0f);
        private static readonly float2 Up = new float2(0f, 1f);

        // 옛 AttackShapeGateTests::Sector_OnAxis_IsInside — 축 위는 안
        [Test]
        public void 부채꼴_축_위는_안이다()
        {
            Assert.IsTrue(Sector(2f, 0f, 90f));
            Assert.IsTrue(Sector(0.1f, 0f, 30f), "꼭짓점 바로 앞도 안");
        }

        // 옛 AttackShapeGateTests::Sector_EdgeAngle_PointOnEdgeIsBoundaryIn_BodyStraddlesIn_BeyondBodyOut — 가장자리: 점은 경계 포함 · 몸이 걸치면 안 · 몸 너머는 밖
        [Test]
        public void 부채꼴_가장자리는_몸이_걸치면_안이고_몸_너머는_밖이다()
        {
            Assert.IsTrue(Sector(1f, 1f, 90f, tr: 0f), "점이면 경계 포함");
            Assert.IsTrue(Sector(1f, 1.3f, 90f), "몸이 가장자리에 걸치면 안");
            Assert.IsFalse(Sector(1f, 1.9f, 90f), "몸도 안 닿으면 밖");
            Assert.IsTrue(Sector(1f, -1.3f, 90f));
            Assert.IsFalse(Sector(1f, -1.9f, 90f));
        }

        // 옛 AttackShapeGateTests::Sector_BehindApex_UsesApexDistance_NotEdgeApproximation — 꼭짓점 뒤는 꼭짓점 거리
        [Test]
        public void 부채꼴_꼭짓점_뒤는_꼭짓점까지의_거리로_잰다()
        {
            Assert.IsFalse(Sector(-0.7f, 0f, 90f), "등 뒤 0.7 — 몸(0.5)이 꼭짓점에 안 닿는다(가장자리 근사면 샌다)");
            Assert.IsTrue(Sector(-0.3f, 0f, 90f), "등 뒤 0.3 — 몸이 꼭짓점을 덮는다");
            Assert.IsFalse(Sector(-0.7f, 0f, 30f), "좁은 각에서도 같은 답");
        }

        // 옛 AttackShapeGateTests::Sector_180_DegeneratesToHalfPlane — 180° 는 반평면
        [Test]
        public void 부채꼴_180도는_반평면이다()
        {
            Assert.IsTrue(Sector(0f, 5f, 180f), "옆도 안");
            Assert.IsTrue(Sector(-0.4f, 5f, 180f), "살짝 뒤라도 몸이 걸치면 안");
            Assert.IsFalse(Sector(-0.6f, 5f, 180f), "몸 반경 너머 뒤는 밖");
        }

        // 옛 AttackShapeGateTests::Band_LateralEdge_IsHalfWidthPlusBody — 띠 옆 경계 = 반폭 + 몸
        [Test]
        public void 띠_옆_경계는_반폭에_몸을_더한다()
        {
            Assert.IsTrue(Band(1f, 0.9f, 0.5f, 2f), "0.9 — 몸이 띠에 걸친다");
            Assert.IsFalse(Band(1f, 1.1f, 0.5f, 2f), "1.1 — 몸도 안 닿는다");
            Assert.IsTrue(Band(1f, -0.9f, 0.5f, 2f), "아래쪽 대칭");
        }

        // 옛 AttackShapeGateTests::Band_Behind_IsOutBeyondBody — 띠 뒤는 몸 너머면 밖
        [Test]
        public void 띠_뒤는_몸_너머면_밖이다()
        {
            Assert.IsTrue(Band(-0.4f, 0f, 0.5f, 2f));
            Assert.IsFalse(Band(-0.6f, 0f, 0.5f, 2f));
        }

        // 옛 AttackShapeGateTests::Band_FrontEdge_IsLengthPlusBody — 띠 앞 경계 = 길이 + 몸
        [Test]
        public void 띠_앞_경계는_길이에_몸을_더한다()
        {
            Assert.IsTrue(Band(2.4f, 0f, 0.5f, 2f));
            Assert.IsFalse(Band(2.6f, 0f, 0.5f, 2f));
        }

        // 옛 AttackShapeGateTests::Band_ZeroHalfWidth_IsValid_BodyOnAxisHits — 반폭 0 도 유효(몸만큼)
        [Test]
        public void 띠_반폭_0도_몸만큼은_맞는다()
        {
            Assert.IsTrue(Band(1f, 0.4f, 0f, 2f));
            Assert.IsFalse(Band(1f, 0.6f, 0f, 2f));
        }

        // 옛 AttackShapeGateTests::InReachShaped_Omni_IsBitIdenticalToInReach — Omni 는 원과 비트 동일
        [Test]
        public void 도형_Omni_는_원_판정과_비트_단위로_같다()
        {
            var omni = AttackShapeBaked.Omni;
            int checkedCount = 0;
            for (float range = 0f; range <= 4f; range += 0.5f)
                for (int x = -6; x <= 6; x++)
                    for (int z = -6; z <= 6; z++)
                    {
                        bool expected = AttackReach.InReach(At(0, 0), At(x, z), range, ShapeTile, ShapeSelfR, ShapeTr);
                        bool actual = AttackReach.InReachShaped(At(0, 0), At(x, z), range, ShapeTile, ShapeSelfR, ShapeTr, in omni, Right);
                        Assert.AreEqual(expected, actual, $"range={range} Δ=({x},{z})");
                        checkedCount++;
                    }
            Assert.Greater(checkedCount, 1000);
        }

        // 옛 AttackShapeGateTests::InReachShaped_RotatesWithPrimaryDirection — 도형은 주 대상 방향을 따라 돈다
        [Test]
        public void 도형은_주_대상_방향을_따라_돈다()
        {
            var s = SectorShape(90f);
            for (int x = -4; x <= 4; x++)
                for (int z = -4; z <= 4; z++)
                    Assert.AreEqual(
                        AttackReach.InReachShaped(At(0, 0), At(x, z), 4f, ShapeTile, ShapeSelfR, ShapeTr, in s, Right),
                        AttackReach.InReachShaped(At(0, 0), At(-z, x), 4f, ShapeTile, ShapeSelfR, ShapeTr, in s, Up),
                        $"Δ=({x},{z})@Right vs ({-z},{x})@Up");
        }

        // 옛 AttackShapeGateTests::InReachShaped_OppositeSide_IsOut_SameSideIn — 반대편은 밖
        [Test]
        public void 도형_반대편은_밖이고_주_대상_쪽은_안이다()
        {
            var s = SectorShape(90f);
            Assert.IsTrue(AttackReach.InReachShaped(At(0, 0), At(2, 0), 4f, ShapeTile, ShapeSelfR, ShapeTr, in s, Right));
            Assert.IsFalse(AttackReach.InReachShaped(At(0, 0), At(-2, 0), 4f, ShapeTile, ShapeSelfR, ShapeTr, in s, Right));
            Assert.IsTrue(AttackReach.InReachShaped(At(0, 0), At(-2, 0), 4f, ShapeTile, ShapeSelfR, ShapeTr, in s, new float2(-1f, 0f)),
                "주 대상이 왼쪽이면 왼쪽이 안");
        }

        // 옛 AttackShapeGateTests::InReachShaped_DirectionScaleDoesNotMatter — 방향 벡터의 크기는 무관
        [Test]
        public void 도형_방향_벡터의_크기는_답을_바꾸지_않는다()
        {
            var s = SectorShape(60f);
            Assert.AreEqual(
                AttackReach.InReachShaped(At(0, 0), At(2, 1), 4f, ShapeTile, ShapeSelfR, ShapeTr, in s, new float2(1f, 0.2f)),
                AttackReach.InReachShaped(At(0, 0), At(2, 1), 4f, ShapeTile, ShapeSelfR, ShapeTr, in s, new float2(37f, 7.4f)));
        }

        // 옛 AttackShapeGateTests::InReachShaped_UndefinedDirection_PassesShapeTerm — 방향 없음 = 도형 항 통과(원 항은 본다)
        [Test]
        public void 방향이_정의되지_않으면_도형_항은_통과하고_원_항만_본다()
        {
            var s = SectorShape(30f);
            Assert.IsTrue(AttackReach.InReachShaped(At(0, 0), At(-2, 0), 4f, ShapeTile, ShapeSelfR, ShapeTr, in s, float2.zero));
            Assert.IsFalse(AttackReach.InReachShaped(At(0, 0), At(-9, 0), 4f, ShapeTile, ShapeSelfR, ShapeTr, in s, float2.zero),
                "원 항은 여전히 본다");
        }

        // 옛 AttackShapeGateTests::InReachShaped_Band_OnAxisLength_MatchesReach — 띠 축 위 경계 = 원 경계
        [Test]
        public void 띠의_축_위_경계는_원_경계와_같다()
        {
            var b = BandShape(0.5f);
            float edge = 2f + ShapeSelfR + ShapeTr;
            Assert.IsTrue(AttackReach.InReachShaped(At(0, 0), At(edge, 0), 2f, ShapeTile, ShapeSelfR, ShapeTr, in b, Right), "경계 포함");
            Assert.IsFalse(AttackReach.InReachShaped(At(0, 0), At(edge + 0.02f, 0), 2f, ShapeTile, ShapeSelfR, ShapeTr, in b, Right), "경계 너머");
            Assert.IsFalse(AttackReach.InReachShaped(At(0, 0), At(0, 2), 2f, ShapeTile, ShapeSelfR, ShapeTr, in b, Right), "축 밖 세로 2 는 띠 밖");
        }

        // 옛 AttackShapeGateTests::InReachShaped_Band_WideBody_AxisEdgeStillMatchesReach — 큰 몸: 띠 길이 = 사거리 + 내 몸, 옆 폭은 안 넓어진다
        [Test]
        public void 큰_몸의_띠도_축_경계는_원과_같고_옆_폭은_넓어지지_않는다()
        {
            var b = BandShape(0.5f);
            const float wideSelf = 1.5f;
            float edge = 2f + wideSelf + ShapeTr;
            Assert.IsTrue(AttackReach.InReach(At(0, 0), At(edge, 0), 2f, ShapeTile, wideSelf, ShapeTr));
            Assert.IsTrue(AttackReach.InReachShaped(At(0, 0), At(edge, 0), 2f, ShapeTile, wideSelf, ShapeTr, in b, Right), "띠 축 경계 = 원 경계");
            Assert.IsFalse(AttackReach.InReachShaped(At(0, 0), At(edge + 0.02f, 0), 2f, ShapeTile, wideSelf, ShapeTr, in b, Right));
            Assert.IsTrue(AttackReach.InReachShaped(At(0, 0), At(2f, 0.9f), 2f, ShapeTile, wideSelf, ShapeTr, in b, Right));
            Assert.IsFalse(AttackReach.InReachShaped(At(0, 0), At(2f, 1.1f), 2f, ShapeTile, wideSelf, ShapeTr, in b, Right));
        }

        // 옛 AttackShapeGateTests::InReachShaped_TileSizeIsDividedOut_BeforeTheGate — 타일 크기는 게이트 전에 나눠진다
        [Test]
        public void 도형_게이트는_타일_크기를_먼저_나눈다()
        {
            var s = SectorShape(90f);
            Assert.AreEqual(
                AttackReach.InReachShaped(At(0, 0), At(1, 1.3f), 4f, 1f, ShapeSelfR, ShapeTr, in s, Right),
                AttackReach.InReachShaped(At(0, 0), At(2, 2.6f), 4f, 2f, ShapeSelfR, ShapeTr, in s, Right));
        }

        // 옛 AttackShapeGateTests::Gates_DegenerateInputs_ReturnBoolWithoutThrowing — 퇴화 입력도 던지지 않는다
        [Test]
        public void 도형_게이트는_퇴화_입력에도_던지지_않는다()
        {
            Assert.DoesNotThrow(() => Sector(0f, 0f, 90f, tr: 0f));
            Assert.DoesNotThrow(() => Band(0f, 0f, 0f, 0f, tr: 0f));
            Assert.IsTrue(Sector(0f, 0f, 90f, tr: 0f), "같은 자리 = 안");
            Assert.IsTrue(Band(0f, 0f, 0f, 0f, tr: 0f), "같은 자리 = 안");
        }

        // 옛 AttackShapeGateTests(판 위 짝 없음 — 띠 게이트 코어 테스트 0) — 띠 도형의 부가 타격은 폭 안만 고른다
        [Test]
        public void 띠_도형의_부가_타격은_폭_안만_고른다()
        {
            var def = Definition(defenderDamage: 5f, defenderRange: 4f, defenderTargetCount: 3);
            def.Units[0].Attack.ShapeKind = AttackShapeBaked.BandKind;
            def.Units[0].Attack.ShapeHalfWidth = 0.5f;
            Rehash(def);
            var m = Match(def);
            SpawnDefender(m, new int2(4, 2));
            var primary = SpawnEnemy(m, new int2(6, 2));   // 주 대상(+X)
            var onAxis = SpawnEnemy(m, new int2(7, 2));    // 띠 안
            var offAxis = SpawnEnemy(m, new int2(6, 3));   // 원 안, 띠 밖(옆 1 > 반폭 + 몸)

            Tick(m, 1);
            Assert.IsTrue(Hurt(primary));
            Assert.IsTrue(Hurt(onAxis), "띠 축 위 부가 대상은 맞는다");
            Assert.IsFalse(Hurt(offAxis), "원 안이라도 띠 밖은 안 맞는다");
        }

        // ═════════════════════════════════════════════════════════════════════
        // 4. AttackSystemStateGate — 적은 Engaging·Standoff 에서만 쏜다
        // ═════════════════════════════════════════════════════════════════════
        //
        // 코어는 상태를 매 틱 이동 단계가 다시 판정한다(`AiMovePhase.StepAiState`) — 그래서 「사거리
        // 안에 대상이 있는데 Marching」을 판 위에서 강제할 수 없다(옛 테스트는 상태 컴포넌트를 손으로
        // 박았다). 여기서는 **판이 만든 상태**마다 발사 여부를 묻는다.

        // 옛 AttackSystemStateGateTests::Engaging_Enemy_Fires — 교전 상태는 쏜다
        [Test]
        public void 교전_상태의_적은_쏜다()
        {
            var m = Match(Definition(defenderDamage: 0f, enemyDamage: 10f, enemyRange: 2f));
            var e = SpawnEnemy(m, new int2(2, 2));
            var d = SpawnDefender(m, new int2(3, 2));
            Tick(m, 1);
            Assert.AreEqual(AiState.Engaging, e.Ai.Enemy);
            Assert.IsTrue(Hurt(d));
        }

        // 옛 AttackSystemStateGateTests::Standoff_Enemy_Fires — 대치(물림 + 가디언 사거리 안)는 쏜다
        [Test]
        public void 대치_상태의_적은_쏜다()
        {
            var m = Match(Definition(defenderDamage: 0f, enemyDamage: 10f, enemyRange: 2f));
            var e = SpawnEnemy(m, new int2(2, 2));
            var g = SpawnDefender(m, new int2(3, 2));
            Tick(m, 1);
            e.Aggro = new Aggro { Target = g.Id };
            float before = g.Health;
            Tick(m, 61);
            Assert.AreEqual(AiState.Standoff, e.Ai.Enemy);
            Assert.Less(g.Health, before);
        }

        // 옛 AttackSystemStateGateTests::Chasing_Enemy_DoesNotFire — 추격(가디언 사거리 밖) 중엔 안 쏜다
        [Test]
        public void 추격_상태의_적은_옆에_대상이_있어도_안_쏜다()
        {
            var m = Match(Definition(defenderDamage: 0f, enemyDamage: 10f, enemyRange: 2f));
            var e = SpawnEnemy(m, new int2(2, 2));
            var neighbor = SpawnDefender(m, new int2(3, 2));
            var g = SpawnDefender(m, new int2(10, 2));
            Tick(m, 1);
            e.Aggro = new Aggro { Target = g.Id };
            float before = neighbor.Health;
            float cdBefore = e.Attack.CooldownRemaining;
            Tick(m, 61);
            Assert.AreEqual(AiState.Chasing, e.Ai.Enemy);
            Assert.AreEqual(before, neighbor.Health, 1e-4f, "추격 중엔 사거리 안 이웃도 안 쏜다");
            Assert.LessOrEqual(e.Attack.CooldownRemaining, cdBefore, "START 를 안 했으니 쿨이 다시 차지 않는다");
        }

        // 옛 AttackSystemStateGateTests::Marching_KeepsCooldownReady_FiresOnEngagingTransition — 행진 중엔 쿨이 준비 상태로 대기하고 교전 전이 즉시 쏜다
        [Test]
        public void 행진_중엔_쿨이_준비_상태로_대기하고_교전으로_바뀌는_틱에_쏜다()
        {
            // 옛 Marching_Enemy_DoesNotFire 도 여기 들어 있다(행진 틱에는 발사 0).
            var m = Match(Definition(defenderDamage: 0f, enemyDamage: 10f, enemyRange: 1f));
            var resolved = Listen(m, CoreEventKind.AttackResolved);
            var e = SpawnEnemy(m, new int2(2, 2));
            Tick(m, 30);
            Assert.AreEqual(AiState.Marching, e.Ai.Enemy);
            Assert.AreEqual(0f, e.Attack.CooldownRemaining, 1e-5f, "행진은 START 를 안 하므로 쿨을 리셋하지 않는다");
            foreach (var r in resolved) Assert.AreNotEqual(e.Id, r.A, "행진 중엔 쏘지 않는다");

            var d = SpawnDefender(m, new int2(3, 2));
            Tick(m, 1);
            Assert.AreEqual(AiState.Engaging, e.Ai.Enemy);
            Assert.IsTrue(Hurt(d), "교전 전이 틱에 즉발");
        }

        // ═════════════════════════════════════════════════════════════════════
        // 9. EnemyTargetPriority — 직업 우선순위
        // ═════════════════════════════════════════════════════════════════════

        private const int RangerRole = 1;     // `DefenderClass.Ranger` 의 정수 값(코어는 직업을 int 로만 본다)
        private const int GuardianRole = 2;   // `DefenderClass.Guardian`

        private static (BattleMatch m, Unit guardian, Unit ranger) PriorityDuel(int priorityClass)
        {
            var def = Definition(defenderDamage: 0f, enemyDamage: 10f, enemyRange: 8f);
            def.Enemies[0].Attack.PriorityClass = priorityClass;
            int guardianRow = AddPassiveDefender(def, GuardianRole);
            int rangerRow = AddPassiveDefender(def, RangerRole);
            Rehash(def);
            var m = Match(def);
            SpawnEnemy(m, new int2(2, 2));
            var guardian = SpawnDefender(m, new int2(3, 2), guardianRow);   // 가깝다
            var ranger = SpawnDefender(m, new int2(5, 2), rangerRow);       // 멀다
            return (m, guardian, ranger);
        }

        // 옛 EnemyTargetPriorityTests::Shooter_PrioritizesRanger_OverCloserGuardian — 우선 직업이 더 가까운 다른 직업을 이긴다
        [Test]
        public void 우선_직업이_사거리_안이면_더_가까운_다른_직업보다_먼저다()
        {
            var (m, guardian, ranger) = PriorityDuel(RangerRole);
            Tick(m, 1);
            Assert.IsTrue(Hurt(ranger), "우선 직업(레인저)을 때린다");
            Assert.IsFalse(Hurt(guardian), "더 가까운 비우선 대상은 건너뛴다");
        }

        // 옛 EnemyTargetPriorityTests::NoPriority_PicksNearest — 우선 직업이 없으면 최근접
        [Test]
        public void 우선_직업이_없으면_최근접이다()
        {
            var (m, guardian, ranger) = PriorityDuel(0);
            Tick(m, 1);
            Assert.IsTrue(Hurt(guardian), "최근접");
            Assert.IsFalse(Hurt(ranger));
        }

        // ═════════════════════════════════════════════════════════════════════
        // 10. ProjectileRetargetAndBounce — 재조준·튕김이 **탄 수명에** 붙어 있나
        // ═════════════════════════════════════════════════════════════════════

        private static MatchDefinition ProjectileBoard(MovementKind movement, PayloadKind payload,
                                                       float speed, float hitThreshold, int pierce = 1)
        {
            var def = Definition(defenderDamage: 0f, defenderRange: 0.5f, enemyHealth: 100f);
            var p = ProjectileDef.Default();
            p.Id = "port_projectile";
            p.Movement = (int)movement;
            p.Payload = (int)payload;
            p.Speed = speed;
            p.HitThreshold = hitThreshold;
            p.PierceCount = pierce;
            p.MinFlightTime = 0.05f;
            def.Projectiles = new[] { p };   // 방어유닛의 평타는 근접 그대로다 — 탄은 테스트가 직접 요청한다
            // 공중 적 한 줄(정의 인덱스 1) — 층 필터 대조용.
            var air = def.Enemies[0];
            air.Id = "port_air_enemy";
            air.TraversalLayers = LayerBits.Air;
            def.Enemies = new[] { def.Enemies[0], air };
            Rehash(def);
            return def;
        }

        private static ProjectileRequest Request(Unit owner, MovementKind movement, PayloadKind payload,
                                                 float3 origin, float damage)
        {
            var req = ProjectileRequest.Empty;
            req.DefIndex = 0;
            req.Movement = movement;
            req.Payload = payload;
            req.Owner = owner.Id;
            req.OwnerFaction = owner.Faction;
            req.TargetMask = (int)Faction.EnemyUnit;
            req.Origin = origin;
            req.Impact = origin;
            req.Damage = damage;
            return req;
        }

        private static Projectile ProjectileOf(BattleMatch m)
            => m.World.Projectiles.Count > 0 ? m.World.Projectiles[0] : null;

        private static void FireHoming(BattleMatch m, Unit owner, Unit target, int retargetTileRange,
                                       byte targetLayers = 0)
        {
            var req = Request(owner, MovementKind.HomingToEntity, PayloadKind.SingleSplash, Center(2, 2), 20f);
            req.Target = target.Id;
            req.Impact = target.Position;
            req.RetargetTileRange = retargetTileRange;
            req.TargetLayers = targetLayers;
            m.World.ProjectileRequests.Add(req);
        }

        // 옛 ProjectileRetargetAndBounceTests::Retarget_TargetDestroyed_RepicksNearbyEnemy — 대상 소멸 시 반경 안 남은 적으로 다시 겨눈다
        [Test]
        public void 재조준_탄은_대상이_사라지면_근처_적으로_다시_겨눈다()
        {
            var m = Match(ProjectileBoard(MovementKind.HomingToEntity, PayloadKind.SingleSplash, 1f, 0.2f));
            var owner = SpawnDefender(m, new int2(0, 4));
            var doomed = SpawnEnemy(m, new int2(5, 2));
            var other = SpawnEnemy(m, new int2(4, 3));
            FireHoming(m, owner, doomed, retargetTileRange: 4);
            Tick(m, 1);
            Assert.IsNotNull(ProjectileOf(m), "전제: 탄이 섰다");

            m.Apply(Command.DebugDestroy(doomed.Id));
            Tick(m, 1);
            var p = ProjectileOf(m);
            Assert.IsNotNull(p, "재조준 대상이 있으면 사라지면 안 된다");
            Assert.AreEqual(other.Id, p.Target, "반경 안의 남은 적으로 다시 겨눈다");
        }

        // 옛 ProjectileRetargetAndBounceTests::Retarget_TargetTaggedDead_RepicksBeforeDestruction — 죽었지만 아직 안 사라진 창도 재조준 트리거
        [Test]
        public void 재조준_탄은_대상이_죽은_틱에도_소멸_전에_갈아탄다()
        {
            var m = Match(ProjectileBoard(MovementKind.HomingToEntity, PayloadKind.SingleSplash, 1f, 0.2f));
            var owner = SpawnDefender(m, new int2(0, 4));
            var dying = SpawnEnemy(m, new int2(5, 2));
            var other = SpawnEnemy(m, new int2(4, 3));
            FireHoming(m, owner, dying, retargetTileRange: 4);
            dying.Inbox.Damage.Add(new DamageEntry { Amount = 1e9f, Source = SimEntityId.None });
            Tick(m, 1);   // 탄이 서고, 같은 틱 전투 단계에서 대상이 사망 표시된다
            Assert.IsTrue(dying.Dead, "전제: 사망 표시(소멸은 다음 틱)");

            Tick(m, 1);   // 탄 단계가 소멸보다 먼저 돈다 — 시체를 보고 갈아타야 한다
            var p = ProjectileOf(m);
            Assert.IsNotNull(p, "죽었지만 아직 안 사라진 창에 도착하면 탄이 증발하면 안 된다");
            Assert.AreEqual(other.Id, p.Target);
        }

        // 옛 ProjectileRetargetAndBounceTests::Retarget_NoCandidateInRange_Destroys — 반경 안 후보가 없으면 소멸
        [Test]
        public void 재조준_반경_안에_후보가_없으면_탄은_사라진다()
        {
            var m = Match(ProjectileBoard(MovementKind.HomingToEntity, PayloadKind.SingleSplash, 1f, 0.2f));
            var owner = SpawnDefender(m, new int2(0, 4));
            var doomed = SpawnEnemy(m, new int2(3, 2));
            SpawnEnemy(m, new int2(11, 0));   // 반경 밖
            FireHoming(m, owner, doomed, retargetTileRange: 2);
            Tick(m, 1);
            m.Apply(Command.DebugDestroy(doomed.Id));
            Tick(m, 2);
            Assert.IsNull(ProjectileOf(m), "반경 안에 후보가 없으면 기존대로 소멸");
        }

        // 옛 ProjectileRetargetAndBounceTests::Retarget_Disabled_KeepsLegacyDestroyBehaviour — 옵트인 안 한 탄은 재조준 안 한다
        [Test]
        public void 재조준을_안_켠_탄은_옆에_후보가_있어도_사라진다()
        {
            var m = Match(ProjectileBoard(MovementKind.HomingToEntity, PayloadKind.SingleSplash, 1f, 0.2f));
            var owner = SpawnDefender(m, new int2(0, 4));
            var doomed = SpawnEnemy(m, new int2(4, 2));
            SpawnEnemy(m, new int2(5, 2));
            FireHoming(m, owner, doomed, retargetTileRange: 0);
            Tick(m, 1);
            m.Apply(Command.DebugDestroy(doomed.Id));
            Tick(m, 2);
            Assert.IsNull(ProjectileOf(m), "opt-in 하지 않은 탄은 재조준하지 않는다");
        }

        // 옛 ProjectileRetargetAndBounceTests::PathOnly_Retarget_IgnoresCloserAirCandidate — 길 전용 재조준은 더 가까운 공중 적을 무시
        [Test]
        public void 길_전용_탄의_재조준은_더_가까운_공중_적을_무시한다()
        {
            var m = Match(ProjectileBoard(MovementKind.HomingToEntity, PayloadKind.SingleSplash, 1f, 0.2f));
            var owner = SpawnDefender(m, new int2(0, 4));
            var doomed = SpawnEnemy(m, new int2(5, 2));
            SpawnEnemy(m, new int2(3, 2), defIndex: 1);    // 공중 — 더 가깝다
            var path = SpawnEnemy(m, new int2(4, 3));
            FireHoming(m, owner, doomed, retargetTileRange: 4, targetLayers: LayerBits.Path);
            Tick(m, 1);
            m.Apply(Command.DebugDestroy(doomed.Id));
            Tick(m, 1);
            var p = ProjectileOf(m);
            Assert.IsNotNull(p);
            Assert.AreEqual(path.Id, p.Target);
        }

        private static void FireDirectional(BattleMatch m, Unit owner, float3 origin, int bounces,
                                            float bounceMul, byte targetLayers = 0)
        {
            var req = Request(owner, MovementKind.DirectionalLinear, PayloadKind.PathHit, origin, 20f);
            req.Direction = new float2(1f, 0f);
            req.DistanceOverride = 10f;
            req.BounceCount = bounces;
            req.BounceTileRange = 4;
            req.BounceDamageMul = bounceMul;
            req.TargetLayers = targetLayers;
            m.World.ProjectileRequests.Add(req);
        }

        // 옛 ProjectileRetargetAndBounceTests::DirectionalBounce_OnPierceSpent_SwitchesToHoming — 방향탄이 관통을 다 쓰면 호밍으로 튕긴다
        [Test]
        public void 방향탄은_관통을_다_쓰면_호밍으로_바꿔_다음_적에게_튕긴다()
        {
            var m = Match(ProjectileBoard(MovementKind.DirectionalLinear, PayloadKind.PathHit, 8f, 0.6f, pierce: 1));
            var owner = SpawnDefender(m, new int2(0, 4));
            var victim = SpawnEnemy(m, new int2(3, 2));
            var next = SpawnEnemy(m, new int2(4, 2));
            FireDirectional(m, owner, Center(2, 2), bounces: 2, bounceMul: 0.5f);
            for (int i = 0; i < 12 && !Hurt(victim); i++) Tick(m, 1);

            Assert.IsTrue(Hurt(victim), "첫 적은 정상 피격");
            var p = ProjectileOf(m);
            Assert.IsNotNull(p, "튕길 대상이 있으면 살아남는다");
            Assert.AreEqual(MovementKind.HomingToEntity, p.Movement, "방향 → 호밍 전환");
            Assert.AreEqual(PayloadKind.SingleSplash, p.Payload, "스윕 → 단일 착탄 전환");
            Assert.AreEqual(next.Id, p.Target, "맞힌 적이 아닌 다음 적");
            Assert.AreEqual(1, p.BounceRemaining, "홉 하나 소비");
            Assert.AreEqual(20f * 0.5f, p.Damage, 1e-3f, "감쇠");
        }

        // 옛 ProjectileRetargetAndBounceTests::DirectionalBounce_NoCandidate_Destroys — 튕길 곳이 없으면 관통 소진과 함께 소멸
        [Test]
        public void 방향탄은_관통을_다_쓰고_튕길_곳이_없으면_사라진다()
        {
            var m = Match(ProjectileBoard(MovementKind.DirectionalLinear, PayloadKind.PathHit, 8f, 0.6f, pierce: 1));
            var owner = SpawnDefender(m, new int2(0, 4));
            var victim = SpawnEnemy(m, new int2(3, 2));
            FireDirectional(m, owner, Center(2, 2), bounces: 2, bounceMul: 1f);
            for (int i = 0; i < 12 && !Hurt(victim); i++) Tick(m, 1);
            Assert.IsTrue(Hurt(victim), "전제: 한 명은 맞았다");
            Tick(m, 1);
            Assert.IsNull(ProjectileOf(m), "맞힐 적이 하나뿐이면 소멸");
        }

        // 옛 ProjectileRetargetAndBounceTests::PathOnly_DirectionalHit_SkipsAirAndHitsPath — 길 전용 방향탄은 공중을 지나쳐 길 적을 맞힌다
        [Test]
        public void 길_전용_방향탄은_공중_적을_지나쳐_길_적만_맞힌다()
        {
            var m = Match(ProjectileBoard(MovementKind.DirectionalLinear, PayloadKind.PathHit, 8f, 0.6f, pierce: 2));
            var owner = SpawnDefender(m, new int2(0, 4));
            var air = SpawnEnemy(m, new int2(3, 2), defIndex: 1);
            var path = SpawnEnemy(m, new int2(4, 2));
            FireDirectional(m, owner, Center(2, 2), bounces: 0, bounceMul: 1f, targetLayers: LayerBits.Path);
            Tick(m, 30);
            Assert.IsFalse(Hurt(air), "길 전용 탄은 공중을 안 맞힌다");
            Assert.AreEqual(path.MaxHealth - 20f, path.Health, 1e-3f, "길 적은 탄 피해 그대로");
        }

        // ═════════════════════════════════════════════════════════════════════
        // 11. DirectionalVolleyIntegration — 연발의 수명 규칙(에셋 없이 픽스처로)
        // ═════════════════════════════════════════════════════════════════════
        //
        // 옛 파일의 에셋 단언(샷건·머신건 SO 저작 모양·VFX 프리팹)은 **규칙이 아니라 저작**이라
        // 옮기지 않았다 — 저작 쪽 짝은 Assets lane(`CoreBuilderDriftTests`·`LiveDefinitionSmokeTests`)이다.

        private const int VolleyShots = 10;
        private const float VolleyInterval = 0.1f;
        private const float VolleyCooldown = 1.6f;

        private static MatchDefinition VolleyDef(float hitDelay = 0f, float interval = VolleyInterval,
                                                 int shots = VolleyShots, float cooldown = VolleyCooldown)
        {
            var def = Definition(defenderDamage: 5f, defenderRange: 4f, defenderCooldown: cooldown,
                                 defenderHitDelay: hitDelay, enemyHealth: 1e7f);
            GiveProjectile(def, MovementKind.DirectionalLinear, PayloadKind.PathHit, speed: 0.01f, maxDistance: 20f);
            var s = new PatternShotDef[shots];
            for (int i = 0; i < shots; i++)
                s[i] = new PatternShotDef { DirectionT = 0.5f, IntervalAfterPreviousSec = i == 0 ? 0f : interval };
            def.Patterns = new[]
            {
                new PatternDef
                {
                    Id = "port_volley",
                    BarrelProjectileDefIndex = 0,
                    Selection = (int)PatternSelectionRule.None,
                    MinAngleDeg = -30f,
                    MaxAngleDeg = 30f,
                    Shots = s,
                },
            };
            def.Units[0].Attack.PatternDefIndices = new[] { 0 };
            Rehash(def);
            return def;
        }

        private static List<int> SpawnTicks(List<CoreEvent> spawned, Unit owner)
        {
            var ticks = new List<int>();
            foreach (var e in spawned) if (e.B == owner.Id) ticks.Add(e.Tick);
            return ticks;
        }

        // 옛 DirectionalVolleyIntegrationTests::MachineGun_FiresTenAtPointOneSecondIntervals_AndDefersNextTrigger — 발 간격 · 다음 트리거는 연발이 끝난 뒤로 미뤄진다
        [Test]
        public void 연발_중엔_다음_트리거가_마지막_탄_뒤로_미뤄진다()
        {
            var m = Match(VolleyDef());
            var spawned = Listen(m, CoreEventKind.ProjectileSpawned);
            var d = SpawnDefender(m, new int2(2, 2));
            SpawnEnemy(m, new int2(5, 2));

            float burst = (VolleyShots - 1) * VolleyInterval;
            int horizon = (int)math.ceil((VolleyCooldown + burst) * 60f) + 20;
            for (int t = 0; t < horizon && SpawnTicks(spawned, d).Count < VolleyShots + 1; t++) Tick(m, 1);

            var ticks = SpawnTicks(spawned, d);
            Assert.AreEqual(VolleyShots + 1, ticks.Count, "첫 연발 10발과 다음 트리거의 첫 탄");
            int intervalTicks = (int)math.round(VolleyInterval * 60f);
            for (int i = 2; i < VolleyShots; i++)
                Assert.AreEqual(intervalTicks, ticks[i] - ticks[i - 1], 1, $"{i}번 탄 간격");
            float nextAfter = (ticks[VolleyShots] - ticks[0]) / 60f;
            Assert.AreEqual(VolleyCooldown + burst, nextAfter, 2f / 60f,
                "다음 트리거 = 기본 쿨 + 연발 길이 — 연발 중에 새 트리거를 받으면 안 된다");
        }

        // 옛 DirectionalVolleyIntegrationTests::ActiveSequence_CompletesAfterLaneEmptiesAndHostBecomesActionLocked — 시작된 연발은 대상 소실·행동 잠금과 무관하게 완주
        [Test]
        public void 시작된_연발은_대상이_사라지고_잠들어도_끝까지_나간다()
        {
            var m = Match(VolleyDef(cooldown: 10f));
            var spawned = Listen(m, CoreEventKind.ProjectileSpawned);
            var d = SpawnDefender(m, new int2(2, 2));
            var e = SpawnEnemy(m, new int2(5, 2));
            Tick(m, 1);   // 트리거 — 첫 발 요청
            Assert.IsTrue(d.Attack.PatternSlots[0].Active, "전제: 연발이 시작됐다");

            m.Apply(Command.DebugDestroy(e.Id));
            m.World.RequestCc(CcRequest.Of(d.Id, CcRequestKind.Sleep, 10f, SimEntityId.None));
            Tick(m, 2);
            Assert.IsTrue(d.ActionLocked, "전제: 사수가 행동 잠금 중이다");
            Tick(m, 120);

            Assert.AreEqual(VolleyShots, SpawnTicks(spawned, d).Count,
                "시작된 연발은 대상·군중 제어와 무관하게 전탄 완주");
            Assert.IsFalse(d.Attack.PatternSlots[0].Active, "완주 뒤 슬롯은 닫힌다");
        }

        // 옛 DirectionalVolleyIntegrationTests::StartedShotgun_FiresAfterTargetMovesOutOfRange / StartedShotgun_FiresAfterWitnessDies — 선딜 중 대상을 잃어도 START 방향으로 쏜다
        [TestCase(false, TestName = "선딜_중_대상이_사거리를_벗어나도_방향_연발은_START_방향으로_나간다")]
        [TestCase(true, TestName = "선딜_중_대상이_죽어도_방향_연발은_START_방향으로_나간다")]
        public void 선딜_중_대상을_잃어도_방향_연발은_나간다(bool kill)
        {
            const int shots = 3;
            const float hitDelay = 0.1f;
            var m = Match(VolleyDef(hitDelay: hitDelay, interval: 0f, shots: shots, cooldown: 10f));
            var spawned = Listen(m, CoreEventKind.ProjectileSpawned);
            var d = SpawnDefender(m, new int2(2, 1));
            var witness = SpawnEnemy(m, new int2(5, 2));   // 오른쪽 위 — 축이 +X 가 아니다
            float startAngle = math.degrees(math.atan2(witness.Position.z - d.Position.z,
                                                       witness.Position.x - d.Position.x));

            Tick(m, 1);   // START
            Assert.IsTrue(d.Attack.Swinging, "전제: 선딜 중");
            Assert.AreEqual(0, SpawnTicks(spawned, d).Count, "선딜 중엔 아직 안 쏜다");

            if (kill) witness.Inbox.Damage.Add(new DamageEntry { Amount = 1e9f, Source = SimEntityId.None });
            else witness.Position = Center(10, 4);
            Tick(m, (int)math.ceil(hitDelay * 60f) + 3);

            Assert.AreEqual(shots, SpawnTicks(spawned, d).Count,
                "START 가 성사된 방향 연발은 대상이 사라져도 쏜다");
            foreach (var p in m.World.Projectiles)
            {
                if (p.Owner != d.Id) continue;
                Assert.IsTrue(p.Target.IsNone, "방향탄은 임자가 없다");
                float angle = math.degrees(math.atan2(p.Direction.y, p.Direction.x));
                Assert.That(angle, Is.InRange(startAngle - 30f - 1e-3f, startAngle + 30f + 1e-3f),
                    "대상이 없어도 START 방향이 확산의 기준축이다");
            }
        }

        // 옛 DirectionalVolleyIntegrationTests::NonFacingTargetBoundProjectile_StillLapsesWhenTargetDiesDuringWindup — 대상 조준 탄은 여전히 빗나간다
        [Test]
        public void 선딜_중_대상이_죽으면_유도탄은_빗나간다()
        {
            var def = Definition(defenderDamage: 8f, defenderRange: 5f, defenderHitDelay: 0.1f, defenderCooldown: 10f);
            GiveProjectile(def, MovementKind.HomingToEntity, PayloadKind.SingleSplash);
            var m = Match(def);
            var spawned = Listen(m, CoreEventKind.ProjectileSpawned);
            var d = SpawnDefender(m, new int2(2, 1));
            var e = SpawnEnemy(m, new int2(3, 3));
            Tick(m, 1);
            e.Inbox.Damage.Add(new DamageEntry { Amount = 1e9f, Source = SimEntityId.None });
            Tick(m, 12);
            Assert.AreEqual(0, SpawnTicks(spawned, d).Count, "호밍·근접의 RESOLVE 재판정 계약은 방향탄 보정으로 안 바뀐다");
            Assert.IsFalse(d.Attack.Swinging);
        }

        // 옛 DirectionalVolleyIntegrationTests::PatternAttack_PushesOneInstancePerTrigger_BeforeEmitterConsumesIt — 트리거당 진행 인스턴스는 하나
        [Test]
        public void 트리거_한_번에_진행_인스턴스는_하나이고_발수만큼만_나간다()
        {
            var m = Match(VolleyDef(cooldown: 10f));
            var spawned = Listen(m, CoreEventKind.ProjectileSpawned);
            var d = SpawnDefender(m, new int2(2, 2));
            SpawnEnemy(m, new int2(5, 2));

            Tick(m, 1);
            Assert.AreEqual(1, m.World.ProjectileRequests.Count, "트리거 틱에는 첫 탄 요청 하나만");
            Assert.AreEqual(1, d.Attack.PatternSlots.Count);
            Assert.IsTrue(d.Attack.PatternSlots[0].Active, "같은 트리거의 단일 진행 인스턴스");

            Tick(m, (int)math.ceil((VolleyShots - 1) * VolleyInterval * 60f) + 5);
            Assert.AreEqual(VolleyShots, SpawnTicks(spawned, d).Count, "한 트리거 = 발수만큼(이중 발사 없음)");
            Assert.IsFalse(d.Attack.PatternSlots[0].Active);
        }
    }
}
