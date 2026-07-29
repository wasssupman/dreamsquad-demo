using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // dot-effect-extraction unit 0 — 지속 피해 병합 정책의 단일 소스.
    //
    // 키는 **(origin, element)** 다. 둘 중 하나만 달라도 슬롯이 갈리므로
    //  - 출혈(Stack·Bleed) 과 화염 장판(Zone·Fire) 이 각자의 scalar·주기로 동시에 타고
    //  - 장판 화염(Zone·Fire) 과 화염 스택 폭발(Stack·Fire) 도 서로를 덮지 않는다.
    // 한 슬롯을 공유하던 시절엔 나중에 온 쪽이 scalar·tickInterval 을 덮어쓰고 remainingTime 만
    // max 로 남아, 장판을 나가도 장판 요율로 계속 타는 과피해가 났다.
    //
    // 같은 키끼리는 병합된다 — 난도질꾼 2기가 한 적을 물어도 출혈은 합산되지 않는다(사양).
    public static class DotEffectMerge
    {
        public static void Apply(ref DynamicBuffer<DotEffect> buffer, DotEffect incoming)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].origin != incoming.origin) continue;
                if (buffer[i].element != incoming.element) continue;

                var slot = buffer[i];
                // 누적기 보존: 매 프레임 존 refresh 에도 리셋 금지. incoming.tickTimer 는 무시한다.
                float carriedTimer = slot.tickTimer;
                // 주기가 바뀌면 "다음 tick 까지 진행률"을 값 비례로 환산 — 큰 주기에서 쌓인 timer 를
                // 작은 주기에 그대로 넘겨 조기 발동시키지 않는다.
                if (slot.tickInterval > 0f && incoming.tickInterval > 0f
                    && slot.tickInterval != incoming.tickInterval)
                {
                    carriedTimer = slot.tickTimer / slot.tickInterval * incoming.tickInterval;
                }

                buffer[i] = new DotEffect
                {
                    origin        = incoming.origin,
                    element       = incoming.element,
                    scalar        = incoming.scalar,
                    tickInterval  = incoming.tickInterval,
                    tickTimer     = carriedTimer,
                    remainingTime = math.max(slot.remainingTime, incoming.remainingTime),
                };
                return;
            }

            // 신규 슬롯: 첫 tick 즉발(진입 즉시 1회). 연속(tickInterval=0)이면 무영향.
            incoming.tickTimer = incoming.tickInterval;
            buffer.Add(incoming);
        }
    }
}
