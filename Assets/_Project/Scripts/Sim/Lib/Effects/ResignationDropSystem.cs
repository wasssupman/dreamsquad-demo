using Wassup.Sim.Units;

namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-G/4 — 캡처 #35(P10). 구 `ResignationDropSystem` 이식.
    ///
    /// 배치 방어유닛이 (원인 불문) 죽으면 그 타일에 사직서를 떨어뜨린다.
    ///
    /// ⚠ **"죽었지만 아직 있는" 창에서만 성립한다.** P10 은 마킹(P3/P9)과 파괴(P12) 사이이고,
    /// 그 창이 없으면 죽은 유닛의 `DefenderTile` 을 읽을 수 없다. 즉시 파괴로 바꾸면 이 시스템은
    /// 아무것도 못 본다 — 신 sim 이 2-phase delete 를 흉내내는 게 아니라 **필요해서** 유지하는
    /// 이유가 이것이다.
    ///
    /// ⚠ **defender 당 정확히 1회**가 계약이다. `DeadTag` 는 사망 프레임에 붙고 같은 틱 P12 가
    /// 파괴하므로 관측 기회가 한 번뿐이다 — 중복 드랍 방지 장치가 따로 없는 이유다.
    /// P12 를 지연시키면 매 틱 사직서가 쌓인다.
    ///
    /// ⚠ 기믹 config 부재 = 미가동(self-gate). 구 sim 의 `RequireForUpdate` 가
    /// <see cref="SimConfig.ClockOut"/> 의 null 검사로 이사했다.
    /// </summary>
    public sealed class ResignationDropSystem
    {
        private readonly SimCommandBuffer _ecb = new SimCommandBuffer();

        public void Run(SimWorld world)
        {
            if (world.Config.ClockOut == null) return;

            foreach (var entity in world.With<DeadTag>())
            {
                if (!world.Has<DefenderUnitTag>(entity)) continue;
                if (!world.TryGet<DefenderTile>(entity, out var tile)) continue;

                var cell = tile.cell;
                _ecb.Defer(w =>
                {
                    var letter = w.CreateInternal();
                    w.Set(letter, new Resignation { cell = cell });
                });
            }

            _ecb.Playback(world);
        }
    }
}
