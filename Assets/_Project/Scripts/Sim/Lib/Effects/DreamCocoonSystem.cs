using System.Collections.Generic;
using Wassup.Sim.Units;

namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-J/3 — 호접몽(잠 완주 감시자)의 상태. 구 `DreamCocoon` 이식.
    /// </summary>
    public struct DreamCocoon
    {
        /// <summary>
        /// 완주 판정 타이머. bake 가 **잠 duration − <see cref="Epsilon"/>** 으로 설정한다 —
        /// 완주 프레임이 자연만료 프레임과 겹치지 않게 하는 **보조** 안전핀이다(실제
        /// 파탄/완주 disambiguator 는 시스템의 `remaining &gt; 0` 가드 + 순서 핀).
        /// </summary>
        public float remaining;
        public StatKind stat;
        /// 완주 버프 배율(예 1.35 = +35%). 발화 시 `FromMultiplier` 로 op/mag 분해.
        public float mult;
        /// StatModifier 네임스페이스의 단일 할당자에서 부여.
        public ushort stackId;

        /// 내부 상수 — 튜닝 노브가 아니다. bake 가 `duration &lt;= Epsilon` 을 거절하므로
        /// `remaining` 은 항상 양수로 시작한다.
        public const float Epsilon = 0.05f;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-J/3 — 캡처 **#39** · <see cref="SimPhase.PostProcess"/>(P11).
    /// 구 `DreamCocoonSystem` 이식. "잠을 끝까지 잤나" 를 감시한다.
    ///
    /// **순서 핀이 곧 정확성이다**: `CcClear`(#37) **뒤** = 피격 wake 가 같은 프레임에 반영된 뒤
    /// 판정하고, `CcDecay`(#40) **앞** = 자연만료가 이 판정보다 늦게 일어나 프레임 히치에서도
    /// **"자연만료를 피격 파탄으로 오인"이 구조적으로 불가능**하다. 캡처 번호 39 가 그 사이에
    /// 있는 것이 우연이 아니다.
    ///
    /// 프레임당 판정 순서: **① 파탄 체크**(Sleep 부재 &amp;&amp; `remaining &gt; 0` → 제거, 버프 없음)
    /// **② 감산** **③ 완주 체크**(`remaining &lt;= 0` → self 영구 버프 후 제거).
    /// 마지막 프레임에 피격과 만료가 동시에 오면 ①이 선행이라 **파탄**이다 —
    /// `remaining &gt; 0` 가드가 그 disambiguator 이므로 제거 금지.
    ///
    /// ⚠ 완주 버프는 **영구**(TTL = +∞)이고 `FromMultiplier` 로 분해한다(+% 는 Additive 버킷).
    /// </summary>
    public sealed class DreamCocoonSystem
    {
        private readonly SimChannels _channels;
        private readonly List<SimEntityId> _remove = new List<SimEntityId>();

        public DreamCocoonSystem(SimChannels channels) => _channels = channels;

        public void Run(SimWorld world)
        {
            float dt = world.DeltaTime;
            _remove.Clear();

            foreach (SimEntityId entity in world.With<DreamCocoon>())
            {
                if (world.Has<DeadTag>(entity)) continue;
                // 구 쿼리는 `CcEffect` 버퍼를 **요구**한다 — 없으면 참여하지 않는다.
                var ccBuffer = world.GetBuffer<CcEffect>(entity);
                if (ccBuffer == null) continue;

                var cocoon = world.Get<DreamCocoon>(entity);

                bool asleep = false;
                for (int i = 0; i < ccBuffer.Count; i++)
                    if (ccBuffer[i].kind == CcKind.Sleep) { asleep = true; break; }

                // ① 파탄 — 잠이 완주 전에 사라졌다 = 피격 wake. 자연만료는 순서 핀 때문에
                //    이 분기에 도달할 수 없다.
                if (!asleep && cocoon.remaining > 0f)
                {
                    _remove.Add(entity);
                    continue;
                }

                // ② 감산 → ③ 완주.
                float rem = cocoon.remaining - dt;
                if (rem <= 0f)
                {
                    SimModifierAuthoring.FromMultiplier(cocoon.mult, out var op, out float mag);
                    _channels.StatApply.Enqueue(new StatModifierApplyEvent
                    {
                        target = entity,
                        stat = cocoon.stat,
                        op = op,
                        magnitude = mag,
                        duration = float.PositiveInfinity,
                        source = entity,
                        stackId = cocoon.stackId,
                        origin = ModifierOrigin.Dreamcatcher,
                    });
                    _remove.Add(entity);
                }
                else
                {
                    cocoon.remaining = rem;
                    world.Set(entity, cocoon);
                }
            }

            for (int i = 0; i < _remove.Count; i++) world.RemoveComponent<DreamCocoon>(_remove[i]);
        }
    }
}
