using System.Collections.Generic;
using Wassup.Sim.Units;

namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-E/3 — 캡처 **#1** · <see cref="SimPhase.FieldsAndPeriodic"/>(P1).
    /// 구 `LastRunSystem` 이식. **P1 의 첫 스텝**이다.
    ///
    /// 라스트런 만료 시 **최대체력 × fraction** 을 자해 피해로 입힌다. `Health` 쓰기는 Units
    /// 소유이므로 정식 인박스(`IncomingDamage`)에 append 하고 감산·사망은 #34 가 한다.
    /// P1 이라 소비자(#34, P9)보다 앞 — **같은 프레임에 정산**된다.
    ///
    /// `source` 를 비우는 것이 계약이다(`SimEntityId.Null` = 미귀속) — 자해는 킬을 귀속시키지
    /// 않는다(DoT·환경 피해와 같은 컨벤션).
    /// </summary>
    public sealed class LastRunSystem
    {
        private readonly List<SimEntityId> _expired = new List<SimEntityId>();

        public void Run(SimWorld world)
        {
            if (!SimSingleton.TryGet(world, out RedBullGimmickConfig config)) return;   // 분류 B 게이트

            float dt = world.DeltaTime;
            _expired.Clear();

            foreach (SimEntityId e in world.With<LastRun>())
            {
                LastRun lr = world.Get<LastRun>(e);
                lr.remaining -= dt;
                world.Set(e, lr);
                // ⚠ 가드가 `> 0f` 라 **정확히 0 이면 만료**다.
                if (lr.remaining > 0f) continue;

                // 피해는 Health **와** 피해 버퍼가 둘 다 있을 때만. 제거는 아래에서 무조건.
                List<IncomingDamage> inbox = world.GetBuffer<IncomingDamage>(e);
                if (inbox != null && world.TryGet(e, out Health health))
                {
                    inbox.Add(new IncomingDamage
                    {
                        amount = health.max * config.lastRunDamageFraction,
                        source = SimEntityId.Null,
                    });
                }
                _expired.Add(e);
            }

            for (int i = 0; i < _expired.Count; i++) world.RemoveComponent<LastRun>(_expired[i]);
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-E/3 — 캡처 **#2** · <see cref="SimPhase.FieldsAndPeriodic"/>(P1).
    /// 구 `HazardLifetimeSystem` 이식.
    ///
    /// **매 프레임 인덱스를 통째로 재빌드한다 — "갱신이 곧 회수"다.** 증분 인덱스로 바꾸면
    /// 만료된 셀이 남고, 그게 tie-break ⑥ 의 뿌리다(계획서: 재작성은 자료구조만,
    /// **순회 순서는 보존**). 순서 계약은 <see cref="HazardCellIndex"/> 가 진다.
    ///
    /// ⚠ **만료 판정이 인덱스 적재보다 앞이다**(continue) — 뒤집으면 죽는 프레임에 장판이
    /// 한 번 더 먹는다.
    ///
    /// ⚠ **여기서 엔티티를 파괴한다 — P1 에서.** 사망 릴레이의 "P12 단독 파괴" 는 **DeadTag
    /// 로 마킹된 유닛**에 대한 계약이고, 수명 만료 해저드는 그 릴레이에 참여하지 않는다
    /// (`SimWorld.Destroy` 주석 참조). 같은 해저드가 **피해로** 죽는 경로는 별개이고 그건 #41 이다.
    /// </summary>
    public sealed class HazardLifetimeSystem
    {
        private readonly List<SimEntityId> _dead = new List<SimEntityId>();

        public void Run(SimWorld world)
        {
            if (!SimSingleton.TryGet(world, out HazardSingleton singleton)) return;   // 분류 C 게이트
            if (!singleton.IsCreated) return;

            singleton.cellToEffects.Clear();
            float dt = world.DeltaTime;
            _dead.Clear();

            // 구 쿼리는 `Hazard` + 두 버퍼를 **모두** 요구한다.
            foreach (SimEntityId e in world.With<Hazard>())
            {
                List<HazardCellsBuffer> cells = world.GetBuffer<HazardCellsBuffer>(e);
                if (cells == null) continue;
                List<HazardEffectsBuffer> effects = world.GetBuffer<HazardEffectsBuffer>(e);
                if (effects == null) continue;

                Hazard hazard = world.Get<Hazard>(e);
                hazard.remainingLife -= dt;
                world.Set(e, hazard);
                if (hazard.remainingLife <= 0f)
                {
                    _dead.Add(e);
                    continue;   // ⚠ 만료 프레임엔 인덱스에 기여하지 않는다
                }

                for (int c = 0; c < cells.Count; c++)
                for (int f = 0; f < effects.Count; f++)
                    singleton.cellToEffects.Add(cells[c].cell, effects[f].effect);
            }

            for (int i = 0; i < _dead.Count; i++) world.Destroy(_dead[i]);
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-E/3 — 캡처 **#6** · <see cref="SimPhase.FieldsAndPeriodic"/>(P1).
    /// 구 `ObstacleLifetimeSystem` 이식.
    ///
    /// 막힌 셀 집합도 **매 프레임 재빌드**다. 두 루프로 갈리는데 그 분할이 계약이다:
    /// ① 순수 장애물(`BlockingHazardCellsBuffer` **없음**)은 수명을 깎고 만료 시 파괴한다.
    /// ② 이동 차단 해저드는 수명을 여기서 관리하지 않고(#2 가 한다) **셀만 등록**하며,
    ///    `DeadTag` 가 붙은 것은 **즉시 제외**한다 — 죽었지만 아직 있는(P10~P12) 해저드는
    ///    그 순간부터 길을 막지 않는다.
    /// </summary>
    public sealed class ObstacleLifetimeSystem
    {
        private readonly List<SimEntityId> _dead = new List<SimEntityId>();

        public void Run(SimWorld world)
        {
            if (!SimSingleton.TryGet(world, out ObstacleSingleton obstacles)) return;   // 분류 C 게이트
            if (!obstacles.IsCreated) return;

            obstacles.blockedCells.Clear();
            float dt = world.DeltaTime;
            _dead.Clear();

            foreach (SimEntityId e in world.With<Obstacle>())
            {
                if (world.HasBuffer<BlockingHazardCellsBuffer>(e)) continue;   // 아래 ② 가 처리

                Obstacle o = world.Get<Obstacle>(e);
                o.remainingLife -= dt;
                world.Set(e, o);
                if (o.remainingLife <= 0f) _dead.Add(e);
                else obstacles.blockedCells.Add(o.cell);
            }

            foreach (SimEntityId e in world.WithBuffer<BlockingHazardCellsBuffer>())
            {
                if (!world.Has<BlockingHazard>(e)) continue;
                if (world.Has<DeadTag>(e)) continue;   // 죽은 해저드는 즉시 길을 열어준다
                List<BlockingHazardCellsBuffer> cells = world.GetBuffer<BlockingHazardCellsBuffer>(e);
                for (int i = 0; i < cells.Count; i++) obstacles.blockedCells.Add(cells[i].cell);
            }

            for (int i = 0; i < _dead.Count; i++) world.Destroy(_dead[i]);
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-E/3 — 구 `SystemAPI.GetSingleton&lt;T&gt;` 대응.
    /// 생성 순서 첫 보유자를 싱글턴으로 본다.
    ///
    /// 18-C 는 이 헬퍼를 `FatigueAccrualSystem` 안 private 으로 뒀다("호출처가 하나라 여기 둔다 —
    /// 반복이 생기면 그때 올린다"). 18-E 에서 호출처가 **4개**가 됐으므로 약속대로 올린다
    /// (제약 10 의 (b) 재사용 2+). 분류 B·C 게이트 20건이 전부 이 모양이라 남은 조각도 쓴다.
    /// </summary>
    public static class SimSingleton
    {
        public static bool TryGet<T>(SimWorld world, out T value) where T : struct
        {
            foreach (SimEntityId e in world.With<T>())
            {
                value = world.Get<T>(e);
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// 싱글턴을 **들고 있는 엔티티**. 값을 되쓰는 시스템(예: `PickupSpawnState.rng`)이 필요로 한다 —
        /// 구 sim 의 `GetSingletonRW` 자리다. 없으면 `Null`.
        /// </summary>
        public static SimEntityId FindEntity<T>(SimWorld world) where T : struct
        {
            foreach (SimEntityId e in world.With<T>()) return e;
            return SimEntityId.Null;
        }
    }
}
