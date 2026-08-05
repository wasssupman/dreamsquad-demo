using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Combat;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Tests.EditMode
{
    /// <summary>
    /// battle-sim-extraction unit 18-I/1 — 해저드 캐스트(#18) 이식의 오라클.
    ///
    /// 18-E 가 이 조각을 미룬 이유(`DcTriggerSlot` 버퍼 존재 확인)는 18-G/2 로 해소됐다.
    /// 여기서 지키는 것은 **캐스트 성사가 곧 공격 사건**이라는 계약이다 — 이 캐스터들은
    /// 사거리 0 이라 공격 루프에 도달하지 못하므로, 이 채널이 없으면 AttackN 트리거가
    /// 영영 돌지 않는다.
    /// </summary>
    public class SimHazardCastTests
    {
        private SimWorld _world;
        private SimChannels _channels;
        private HazardCastSystem _sut;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(1u, 1u));
            _channels = new SimChannels();
            _sut = new HazardCastSystem(_channels);
            _world.SetDeltaTime(0.1f);

            var f = _world.Create();
            _world.Set(f, new FlowFieldSingleton
            {
                flow = new SimVec2[1], dist = new int[1],
                gridSize = new SimInt2(64, 64), tileSize = 1f, origin = default,
            });
        }

        private SimEntityId Target(SimVec3 pos, Faction faction = Faction.Enemy)
        {
            var e = _world.Create();
            _world.Set(e, new FactionTag { value = faction });
            _world.Set(e, new PathFollowState { speed = 1f });
            _world.Set(e, SimTransform.FromPosition(pos));
            return e;
        }

        private SimEntityId Caster(SimVec3 pos, float range = 3f, float cooldown = 4f,
                                   HazardCastKind kind = HazardCastKind.Zone, int mask = (int)Faction.Enemy)
        {
            var e = _world.Create();
            _world.Set(e, new DefenderUnitTag());
            _world.Set(e, SimTransform.FromPosition(pos));
            _world.Set(e, new HazardCastState
            {
                range = range, cooldownDuration = cooldown, cooldownRemaining = 0f,
                targetMask = mask, dataIndex = 0, kind = kind,
                footprintWidth = 2, footprintHeight = 2,
            });
            return e;
        }

        private float Cooldown(SimEntityId e) => _world.Get<HazardCastState>(e).cooldownRemaining;

        [Test]
        public void CastsAtTheTargetCell_AndResetsCooldown()
        {
            var target = Target(new SimVec3(2f, 0, 0));
            var caster = Caster(new SimVec3(0, 0, 0));

            _sut.Run(_world);

            var reqs = _channels.HazardSpawnRequest.Drain();
            Assert.AreEqual(1, reqs.Count);
            Assert.AreEqual(new SimInt2(2, 0), reqs[0].centerCell, "해저드는 대상의 셀에 놓인다");
            Assert.AreEqual(target, reqs[0].target);
            Assert.AreEqual(caster, reqs[0].caster);
            Assert.AreEqual(4f, Cooldown(caster), 1e-4f);
        }

        [Test]
        public void FootprintIsAlwaysOneByOne_NotTheAuthoredValue()
        {
            // ⚠ 저작에 2×2 가 들어 있어도 구 sim 은 1×1 로 보낸다 — 재현 대상이다.
            Target(new SimVec3(1f, 0, 0));
            Caster(new SimVec3(0, 0, 0));

            _sut.Run(_world);

            var req = _channels.HazardSpawnRequest.Drain()[0];
            Assert.AreEqual(1, req.width);
            Assert.AreEqual(1, req.height);
        }

        [Test]
        public void RangeGateIsChebyshevTiles()
        {
            Target(new SimVec3(4f, 0, 0)); // 체비셰프 4 > range 3
            var caster = Caster(new SimVec3(0, 0, 0), range: 3f);

            _sut.Run(_world);

            Assert.AreEqual(0, _channels.HazardSpawnRequest.Count);
            Assert.AreEqual(0f, Cooldown(caster), 1e-4f, "대상이 없으면 쿨다운도 리셋되지 않는다");
        }

        [Test]
        public void TargetMaskFiltersFaction()
        {
            Target(new SimVec3(1f, 0, 0), Faction.Defender);
            Caster(new SimVec3(0, 0, 0), mask: (int)Faction.Enemy);

            _sut.Run(_world);

            Assert.AreEqual(0, _channels.HazardSpawnRequest.Count, "마스크에 없는 진영은 후보가 아니다");
        }

        [Test]
        public void EquidistantTie_GoesToTheLowerSimId()
        {
            // ⚠ 이 축이 없으면 결과가 스냅샷 순서에 걸려 같은 판이 실행마다 갈린다.
            var first = Target(new SimVec3(2f, 0, 0));
            Target(new SimVec3(-2f, 0, 0)); // 같은 거리, 나중에 생성 = 높은 simId
            Caster(new SimVec3(0, 0, 0), range: 5f);

            _sut.Run(_world);

            Assert.AreEqual(first, _channels.HazardSpawnRequest.Drain()[0].target);
        }

        [Test]
        public void CooldownTicks_AndBlocksCasting()
        {
            Target(new SimVec3(1f, 0, 0));
            var caster = Caster(new SimVec3(0, 0, 0), cooldown: 1f);

            _sut.Run(_world);
            Assert.AreEqual(1f, Cooldown(caster), 1e-4f);
            _channels.HazardSpawnRequest.Drain();

            _sut.Run(_world);
            Assert.AreEqual(0, _channels.HazardSpawnRequest.Count, "쿨다운 중엔 캐스트하지 않는다");
            Assert.AreEqual(0.9f, Cooldown(caster), 1e-4f, "쿨다운은 계속 흐른다");
        }

        [Test]
        public void InertAuthoring_NeverCasts()
        {
            Target(new SimVec3(1f, 0, 0));
            var none = Caster(new SimVec3(0, 0, 0), kind: HazardCastKind.None);
            var noData = Caster(new SimVec3(0, 0, 1f));
            var s = _world.Get<HazardCastState>(noData);
            s.dataIndex = -1;
            _world.Set(noData, s);

            _sut.Run(_world);

            Assert.AreEqual(0, _channels.HazardSpawnRequest.Count, "kind None / dataIndex<0 은 inert");
            Assert.AreEqual(0f, Cooldown(none), 1e-4f);
        }

        [Test]
        public void CastEvent_OnlyForCastersCarryingACard()
        {
            Target(new SimVec3(1f, 0, 0));
            var plain = Caster(new SimVec3(0, 0, 0));
            var carded = Caster(new SimVec3(0, 0, 1f));
            _world.AddBuffer<DcTriggerSlot>(carded).Add(new DcTriggerSlot { patternIndex = -1 });

            _sut.Run(_world);

            var casts = _channels.Cast.Drain();
            Assert.AreEqual(1, casts.Count, "⚠ 카드 없는 캐스터에도 내면 이벤트가 쌓이기만 한다");
            Assert.AreEqual(carded, casts[0].caster);
            Assert.AreEqual(2, _channels.HazardSpawnRequest.Count, "스폰 요청은 둘 다 낸다");
        }

        [Test]
        public void CastEventCarriesTheCasterPosition_NotTheTargetCell()
        {
            Target(new SimVec3(3f, 0, 0));
            var caster = Caster(new SimVec3(1f, 0.5f, 2f));
            _world.AddBuffer<DcTriggerSlot>(caster).Add(new DcTriggerSlot { patternIndex = -1 });

            _sut.Run(_world);

            Assert.AreEqual(new SimVec3(1f, 0.5f, 2f), _channels.Cast.Drain()[0].casterPos,
                "드레인이 위치를 다시 조회하지 않도록 발사 원점을 싣는다");
        }

        [Test]
        public void DeadOrPendingCasters_AndTargets_AreExcluded()
        {
            var deadTarget = Target(new SimVec3(1f, 0, 0));
            _world.Set(deadTarget, new DeadTag());
            var pendingTarget = Target(new SimVec3(0, 0, 1f));
            _world.Set(pendingTarget, new PendingDeployment());

            var deadCaster = Caster(new SimVec3(0, 0, 0));
            _world.Set(deadCaster, new DeadTag());

            _sut.Run(_world);

            Assert.AreEqual(0, _channels.HazardSpawnRequest.Count);
        }

        [Test]
        public void TargetWithoutPathState_IsNotACandidate()
        {
            // 후보 조건이 FactionTag + 위치 + PathFollowState 셋 다인 것이 계약이다.
            var e = _world.Create();
            _world.Set(e, new FactionTag { value = Faction.Enemy });
            _world.Set(e, SimTransform.FromPosition(new SimVec3(1f, 0, 0)));
            Caster(new SimVec3(0, 0, 0));

            _sut.Run(_world);

            Assert.AreEqual(0, _channels.HazardSpawnRequest.Count);
        }

        [Test]
        public void VisualEventAimsAtTheCellCenter_AtCasterHeight()
        {
            Target(new SimVec3(2f, 0, 0));
            Caster(new SimVec3(0, 1.25f, 0));

            _sut.Run(_world);

            var visual = _channels.UnitAttackVisual.Drain()[0];
            Assert.AreEqual(GridMath.CellToWorldCenter(new SimInt2(2, 0), 1f, 1.25f, default), visual.targetWorld,
                "셀 중심 + 시전자 높이");
        }
    }
}
