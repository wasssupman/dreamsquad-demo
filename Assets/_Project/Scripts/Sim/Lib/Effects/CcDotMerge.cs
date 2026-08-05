using System.Collections.Generic;

namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-D — `CcEffect` 버퍼 병합의 **단일 정책**. 구 `CcEffectMerge` 이식.
    ///
    /// 병합 키 = **`kind`**(비스택: 같은 kind 는 슬롯 1개). `remainingTime` 은 max,
    /// `vector`/`scalar`/`tickInterval` 은 incoming 으로 갱신.
    ///
    /// ⚠ **`tickTimer` 는 보존한다** — 매 프레임 존 refresh 에도 리셋하지 않는다. incoming 의
    /// `tickTimer` 는 무시한다. 주기가 바뀌면 "다음 tick 까지 진행률" 을 **값 비례로 환산**해서
    /// 넘긴다 — 큰 주기에서 쌓인 timer 를 작은 주기에 그대로 주면 조기 발동한다.
    /// 신규 슬롯은 **첫 tick 즉발**(`tickTimer = tickInterval`).
    /// </summary>
    public static class CcEffectMerge
    {
        public static void Apply(List<CcEffect> buffer, CcEffect incoming)
        {
            for (int i = 0; i < buffer.Count; i++)
            {
                if (buffer[i].kind != incoming.kind) continue;

                CcEffect slot = buffer[i];
                float carriedTimer = slot.tickTimer;
                if (slot.tickInterval > 0f && incoming.tickInterval > 0f
                    && slot.tickInterval != incoming.tickInterval)
                {
                    carriedTimer = slot.tickTimer / slot.tickInterval * incoming.tickInterval;
                }

                buffer[i] = new CcEffect
                {
                    kind = incoming.kind,
                    vector = incoming.vector,
                    scalar = incoming.scalar,
                    remainingTime = SimMath.Max(slot.remainingTime, incoming.remainingTime),
                    tickInterval = incoming.tickInterval,
                    tickTimer = carriedTimer,
                };
                return;
            }

            incoming.tickTimer = incoming.tickInterval;
            buffer.Add(incoming);
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-D — `DotEffect` 병합의 단일 정책. 구 `DotEffectMerge` 이식.
    ///
    /// 병합 키 = **`(origin, element)` 2축**. 둘 중 하나만 달라도 슬롯이 갈리므로
    /// 출혈(Stack·Bleed)과 화염 장판(Zone·Fire)이 각자의 scalar·주기로 동시에 타고,
    /// 장판 화염(Zone·Fire)과 화염 스택 폭발(Stack·Fire)도 서로를 덮지 않는다.
    /// 한 슬롯을 공유하던 시절엔 나중에 온 쪽이 scalar·tickInterval 을 덮고 remainingTime 만
    /// max 로 남아, **장판을 나가도 장판 요율로 계속 타는 과피해**가 났다.
    ///
    /// 같은 키끼리는 병합된다 — 난도질꾼 2기가 한 적을 물어도 출혈은 합산되지 않는다(사양).
    ///
    /// `tickTimer` 취급은 <see cref="CcEffectMerge"/> 와 같다. **두 정책을 합치지 않는다** —
    /// 키가 다르고(1축 vs 2축) 필드 집합이 다르며, 구 sim 도 별개 파일로 유지했다.
    /// </summary>
    public static class DotEffectMerge
    {
        public static void Apply(List<DotEffect> buffer, DotEffect incoming)
        {
            for (int i = 0; i < buffer.Count; i++)
            {
                if (buffer[i].origin != incoming.origin) continue;
                if (buffer[i].element != incoming.element) continue;

                DotEffect slot = buffer[i];
                float carriedTimer = slot.tickTimer;
                if (slot.tickInterval > 0f && incoming.tickInterval > 0f
                    && slot.tickInterval != incoming.tickInterval)
                {
                    carriedTimer = slot.tickTimer / slot.tickInterval * incoming.tickInterval;
                }

                buffer[i] = new DotEffect
                {
                    origin = incoming.origin,
                    element = incoming.element,
                    scalar = incoming.scalar,
                    tickInterval = incoming.tickInterval,
                    tickTimer = carriedTimer,
                    remainingTime = SimMath.Max(slot.remainingTime, incoming.remainingTime),
                };
                return;
            }

            incoming.tickTimer = incoming.tickInterval;
            buffer.Add(incoming);
        }
    }
}
