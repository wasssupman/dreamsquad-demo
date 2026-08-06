using System.Collections.Generic;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-J/3 — 궁극기 도약 연출 신호의 종류. 구 `UltimateLeapVisualKind` 이식.
    /// ⚠ append-only.
    /// </summary>
    public enum UltimateLeapVisualKind : byte
    {
        /// 발동 프레임 — 이탈 상승 시작.
        Ascend = 0,
        /// 착지 프레임 — 강하 시작(**sim 은 이미 착지 셀로 텔레포트했다**).
        Descend = 1,
    }

    /// <summary>
    /// battle-sim-extraction unit 18-J/3 — 궁극기 도약 연출 신호. 구 `UltimateLeapVisualEvent` 이식.
    ///
    /// **왜 보스 도약(아치 하나)을 재사용할 수 없나**: 이탈과 강하가 **예고 시간만큼 떨어진 별개
    /// 사건**이라 한 이벤트에 실으려면 발동 시점에 도착 시각을 알아야 하는데, 그 시점은 sim
    /// 시퀀스가 정한다. 그래서 <see cref="UltimateLeapVisualKind"/> 2종으로 나눠 보내고
    /// **브리지는 예고 시간을 복제하지 않는다**(복제하면 두 시계가 갈린다).
    ///
    /// ⚠ 이 채널의 뷰는 게임 규칙을 하나도 소유하지 않는다 — 피해도 텔레포트도 sim 이 이미 끝냈고
    /// 브리지에는 슬램 VFX 타이밍(뷰 도착)만 남는다.
    /// </summary>
    public struct UltimateLeapVisualEvent
    {
        public SimEntityId entity;
        public UltimateLeapVisualKind kind;
        /// `Ascend` = 이탈 위치 · `Descend` = 착지 셀 중심.
        public SimVec3 world;
        /// 착지 VFX(`Descend` 만, `&lt;0` = 무연출).
        public int dataIndex;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-J/3 — 캡처 **#43** · <see cref="SimPhase.Destruction"/>(P12).
    /// 구 `UltimateLeapSystem` 이식.
    ///
    /// **시퀀스를 sim 이 소유하는 이유**: 예고 창은 회피 창이자 피해 게이트 = **게임 규칙**이다
    /// (일반 도약의 비행 창이 뷰 시계 소유인 것과 비대칭인 게 맞다 — 그쪽은 연출 정합이다).
    ///
    /// 착지 순서 4단이 계약이다: **텔레포트 요청 → 슬램 캐리어 → 강하 신호 → 상태 해제.**
    /// 텔레포트는 위치가 Movement 소유라 `BlinkRequest` seam 으로 나가고, #44 가 **같은 phase 안에서
    /// 뒤**라 같은 틱에 착지한다.
    ///
    /// ⚠ `DeadTag` 제외는 **방어적 가드**다 — 계약상 공중 사망이 없으므로 정상 경로에선 도달하지
    /// 않는다. 오버킬 프레임 경합으로 죽었다면 착지 없이 상태만 걷어 **시체가 잠긴 채 남지 않게** 한다.
    ///
    /// ⚠ 상태 해제는 `UltimateLeapState` + `LeapFlight` **둘 다**다(붙을 때와 대칭) —
    /// 무적과 잠금이 함께 떨어져야 한다.
    /// </summary>
    public sealed class UltimateLeapSystem
    {
        private readonly SimChannels _channels;
        private readonly List<SimEntityId> _landed = new List<SimEntityId>();

        public UltimateLeapSystem(SimChannels channels) => _channels = channels;

        public void Run(SimWorld world)
        {
            float dt = world.DeltaTime;
            _landed.Clear();

            foreach (SimEntityId entity in world.With<UltimateLeapState>())
            {
                if (world.Has<DeadTag>(entity)) continue;

                var leap = world.Get<UltimateLeapState>(entity);
                leap.remaining -= dt;
                world.Set(entity, leap);
                if (leap.remaining > 0f) continue;

                // 1. 텔레포트 — 위치 쓰기는 Movement 소유라 요청만 낸다.
                _channels.BlinkRequest.Enqueue(new BlinkRequestEvent
                {
                    entity = entity,
                    destWorld = leap.landingWorld,
                });

                // 2. 슬램 캐리어 — shooter 스냅샷 없이 **고정 피해**다(보스 도약 슬램·메테오 barrage
                //    와 같은 규약). `dataIndex` 는 bake 가 보장한다.
                if (leap.slamDamage > 0f)
                {
                    var carrier = world.CreateInternal();
                    world.Set(carrier, new ProjectileSpawnRequest
                    {
                        movement = MovementKind.SkyFall,
                        payload = PayloadKind.TileAoe,
                        origin = leap.landingWorld,
                        impact = leap.landingWorld,
                        damage = leap.slamDamage,
                        impactTileRange = leap.slamTileRange,
                        flightTime = 0f, // 즉발 — 예고가 이미 창을 벌었다
                        arcHeight = 0f,
                        dataIndex = leap.projectileDataIndex,
                        visualScale = 1f,
                        owner = entity,
                        targetFaction = ProjectileTargetFaction.Defender,
                    });
                    world.Set(carrier, new ProjectileRequestCarrier());
                }

                // 3. 강하 연출 — 뷰는 **지금부터** 떨어지기 시작한다. sim 은 이미 착지했으므로
                //    슬램 VFX 타이밍(뷰 도착)은 소비 지점이 소유한다.
                _channels.UltimateLeapVisual.Enqueue(new UltimateLeapVisualEvent
                {
                    entity = entity,
                    kind = UltimateLeapVisualKind.Descend,
                    world = leap.landingWorld,
                    dataIndex = leap.projectileDataIndex,
                });

                _landed.Add(entity);
            }

            // 4. 상태 해제 — 무적과 잠금이 함께 떨어진다.
            for (int i = 0; i < _landed.Count; i++)
            {
                world.RemoveComponent<UltimateLeapState>(_landed[i]);
                world.RemoveComponent<LeapFlight>(_landed[i]);
            }
        }
    }
}
