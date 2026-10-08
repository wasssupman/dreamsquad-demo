using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCore.Map;
using Somnia.Battle.BattleCore.Trigger;
using static Somnia.Battle.Tests.EditMode.Core.CoreTriggerFixtures;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // 하드 케이스 3 탐침(2026-09-28) — 「방어유닛이 보스(짱쎈)의 도약 스킬을 **소유**하면 발동하나」.
    //
    //   짱쎈 저작(`Enemy_Boss_Jjangssen.asset` nightmareMechanics · census 행 54-56):
    //     경계(0.9 · 0.5) × 순간이동(SelfBlink → BlinkToCluster) · 경계(0.8) × 궁극기 도약(UltimateLeap · fireCap 1)
    //     · 경계(0.2) × 자기 자리 폭발(SelfTileAoe). 슬램은 **자리에 떨어지는 것**(제약 13 — 착지 좌표 · 몸 0).
    //
    // 빌더 경로(정적 확인 · HEAD 줄): 방어유닛 능력은 `BindingDefinitionBuilder.cs:45`(`hostIsEnemy: false`) 로 적 악몽
    // (`:57`)과 **같은 `Bake`** 를 지난다. 조합 검증 `EffectComboRule.Check`(`EffectComboRule.cs:49-67`)는 경계 트리거에
    // 진영을 안 묻고(`SkillRouting.HasDetector` — 경계는 양쪽 다 감지자 있음), payload 가드 `BindPayload`
    // (`BindingDefinitionBuilder.cs:338-353`)도 숙주를 모른다(UltimateLeap = 탄 필수 + fireCap 1 · SelfBlink = 탄 선택).
    // 그래서 아래 정의표는 그 bake 가 방어유닛 줄에 굽는 모양을 코어 API 로 손조립한 것이다.
    //
    // ⚠ 초록 = 「현 구조가 그 문장을 만족한다」. `[Ignore]` = 「현 구조로는 그 문장이 성립하지 않는다」 — 사유에 막힌 지점
    // (파일:줄)을 적었다. 「사용자 결정 필요」가 붙은 것은 **규칙의 성질**이 정해지지 않아 구현이 답을 못 고르는 자리다.
    // `현행_` 으로 시작하는 테스트는 **지금의 동작을 박제한 증언**이다 — 코어가 고쳐지면 빨개지는 것이 정상이고,
    // 그때 짝이 되는 `[Ignore]` 를 풀면 된다.
    //
    // ⚠ 여기 수치는 게임 값이 아니라 픽스처다(짱쎈 저작 값을 **모양만** 따른다).
    [TestFixture]
    public class HardCaseBossSkillOnDefenderProbeTests
    {
        private const float SlamDamage = 50f;
        private const float UltSlamDamage = 100f;
        private const int UltTelegraphTicks = 120;   // Duration 2초

        // 판 12×5(`CoreMatchFixtures.Board`) · 골 (11,2) · 웨이브 스폰 x=0(웨이브 적은 속도 0 이라 거기 선다).
        private static readonly int2 HomeCell = new int2(3, 2);                  // 방어유닛 D 의 배치 칸
        private static readonly int2[] Cluster = { new int2(8, 1), new int2(8, 2), new int2(9, 2) };
        private static readonly int2 Densest = new int2(8, 1);                   // 동률 → row-major 최소 키(`LandingMath.TryDensestCell`)
        private static readonly int2 LoneCell = new int2(3, 3);                  // D 옛 자리 옆 외톨이 적
        private static readonly int2 FarCell = new int2(5, 4);                   // 어느 슬램에도 안 드는 적

        // ── 정의표 ──────────────────────────────────────────────────────────

        private static MatchDefinition Definition(out int slamDef)
        {
            var def = CoreMatchFixtures.Definition();
            def.Units[0].Cost = 0;
            def.Units[0].MaxOnBoard = 8;
            def.Units[0].DeathCooldown = 0f;
            for (int i = 0; i < def.Enemies.Length; i++)
            {
                def.Enemies[i].MoveSpeed = 0f;
                def.Enemies[i].Health = 1000f;
            }
            slamDef = AddBlastProjectile(def);
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        /// <summary>짱쎈 행 55 — 경계 × 순간이동(착지 슬램 50 · 반경 1). 저작은 magnitude = 밀집 탐색 반경 · tileRange = 착지 링.</summary>
        private static RuleRow BlinkRow(int slamDef, float fraction = 0.5f)
        {
            var r = Rule(TriggerKind.HealthThreshold, EffectKind.SelfBlink);
            r.Rule.Label = "짱쎈 순간이동(방어유닛 소유)";
            r.Rule.Fraction = fraction;
            r.Effect.Magnitude = 2f;
            r.Effect.TileRange = 6;
            r.Effect.Damage = SlamDamage;
            r.Effect.SlamTileRange = 1;
            r.Effect.DataIndex = slamDef;
            r.Rule.Origin = BindingOrigin.UnitAuthored;
            return r;
        }

        /// <summary>짱쎈 행 56 — 경계 × 궁극기 도약(예고 2초 · 슬램 100 · 반경 2 · fireCap 1 = 빌더가 굽는 값).</summary>
        private static RuleRow UltRow(int slamDef, float fraction = 0.8f)
        {
            var r = Rule(TriggerKind.HealthThreshold, EffectKind.UltimateLeap);
            r.Rule.Label = "짱쎈 궁극기 도약(방어유닛 소유)";
            r.Rule.Fraction = fraction;
            r.Effect.Magnitude = 2f;
            r.Effect.TileRange = 6;
            r.Effect.Duration = 2f;
            r.Effect.Damage = UltSlamDamage;
            r.Effect.SlamTileRange = 2;
            r.Effect.DataIndex = slamDef;
            r.Rule.FireCap = 1;
            r.Rule.Origin = BindingOrigin.UnitAuthored;
            return r;
        }

        /// <summary>짱쎈 행 54 — 경계 × 자기 자리 폭발.</summary>
        private static RuleRow QuakeRow(int slamDef)
        {
            var r = Rule(TriggerKind.HealthThreshold, EffectKind.SelfTileAoe);
            r.Rule.Label = "짱쎈 지진(방어유닛 소유)";
            r.Rule.Fraction = 0.2f;
            r.Effect.Magnitude = 60f;
            r.Effect.TileRange = 2;
            r.Effect.DataIndex = slamDef;
            r.Rule.Origin = BindingOrigin.UnitAuthored;
            return r;
        }

        // ── 판 ──────────────────────────────────────────────────────────────

        private sealed class Obs
        {
            public BattleMatch M;
            public Unit D;
            public SimEntityId DId;
            public List<Unit> ClusterUnits = new List<Unit>();
            public Unit Lone;
            public Unit Far;
            public List<CoreEvent> Ascend, Descend, Blinked, Spawned, Hits;

            public float DamageTo(SimEntityId id)
            {
                float s = 0f;
                foreach (var h in Hits) if (h.B == id) s += h.Amount;
                return s;
            }

            public float DamageFrom(SimEntityId src, SimEntityId victim)
            {
                float s = 0f;
                foreach (var h in Hits) if (h.A == src && h.B == victim) s += h.Amount;
                return s;
            }
        }

        private static Unit Place(BattleMatch m, int2 cell)
        {
            Assert.AreEqual(RejectReason.None, m.Apply(Command.PlaceDefender(0, cell)).Reason, $"배치 {cell}");
            var units = m.World.Units;
            return units[units.Count - 1];
        }

        private static void Hurt(Unit u, float amount)
            => u.Inbox.Damage.Add(new DamageEntry { Amount = amount, Source = SimEntityId.None });

        private static int2 CellOf(Obs o, Unit u) => o.M.Map.CellOf(u.Position);

        /// <summary>D 를 놓고 적을 세운다. 규칙은 `def` 에 이미 들어 있다(`GiveUnit`).</summary>
        private static Obs Arena(MatchDefinition def, bool lone = false, System.Action<BattleMatch> beforePlace = null)
        {
            var o = new Obs { M = CoreMatchFixtures.BeginBattle(def) };
            foreach (var c in Cluster) o.ClusterUnits.Add(SpawnEnemy(o.M, c));
            if (lone) o.Lone = SpawnEnemy(o.M, LoneCell);
            o.Far = SpawnEnemy(o.M, FarCell);
            beforePlace?.Invoke(o.M);
            o.Ascend = CoreCombatFixtures.Listen(o.M, CoreEventKind.LeapAscend);
            o.Descend = CoreCombatFixtures.Listen(o.M, CoreEventKind.LeapDescend);
            o.Blinked = CoreCombatFixtures.Listen(o.M, CoreEventKind.Blinked);
            o.Spawned = CoreCombatFixtures.Listen(o.M, CoreEventKind.ProjectileSpawned);
            o.Hits = CoreCombatFixtures.Listen(o.M, CoreEventKind.DamageApplied);
            o.D = Place(o.M, HomeCell);
            o.DId = o.D.Id;
            CoreCombatFixtures.Tick(o.M, 5);   // 배치 모션 0 — 활성화까지
            return o;
        }

        private static Obs BlinkArena(bool lone = false, System.Action<MatchDefinition> tweak = null,
                                      System.Action<BattleMatch> beforePlace = null)
        {
            var def = Definition(out int slam);
            tweak?.Invoke(def);
            GiveUnit(def, 0, BlinkRow(slam));
            var o = Arena(def, lone, beforePlace);
            Hurt(o.D, 260f);                       // 500 → 240 — 경계 0.5 를 넘는다
            CoreCombatFixtures.Tick(o.M, 2);
            return o;
        }

        private static int FlightTicks(BattleMatch m)
            => MatchClock.TicksOf(m.Definition.Movement.BossLeapFlightSeconds, BattleMatch.Dt);

        // ── 1. 규칙이 굽히고 트리거가 울린다 ──────────────────────────────────

        [Test]
        public void 방어유닛_숙주도_짱쎈_세_조합이_조합_검증을_통과하고_라우팅이_있다()
        {
            // 빌더가 방어유닛 능력에 부르는 그 검증(`hostIsEnemy: false`).
            foreach (var payload in new[] { EffectKind.SelfBlink, EffectKind.UltimateLeap, EffectKind.SelfTileAoe })
            {
                var c = new EffectCombo { Trigger = TriggerKind.HealthThreshold, Payload = payload, HostIsEnemy = false };
                Assert.AreEqual(ComboVerdict.Allowed, EffectComboRule.Check(in c), $"{payload} × 방어유닛");
                Assert.IsNotNull(SkillRouting.Resolve(TriggerKind.HealthThreshold, payload), $"{payload} 라우팅");
            }

            var def = Definition(out int slam);
            GiveUnit(def, 0, BlinkRow(slam, 0.9f), BlinkRow(slam, 0.5f), UltRow(slam), QuakeRow(slam));
            Assert.AreEqual(4, def.Units[0].Bindings.Length, "짱쎈 네 줄이 방어유닛 줄에 선다");
            var m = CoreMatchFixtures.BeginBattle(def);
            var d = Place(m, HomeCell);
            CoreCombatFixtures.Tick(m, 2);
            Assert.AreEqual(4, d.Bindings.Count, "배치된 방어유닛이 네 규칙을 든다");
        }

        [Test]
        public void 방어유닛이_도약_능력을_가지면_체력_경계를_넘을_때_발동한다()
        {
            var o = BlinkArena();
            Assert.AreEqual(1, o.Ascend.Count, "경계에서 도약");
            Assert.AreEqual(o.DId, o.Ascend[0].A, "도약한 것은 D");
            Assert.AreEqual(0, o.Ascend[0].Arg, "일반 도약");
        }

        // ── 2. 순간이동 ─────────────────────────────────────────────────────

        [Test]
        public void 순간이동_방어유닛이_적이_몰린_곳으로_실제로_자리를_옮긴다()
        {
            var o = BlinkArena();
            var landing = o.M.Map.CenterOf(Densest);
            Assert.AreEqual(landing.x, o.D.Position.x, 1e-4f, "코어 위치가 옮겨졌다(뷰만 아치로 난다)");
            Assert.AreEqual(landing.z, o.D.Position.z, 1e-4f);
            Assert.AreEqual(landing.x, o.Ascend[0].SiteTarget.Pos.x, 1e-4f, "사건의 착지점 = 적 밀집 칸");
        }

        // ── 3. 착지 슬램(자리형) — 적에게만 ────────────────────────────────

        [Test]
        public void 일반_도약_착지_슬램은_착지_좌표_기준_반경_안_적에게만_몸_0()
        {
            SimEntityId ally = SimEntityId.None;
            var o = BlinkArena(beforePlace: m => ally = Place(m, new int2(9, 1)).Id);   // 착지 칸 옆 아군
            CoreCombatFixtures.Tick(o.M, FlightTicks(o.M) + 5);

            var mine = o.Spawned.FindAll(e => e.B == o.DId);
            Assert.AreEqual(1, mine.Count, "창 끝에 슬램 한 발(귀속 D)");
            Assert.AreEqual(0f, mine[0].SiteFired.OriginBody, 1e-6f, "자리에 떨어지는 것 — 몸 0(제약 13)");
            Assert.AreEqual(o.M.Map.CenterOf(Densest).x, mine[0].SiteTarget.Pos.x, 1e-4f, "착지 좌표에");
            foreach (var e in o.ClusterUnits) Assert.AreEqual(SlamDamage, o.DamageTo(e.Id), 1e-3f, $"반경 안 적 {e.Id}");
            Assert.AreEqual(0f, o.DamageTo(o.Far.Id), "반경 밖 적 무피해");
            Assert.AreEqual(0f, o.DamageTo(ally), "반경 안 아군 방어유닛 무피해 — 시전자 상대 진영만");
        }

        [Test]
        public void 궁극기_도약_방어유닛이_이탈_무적_예고_뒤_착지하고_반경_안_적에게만_슬램()
        {
            var def = Definition(out int slam);
            GiveUnit(def, 0, UltRow(slam));
            SimEntityId ally = SimEntityId.None;
            var o = Arena(def, beforePlace: m => ally = Place(m, new int2(9, 1)).Id);
            Hurt(o.D, 410f);   // 500 → 90 — 경계 0.2
            CoreCombatFixtures.Tick(o.M, 2);

            Assert.AreEqual(1, o.Ascend.Count, "이탈");
            Assert.AreEqual(1, o.Ascend[0].Arg, "궁극기");
            Assert.IsFalse(o.D.IsTargetable(), "이탈 중 무적");
            Assert.IsTrue(o.D.ActionLocked, "이탈 중 공격 불가");

            CoreCombatFixtures.Tick(o.M, UltTelegraphTicks + 5);
            Assert.AreEqual(1, o.Descend.Count, "강하");
            var landing = o.M.Map.CenterOf(Densest);
            Assert.AreEqual(landing.x, o.D.Position.x, 1e-4f, "착지 칸에 섰다");
            Assert.IsTrue(o.D.IsTargetable(), "착지 뒤 무적 해제");

            var mine = o.Spawned.FindAll(e => e.B == o.DId);
            Assert.AreEqual(1, mine.Count, "슬램 한 발");
            Assert.AreEqual(0f, mine[0].SiteFired.OriginBody, 1e-6f, "자리형 — 몸 0");
            foreach (var e in o.ClusterUnits) Assert.AreEqual(UltSlamDamage, o.DamageTo(e.Id), 1e-3f, $"반경 안 적 {e.Id}");
            Assert.AreEqual(0f, o.DamageTo(ally), "아군 무피해");
        }

        // ── 4. 새 자리에서 공격·피격 ─────────────────────────────────────────

        private static void ArmBoth(MatchDefinition def)
        {
            def.Units[0].AttackRange = 1f;
            def.Units[0].Attack.Mode = (int)TargetMode.Nearest;
            def.Units[0].Attack.Outputs = new[] { new AttackOutputDef { Kind = AttackOutputKind.Damage, Magnitude = 10f } };
            for (int i = 0; i < def.Enemies.Length; i++)
            {
                def.Enemies[i].Attack.Mode = (int)TargetMode.Nearest;
                def.Enemies[i].Attack.Outputs = new[] { new AttackOutputDef { Kind = AttackOutputKind.Damage, Magnitude = 1f } };
            }
        }

        [Test]
        public void 옮긴_뒤_방어유닛의_사거리_원점은_새_자리다()
        {
            var o = BlinkArena(lone: true, tweak: ArmBoth);
            int hitsBefore = o.Hits.Count;
            float loneBefore = o.DamageFrom(o.DId, o.Lone.Id);
            CoreCombatFixtures.Tick(o.M, FlightTicks(o.M) + 180);

            Assert.AreEqual(loneBefore, o.DamageFrom(o.DId, o.Lone.Id), 1e-3f, "옛 자리 옆 적은 더 안 맞는다");
            float clusterByD = 0f;
            for (int i = hitsBefore; i < o.Hits.Count; i++)
                if (o.Hits[i].A == o.DId && o.Hits[i].Amount < SlamDamage - 1e-3f && o.ClusterUnits.Exists(e => e.Id == o.Hits[i].B))
                    clusterByD += o.Hits[i].Amount;
            Assert.Greater(clusterByD, 0f, "새 자리 옆 적을 평타로 친다");
        }

        [Test]
        public void 옮긴_뒤_방어유닛은_새_자리에서_맞는다()
        {
            var o = BlinkArena(lone: true, tweak: ArmBoth);
            int from = o.Hits.Count;
            CoreCombatFixtures.Tick(o.M, FlightTicks(o.M) + 180);
            float byLone = 0f, byCluster = 0f;
            for (int i = from; i < o.Hits.Count; i++)
            {
                if (o.Hits[i].B != o.DId) continue;
                if (o.Hits[i].A == o.Lone.Id) byLone += o.Hits[i].Amount;
                else if (o.ClusterUnits.Exists(e => e.Id == o.Hits[i].A)) byCluster += o.Hits[i].Amount;
            }
            Assert.AreEqual(0f, byLone, "옛 자리 옆 적은 더 못 때린다");
            Assert.Greater(byCluster, 0f, "새 자리 옆 적이 때린다");
        }

        // ── 5. 배치 격자(점유 · 장애물 · 퇴근) ──────────────────────────────

        [Test]
        public void 현행_옮긴_뒤에도_점유와_장애물은_옛_칸에_남고_새_칸은_비어_있다()
        {
            var o = BlinkArena();
            var occ = o.M.Map.Occupancy;
            Assert.AreEqual(Densest, CellOf(o, o.D), "몸은 새 칸");
            Assert.AreEqual(HomeCell, o.D.Footprint.Anchor, "점유 앵커는 옛 칸 그대로");
            Assert.AreEqual(o.DId.Value, occ.OwnerAt(HomeCell), "옛 칸을 여전히 문다");
            Assert.AreEqual(SimEntityId.NoneValue, occ.OwnerAt(Densest), "새 칸은 아무도 안 문다");
            var snap = o.M.Map.Snapshot;
            Assert.IsTrue(o.M.Map.Obstacles.Blocked[snap.Index(HomeCell)], "빈 옛 칸이 적 길을 막는다");
            Assert.IsFalse(o.M.Map.Obstacles.Blocked[snap.Index(Densest)], "D 가 선 새 칸은 적이 지나간다");
        }

        [Test]
        [Ignore("성립하지 않는다 — 순간이동은 좌표만 쓴다: `IntentApplier.cs:224-230`(방어유닛은 `Move` 가 없어 `u.Position` 직접 대입) · "
              + "궁극기 착지 `CombatPhase.cs:1515-1517` 도 같다. 점유(`PlacementOccupancy`)는 스폰 `PlacementService.cs:578` 과 해제 "
              + "`:514` · `CombatPhase.cs:1467` 에서만 바뀌고 `Footprint.Anchor` 를 옮기는 담당자가 없다(장애물 `FieldPrepPhase.cs:385-387` 이 그 앵커를 읽는다). "
              + "사용자 결정 필요: 다칸(2×2 · 2×3) footprint 는 착지 칸 하나(`CoreSkillContext.cs:224-230`)에 어떻게 앉나.")]
        public void 옮긴_뒤_옛_칸_점유가_풀리고_새_칸을_점유한다()
        {
            var o = BlinkArena();
            var occ = o.M.Map.Occupancy;
            Assert.AreEqual(SimEntityId.NoneValue, occ.OwnerAt(HomeCell), "옛 칸 해제");
            Assert.AreEqual(o.DId.Value, occ.OwnerAt(Densest), "새 칸 점유");
            Assert.AreEqual(Densest, o.D.Footprint.Anchor);
        }

        [Test]
        public void 현행_옮긴_방어유닛의_새_자리에_다른_방어유닛을_겹쳐_놓을_수_있고_빈_옛_칸엔_못_놓는다()
        {
            var o = BlinkArena();
            Assert.AreEqual(RejectReason.Occupied, o.M.Placement.Judge(0, HomeCell), "빈 옛 칸 = 점유 중으로 거절");
            var second = Place(o.M, Densest);
            Assert.AreEqual(o.D.Position.x, second.Position.x, 1e-4f, "같은 자리에 두 방어유닛 — 겹침");
            Assert.AreEqual(o.D.Position.z, second.Position.z, 1e-4f);
        }

        [Test]
        public void 다른_방어유닛이_선_칸에는_착지하지_않는다()
        {
            // 방어유닛 footprint 는 흐름장 장애물(`FieldPrepPhase.cs:385-387`) — 착지 탐색(`LandingMath.TryLandingCell`)이
            // 닿지 않는 칸으로 본다. ⚠ 이 방어는 **배치 때 점유한 칸**만 된다(위 `현행_` — 옮긴 유닛의 새 칸은 장애물이 아니다).
            SimEntityId blocker = SimEntityId.None;
            var o = BlinkArena(beforePlace: m => blocker = Place(m, Densest).Id);
            var landed = CellOf(o, o.D);
            Assert.AreNotEqual(Densest, landed, "밀집 칸엔 아군이 서 있다");
            Assert.AreEqual(SimEntityId.NoneValue, o.M.Map.Occupancy.OwnerAt(landed), "착지 칸은 아무도 안 문 칸");
            Assert.AreEqual(1, math.max(math.abs(landed.x - Densest.x), math.abs(landed.y - Densest.y)), "밀집 칸 옆 고리 1");
        }

        private static void RoadNotPlaceable(MatchDefinition def)
        {
            // 라이브 맵 모양 — 적 길 칸은 지상 방어유닛을 안 받는다. D 의 배치 칸만 열어 둔다.
            def.Units[0].PlacementLayers = LayerBits.Ground;
            var map = def.Map;
            for (int i = 0; i < map.PlaceMask.Length; i++) map.PlaceMask[i] = LayerBits.Path;
            map.PlaceMask[map.Index(HomeCell)] = LayerBits.CellBits;
            map.PlaceMask[map.Index(new int2(3, 0))] = LayerBits.CellBits;
        }

        [Test]
        public void 현행_착지_칸은_적_길_흐름장만_보고_배치_가능_여부를_안_본다()
        {
            var o = BlinkArena(tweak: RoadNotPlaceable);
            var landed = CellOf(o, o.D);
            Assert.AreEqual(Densest, landed, "적 밀집 칸(길)에 내렸다");
            Assert.IsFalse(o.M.Map.Snapshot.PlaceableAt(landed, LayerBits.Ground), "그 칸은 D 를 배치할 수 없는 칸");
            Assert.AreEqual(RejectReason.NotBuildable, o.M.Placement.Judge(0, landed));
        }

        [Test]
        [Ignore("시기상조 — 사용자 결정 2026-09-28(방어유닛 자리 이동은 범위 밖). 방어유닛이 «배치할 수 없는 칸»(적 길)에 착지해도 되나. 현행 착지 탐색은 **적 길 흐름장**"
              + "(`CoreSkillContext.cs:224-230` · `TraversalSlots.DefaultMask = Path` `TraversalSlots.cs:16`)만 보고 배치 마스크를 안 본다 — "
              + "그래서 지상 방어유닛의 배치 구역(`MapTile.Place` = 경로 층 없음)에는 **절대** 안 내리고 언제나 길에 내린다.")]
        public void 방어유닛은_배치_불가_칸에_착지하지_않는다()
        {
            var o = BlinkArena(tweak: RoadNotPlaceable);
            Assert.IsTrue(o.M.Map.Snapshot.PlaceableAt(CellOf(o, o.D), LayerBits.Ground));
        }

        [Test]
        public void 옮긴_뒤_퇴근하면_점유가_새지_않는다()
        {
            var o = BlinkArena();
            Assert.AreEqual(RejectReason.None, o.M.Apply(Command.Retire(o.DId)).Reason, "퇴근");
            Assert.AreEqual(0, o.M.Map.Occupancy.OwnerCount, "주인 기준 해제 — 물고 있던 옛 칸이 풀린다");
            Assert.AreEqual(RejectReason.None, o.M.Placement.Judge(0, HomeCell), "옛 칸에 다시 놓을 수 있다");
        }

        private static Obs RetireProbe(out List<float3> sites)
        {
            var got = new List<float3>();
            var probe = new ProbeSkill { OnExecute = (c, t, p, ctx) => got.Add(t.Origin.EffectSite) };
            var o = BlinkArena(tweak: def => GiveUnit(def, 0, Probe(TriggerKind.OnRetire, probe)));
            Assert.AreEqual(RejectReason.None, o.M.Apply(Command.Retire(o.DId)).Reason, "퇴근");
            CoreCombatFixtures.Tick(o.M, 2);
            sites = got;
            return o;
        }

        [Test]
        public void 현행_옮긴_뒤_퇴근_자리_효과는_옛_칸에서_난다()
        {
            var o = RetireProbe(out var sites);
            Assert.AreEqual(1, sites.Count, "퇴근 규칙 발화");
            Assert.AreEqual(o.M.Map.CenterOf(HomeCell).x, sites[0].x, 1e-4f, "자리 = 점유 앵커(옛 칸) — 선 자리가 아니다");
        }

        [Test]
        [Ignore("성립하지 않는다 — 퇴근 자리는 점유 앵커에서 읽는다(`PlacementService.cs:510-511` · 퇴직 위로금 `TriggerDispatcher.cs:207`→"
              + "`GimmickBindings.cs:205-210`). 앵커를 옮기는 담당자가 없어 옮긴 유닛의 퇴근 운석·위로금이 **빈 옛 칸**에 난다(위 점유 구멍과 같은 뿌리).")]
        public void 옮긴_뒤_퇴근_자리_효과는_선_자리에서_난다()
        {
            var o = RetireProbe(out var sites);
            Assert.AreEqual(o.M.Map.CenterOf(Densest).x, sites[0].x, 1e-4f);
        }

        // ── 6. 진영 뒤집기 ─────────────────────────────────────────────────

        [Test]
        public void 같은_궁극기를_적이_쓰면_방어유닛만_방어유닛이_쓰면_적만_맞는다()
        {
            // 적(보스)이 쓸 때 — 방어유닛 밀집 쪽으로 뛰고 방어유닛만 맞는다.
            var def = Definition(out int slam);
            GiveEnemy(def, 0, UltRow(slam));
            var m = CoreMatchFixtures.BeginBattle(def);
            var hits = CoreCombatFixtures.Listen(m, CoreEventKind.DamageApplied);
            var defenders = new List<SimEntityId>();
            foreach (var c in Cluster) defenders.Add(Place(m, c).Id);
            var boss = SpawnEnemy(m, HomeCell);
            var bystander = SpawnEnemy(m, new int2(9, 3));   // 방어유닛 밀집 옆 적
            SimEntityId bossId = boss.Id, bystanderId = bystander.Id;
            CoreCombatFixtures.Tick(m, 5);
            Hurt(boss, 810f);   // 1000 → 190 — 경계 0.2
            CoreCombatFixtures.Tick(m, UltTelegraphTicks + 10);
            float toDefenders = 0f, toEnemies = 0f;
            foreach (var h in hits)
            {
                if (h.A != bossId) continue;
                if (defenders.Contains(h.B)) toDefenders += h.Amount;
                if (h.B == bystanderId) toEnemies += h.Amount;
            }
            Assert.Greater(toDefenders, 0f, "적 시전 → 방어유닛이 맞는다");
            Assert.AreEqual(0f, toEnemies, "적 시전 → 옆 적은 무피해");

            // 방어유닛이 쓸 때 — 적 밀집 쪽으로 뛰고 적만 맞는다(위 궁극기 테스트의 아군 무피해와 짝).
            var def2 = Definition(out int slam2);
            GiveUnit(def2, 0, UltRow(slam2));
            SimEntityId ally = SimEntityId.None;
            var o = Arena(def2, beforePlace: mm => ally = Place(mm, new int2(9, 1)).Id);
            Hurt(o.D, 410f);
            CoreCombatFixtures.Tick(o.M, UltTelegraphTicks + 10);
            Assert.Greater(o.DamageFrom(o.DId, o.ClusterUnits[0].Id), 0f, "방어유닛 시전 → 적이 맞는다");
            Assert.AreEqual(0f, o.DamageTo(ally), "방어유닛 시전 → 아군 무피해");
        }

        // ── 7. 뷰가 따라가나(코어 사건 쪽) ──────────────────────────────────

        [Test]
        public void 방어유닛_도약도_뷰가_듣는_도약_사건을_낸다()
        {
            // 뷰 쪽(정적): `CoreLeapPresenter.cs:76-96` 은 사건 종류만 보고 진영을 안 거른다 · `CoreUnitViewPool.cs:396-402` 는
            // 도약 덮어쓰기가 없으면 `BattleDriver.TryGetRenderPosition`(`BattleDriver.cs:227-235` = `u.Position`)을 따른다 — 방어유닛도.
            var o = BlinkArena();
            CoreCombatFixtures.Tick(o.M, FlightTicks(o.M) + 5);
            Assert.AreEqual(1, o.Ascend.Count, "LeapAscend — 아치 연출 입력");
            Assert.AreEqual(1, o.Descend.FindAll(e => e.A == o.DId).Count, "LeapDescend(일반) — 착지 눌림");
        }

        [Test]
        public void 현행_방어유닛_순간이동은_Blinked_사건을_안_낸다_적은_낸다()
        {
            // `Blinked` 는 이동 단계(`AiMovePhase.cs:532-537`)만 낸다 — `Move` 가 없는 방어유닛은 `IntentApplier.cs:229` 에서
            // 좌표만 바뀐다. 뷰 소비자는 없지만 트레이스(`CoreHarness.cs:56`)·효과 증언(`EffectWitness.cs:211`)이 순간이동을 못 본다.
            var o = BlinkArena();
            Assert.AreEqual(0, o.Blinked.FindAll(e => e.A == o.DId).Count, "방어유닛 — Blinked 0");

            var def = Definition(out int slam);
            GiveEnemy(def, 0, BlinkRow(slam));
            var m = CoreMatchFixtures.BeginBattle(def);
            var blinked = CoreCombatFixtures.Listen(m, CoreEventKind.Blinked);
            foreach (var c in Cluster) Place(m, c);
            var boss = SpawnEnemy(m, HomeCell);
            SimEntityId bossId = boss.Id;
            CoreCombatFixtures.Tick(m, 5);
            Hurt(boss, 510f);
            CoreCombatFixtures.Tick(m, 3);
            Assert.AreEqual(1, blinked.FindAll(e => e.A == bossId).Count, "적 — Blinked 1");
        }
    }
}
