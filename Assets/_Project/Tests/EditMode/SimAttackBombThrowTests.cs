using System.Collections.Generic;
using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Combat;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Tests.EditMode
{
    /// <summary>
    /// battle-sim-extraction unit 18-I/2 arm C/1 — 착지 셀 산출. 구 `BombLandingTests` 어서션 복제.
    /// </summary>
    public class SimBombLandingTests
    {
        private static readonly SimInt2 Grid = new SimInt2(20, 10);

        [Test]
        public void East_OffsetsX_Positive()
        {
            BombLanding.ResolveCell(new SimInt2(5, 5), new SimInt2(1, 0), 3, Grid, out var cell, out var valid);
            Assert.AreEqual(new SimInt2(8, 5), cell);
            Assert.IsTrue(valid);
        }

        [Test]
        public void West_OffsetsX_Negative()
        {
            BombLanding.ResolveCell(new SimInt2(5, 5), new SimInt2(-1, 0), 3, Grid, out var cell, out var valid);
            Assert.AreEqual(new SimInt2(2, 5), cell);
            Assert.IsTrue(valid);
        }

        [Test]
        public void North_OffsetsY_Positive()
        {
            BombLanding.ResolveCell(new SimInt2(5, 5), new SimInt2(0, 1), 2, Grid, out var cell, out var valid);
            Assert.AreEqual(new SimInt2(5, 7), cell);
            Assert.IsTrue(valid);
        }

        [Test]
        public void South_OffsetsY_Negative()
        {
            BombLanding.ResolveCell(new SimInt2(5, 5), new SimInt2(0, -1), 2, Grid, out var cell, out var valid);
            Assert.AreEqual(new SimInt2(5, 3), cell);
            Assert.IsTrue(valid);
        }

        [Test]
        public void OffGrid_EastPastRightEdge_Invalid()
        {
            BombLanding.ResolveCell(new SimInt2(18, 5), new SimInt2(1, 0), 3, Grid, out var cell, out var valid);
            Assert.AreEqual(new SimInt2(21, 5), cell,
                "⚠ clamp 하지 않는다 — 셀은 격자 밖 그대로 나가고 valid 가 거절을 나른다");
            Assert.IsFalse(valid, "x=21 >= gridSize.x=20 → off-grid");
        }

        [Test]
        public void OffGrid_SouthPastBottom_Invalid()
        {
            BombLanding.ResolveCell(new SimInt2(5, 1), new SimInt2(0, -1), 3, Grid, out _, out var valid);
            Assert.IsFalse(valid, "y=-2 < 0 → off-grid");
        }

        [Test]
        public void Edge_LandsExactlyOnLastColumn_Valid()
        {
            BombLanding.ResolveCell(new SimInt2(16, 0), new SimInt2(1, 0), 3, Grid, out var cell, out var valid);
            Assert.AreEqual(new SimInt2(19, 0), cell);
            Assert.IsTrue(valid, "x=19 = gridSize.x-1 → 마지막 열, 유효");
        }

        [Test]
        public void Edge_Origin_ZeroTiles_Valid()
        {
            BombLanding.ResolveCell(new SimInt2(0, 0), new SimInt2(0, 1), 0, Grid, out var cell, out var valid);
            Assert.AreEqual(new SimInt2(0, 0), cell);
            Assert.IsTrue(valid);
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-I/2 arm C/1 — 통합 공격자 루프의 골격 + 폭탄맨 분기.
    ///
    /// 어서션 복제 출처(구 `AttackSystemUnifiedLoopTests`):
    /// `BombThrower_PokeNeedle_FiresOnFifthBombWithSelfChosenTarget` ·
    /// `BombThrower_PokeNeedle_DoesNotCountWhenBombCannotLaunch` ·
    /// `PendingDeployment_Excludes_Attacker_From_Loop`.
    ///
    /// 계약 셋: **① 쿨다운은 CC 중에도 돈다**(깨어나면 즉시 공격) **② 발사 성사와 무관하게
    /// 쿨다운은 리셋된다** **③ 공격 사건은 폭탄이 실제로 손을 떠난 프레임만**.
    /// </summary>
    public class SimAttackBombThrowTests
    {
        private SimWorld _world;
        private SimChannels _channels;
        private AttackSystem _sut;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(1u, 1u));
            _channels = new SimChannels();
            _sut = new AttackSystem(_channels);
            _world.SetDeltaTime(0.016f);
        }

        private SimEntityId Bomber(SimVec3 pos, SimInt2 facing, int landingTiles = 2,
                                   float cooldown = 0.01f, uint seed = 12345u)
        {
            var e = _world.Create();
            _world.Set(e, new FactionTag { value = Faction.Defender });
            _world.Set(e, new DefenderUnitTag());
            _world.Set(e, new Health { value = 10f, max = 10f });
            _world.Set(e, SimTransform.FromPosition(pos));
            _world.Set(e, new AttackState
            {
                range = 5f, cooldownDuration = cooldown, cooldownRemaining = 0f,
                attackTargetCount = 1, targetMask = (int)Faction.Enemy,
            });
            _world.Set(e, new DeployedFacing { value = facing });
            _world.Set(e, new ProjectileRef { dataIndex = 0, speed = 10f, visualScale = 1f });
            _world.Set(e, new BombLauncherState
            {
                landingTiles = landingTiles,
                travelSec = 0.2f,
                fuseSec = 0.2f,
                aoeTileRange = 1,
                aoeTargetCap = 3,
                dmgBombDamage = 5f,
                sleepSec = 1.5f,
                stunSec = 1f,
                rng = new SimRandom(seed),
            });
            return e;
        }

        private void PokeNeedleSlot(SimEntityId host, int period, int tileRange = 4,
                                    DcPayloadKind payload = DcPayloadKind.ProjectileToTarget)
        {
            _world.AddBuffer<DcTriggerSlot>(host).Add(new DcTriggerSlot
            {
                instanceId = 1,
                trigger = DcTriggerKind.AttackN,
                period = (ushort)period,
                counter = 0,
                payload = payload,
                magnitude = 20f,
                projectileDataIndex = 0,
                speed = 10f,
                hitThreshold = 0.3f,
                visualScale = 1f,
                tileRange = tileRange,
                patternIndex = -1,
            });
        }

        private SimEntityId Enemy(SimVec3 pos)
        {
            var e = _world.Create();
            _world.Set(e, new FactionTag { value = Faction.Enemy });
            _world.Set(e, new AttackUnitTag());
            _world.Set(e, new Health { value = 10f, max = 10f });
            _world.Set(e, SimTransform.FromPosition(pos));
            return e;
        }

        private List<ProjectileSpawnRequest> Carriers()
        {
            var reqs = new List<ProjectileSpawnRequest>();
            foreach (var e in _world.With<ProjectileRequestCarrier>())
                reqs.Add(_world.Get<ProjectileSpawnRequest>(e));
            return reqs;
        }

        private ushort Counter(SimEntityId e) => _world.GetBuffer<DcTriggerSlot>(e)[0].counter;
        private float Cooldown(SimEntityId e) => _world.Get<AttackState>(e).cooldownRemaining;

        // ── 구 오라클 복제 ────────────────────────────────────────────────────

        [Test]
        public void PokeNeedle_FiresOnFifthBomb_WithSelfChosenTarget()
        {
            var bomber = Bomber(new SimVec3(0f, 0f, 0f), new SimInt2(1, 0));
            PokeNeedleSlot(bomber, period: 5);

            var far = Enemy(new SimVec3(3f, 0f, 0f));
            var near = Enemy(new SimVec3(1f, 0f, 0f));

            for (int i = 0; i < 4; i++) _sut.Run(_world);
            Assert.AreEqual(0, Carriers().Count, "5발째 전에는 니들이 나가면 안 된다(폭탄만 나간다)");

            _sut.Run(_world);
            var reqs = Carriers();
            Assert.AreEqual(1, reqs.Count, "폭탄맨도 5번째 발사에 니들 캐리어를 스폰해야 한다");
            Assert.AreEqual(MovementKind.HomingToEntity, reqs[0].movement);
            Assert.AreEqual(20f, reqs[0].damage, 1e-4f, "flat magnitude(계약 7)");
            Assert.AreEqual(near, reqs[0].target,
                "host 가 대상을 안 주므로 페이로드가 스스로 최근접 적을 고른다");
            Assert.AreNotEqual(far, reqs[0].target);
            Assert.AreEqual(bomber, reqs[0].owner, "위협 귀속은 폭탄맨 본인");
        }

        [Test]
        public void PokeNeedle_DoesNotCount_WhenBombCannotLaunch()
        {
            // 그리드 밖을 향해 배치 → `landValid=false`. 쿨다운은 돌지만 폭탄이 손을 떠나지
            // 않으므로 카운트도 없다(계약 2).
            var bomber = Bomber(new SimVec3(0f, 0f, 0f), new SimInt2(-1, 0), landingTiles: 5);
            PokeNeedleSlot(bomber, period: 1);
            Enemy(new SimVec3(1f, 0f, 0f));

            for (int i = 0; i < 5; i++) _sut.Run(_world);

            Assert.AreEqual(0, Counter(bomber),
                "폭탄이 안 나간 프레임은 공격 사건이 아니다 — 카운터가 움직이면 안 된다");
            Assert.AreEqual(0, Carriers().Count);
            Assert.AreEqual(0, _channels.DcTriggerFired.Count);
        }

        [Test]
        public void PendingDeployment_ExcludesAttackerFromLoop()
        {
            var bomber = Bomber(new SimVec3(0f, 0f, 0f), new SimInt2(1, 0), cooldown: 1f);
            _world.Set(bomber, new PendingDeployment());

            _sut.Run(_world);

            Assert.IsFalse(_world.Has<ProjectileSpawnRequest>(bomber), "배치 중 공격자는 발사하지 않는다");
            Assert.AreEqual(0, _channels.UnitAttackVisual.Count);
            Assert.AreEqual(0f, Cooldown(bomber), 1e-4f, "루프에 들어가지 않으니 쿨다운도 안 돈다");
        }

        // ── 폭탄 요청 ─────────────────────────────────────────────────────────

        [Test]
        public void BombRequest_LandsOnTheFacingCell_AndSitsOnTheAttackerItself()
        {
            // ⚠ 요청이 **캐리어가 아니라 공격자 본인**에 붙는다 — 주 발사의 자리다.
            var bomber = Bomber(new SimVec3(0f, 0f, 0f), new SimInt2(1, 0), landingTiles: 2);

            _sut.Run(_world);

            Assert.AreEqual(0, Carriers().Count, "주 발사는 캐리어를 쓰지 않는다");
            Assert.IsTrue(_world.Has<ProjectileSpawnRequest>(bomber));
            var req = _world.Get<ProjectileSpawnRequest>(bomber);
            Assert.AreEqual(MovementKind.GrenadeToCell, req.movement);
            Assert.AreEqual(PayloadKind.TileAoe, req.payload);
            Assert.AreEqual(GridMath.CellToWorldCenter(new SimInt2(2, 0), 1f, 0f, default), req.impact,
                "방향 × landingTiles 만큼 앞 셀");
            Assert.AreEqual(1, req.impactTileRange);
            Assert.AreEqual(3, req.aoeTargetCap);
            Assert.AreEqual(0.2f, req.flightTime, 1e-4f, "travel 은 거리 무관 고정 — 요청이 싣고 온다");
            Assert.AreEqual(0.2f, req.fuseSec, 1e-4f);
            Assert.AreEqual(bomber, req.owner);
            Assert.AreEqual(ProjectileTargetFaction.Enemy, req.targetFaction);
        }

        [Test]
        public void BombType_IsOneOfThree_AndDrivesDamageOrCc()
        {
            // 3종 균등(0 피해 · 1 수면 · 2 스턴). 어느 종류든 **나머지 축은 0** 이어야 한다.
            var bomber = Bomber(new SimVec3(5f, 0f, 5f), new SimInt2(1, 0));

            _sut.Run(_world);
            var req = _world.Get<ProjectileSpawnRequest>(bomber);

            Assert.That(req.bombType, Is.InRange((byte)0, (byte)2));
            if (req.bombType == 0)
            {
                Assert.AreEqual(5f, req.damage, 1e-4f);
                Assert.AreEqual(0, req.ccKind);
                Assert.AreEqual(0f, req.ccDuration, 1e-4f);
            }
            else
            {
                Assert.AreEqual(0f, req.damage, 1e-4f, "CC 폭탄은 피해가 없다");
                Assert.AreEqual(req.bombType == 1 ? (byte)CcKind.Sleep : (byte)CcKind.Stun, req.ccKind);
                Assert.AreEqual(req.bombType == 1 ? 1.5f : 1f, req.ccDuration, 1e-4f);
            }
        }

        [Test]
        public void Rng_AdvancesPerLaunch_AndIsPerCaster()
        {
            // ⚠ `rng` 는 상태 해시에 실린다 — 캐스터별 독립 스트림이고 draw 마다 전진한다.
            var a = Bomber(new SimVec3(0f, 0f, 0f), new SimInt2(1, 0), seed: 777u);
            var b = Bomber(new SimVec3(0f, 0f, 5f), new SimInt2(1, 0), seed: 777u);

            uint before = _world.Get<BombLauncherState>(a).rng.state;
            _sut.Run(_world);

            Assert.AreNotEqual(before, _world.Get<BombLauncherState>(a).rng.state, "draw 마다 전진한다");
            Assert.AreEqual(_world.Get<BombLauncherState>(a).rng.state,
                            _world.Get<BombLauncherState>(b).rng.state,
                            "같은 시드 두 캐스터는 같은 스트림 — 서로의 draw 를 소비하지 않는다");
        }

        [Test]
        public void OffGridLaunch_DoesNotAdvanceRng_ButStillResetsCooldown()
        {
            // 거절된 프레임은 draw 자체가 없다. 그런데 쿨다운은 돈다(재스캔 스팸 방지).
            var bomber = Bomber(new SimVec3(0f, 0f, 0f), new SimInt2(-1, 0), landingTiles: 5, cooldown: 2f);
            uint before = _world.Get<BombLauncherState>(bomber).rng.state;

            _sut.Run(_world);

            Assert.AreEqual(before, _world.Get<BombLauncherState>(bomber).rng.state,
                "발사가 거절되면 rng 스트림이 전진하지 않는다");
            Assert.IsFalse(_world.Has<ProjectileSpawnRequest>(bomber));
            Assert.AreEqual(2f, Cooldown(bomber), 1e-4f, "성사 여부와 무관하게 쿨다운은 리셋된다");
        }

        // ── 루프 골격 ─────────────────────────────────────────────────────────

        [Test]
        public void Cooldown_TicksDown_AndBlocksLaunch()
        {
            var bomber = Bomber(new SimVec3(0f, 0f, 0f), new SimInt2(1, 0), cooldown: 1f);

            _sut.Run(_world);
            Assert.AreEqual(1f, Cooldown(bomber), 1e-4f, "발사 프레임에 쿨다운이 리셋된다");

            _world.RemoveComponent<ProjectileSpawnRequest>(bomber);
            _sut.Run(_world);
            Assert.AreEqual(1f - 0.016f, Cooldown(bomber), 1e-4f, "다음 틱엔 dt 만큼 줄어든다");
            Assert.IsFalse(_world.Has<ProjectileSpawnRequest>(bomber), "쿨다운 중엔 발사하지 않는다");
        }

        [Test]
        public void ActionLock_BlocksLaunch_ButNotTheCooldownTick()
        {
            // ⚠ 이것이 CC 규약이다 — START 만 막고 쿨다운은 굴린다. 그래야 깨어난 유닛이
            //   한 쿨 기다리지 않고 즉시 때린다.
            var bomber = Bomber(new SimVec3(0f, 0f, 0f), new SimInt2(1, 0), cooldown: 1f);
            var attack = _world.Get<AttackState>(bomber);
            attack.cooldownRemaining = 0.5f;
            _world.Set(bomber, attack);
            _world.AddBuffer<CcEffect>(bomber).Add(new CcEffect { kind = CcKind.Stun, remainingTime = 5f });

            _sut.Run(_world);

            Assert.IsFalse(_world.Has<ProjectileSpawnRequest>(bomber), "행동불가 중엔 발사하지 않는다");
            Assert.AreEqual(0.5f - 0.016f, Cooldown(bomber), 1e-4f, "그래도 쿨다운은 흐른다");
        }

        [Test]
        public void LeapFlight_JoinsTheSameLockPredicate()
        {
            var bomber = Bomber(new SimVec3(0f, 0f, 0f), new SimInt2(1, 0), cooldown: 1f);
            _world.Set(bomber, new LeapFlight());

            _sut.Run(_world);

            Assert.IsFalse(_world.Has<ProjectileSpawnRequest>(bomber),
                "도약 비행은 CC 와 같은 술어에 OR 로 합류한다");
        }

        [Test]
        public void MissingFacingOrProjectileRef_IsInert()
        {
            // 저작 누락 — 조용히 no-op 이되 **쿨다운도 리셋하지 않는다**(발사 시도 자체가 없다).
            var noFacing = Bomber(new SimVec3(0f, 0f, 0f), new SimInt2(1, 0), cooldown: 3f);
            _world.RemoveComponent<DeployedFacing>(noFacing);
            var noProj = Bomber(new SimVec3(0f, 0f, 5f), new SimInt2(1, 0), cooldown: 3f);
            _world.RemoveComponent<ProjectileRef>(noProj);

            _sut.Run(_world);

            Assert.IsFalse(_world.Has<ProjectileSpawnRequest>(noFacing));
            Assert.IsFalse(_world.Has<ProjectileSpawnRequest>(noProj));
            Assert.AreEqual(0f, Cooldown(noFacing), 1e-4f);
            Assert.AreEqual(0f, Cooldown(noProj), 1e-4f);
        }

        [Test]
        public void AttackVisual_UsesRawCooldownDuration_NotTheSpeedAdjustedOne()
        {
            // ⚠ START 경로와 다르다 — 폭탄 분기는 `attackSpeedMul` 을 타지 않는다(구 sim 값).
            var bomber = Bomber(new SimVec3(0f, 0f, 0f), new SimInt2(1, 0), cooldown: 0.8f);
            _world.Set(bomber, new ModifierStats { attackSpeedMul = 2f });

            _sut.Run(_world);

            var visual = _channels.UnitAttackVisual.Drain();
            Assert.AreEqual(1, visual.Count);
            Assert.AreEqual(bomber, visual[0].attacker);
            Assert.AreEqual(0.8f, visual[0].attackAnimPeriod, 1e-4f);
            Assert.AreEqual(GridMath.CellToWorldCenter(new SimInt2(2, 0), 1f, 0f, default), visual[0].targetWorld,
                "facing 은 착지 셀을 본다");
        }

        [Test]
        public void UnhandledPayload_WarnsWithTheBombCode()
        {
            var bomber = Bomber(new SimVec3(0f, 0f, 0f), new SimInt2(1, 0));
            PokeNeedleSlot(bomber, period: 1, payload: DcPayloadKind.SelfTileAoe);
            Enemy(new SimVec3(1f, 0f, 0f));

            _sut.Run(_world);

            var warnings = _channels.Warnings.Drain();
            Assert.AreEqual(1, warnings.Count);
            Assert.AreEqual(SimWarningCode.BombThrowUnhandledPayload, warnings[0].code,
                "사건 지점마다 코드를 가른다 — 어느 아키타입의 카드가 죽었는지가 진단의 실질이다");
            Assert.AreEqual(bomber, warnings[0].entity);
            Assert.AreEqual(0, Counter(bomber), "카운트는 그래도 소비됐다(계약 5)");
        }
    }
}
