using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // CcEffect 버퍼 병합의 단일 정책(source of truth). CcApplySystem(이벤트 드레인)과
    // EffectSpawner(bridge 직접 적용)가 모두 이 헬퍼를 통한다 — 두 경로의 정책 갈라짐 방지.
    //
    // 병합 키 = kind (비스택: 같은 kind 는 슬롯 1개). merge 시 remainingTime=max, vector/
    // scalar/tickInterval 은 incoming 으로 갱신. tickTimer(이산 tick 누적기)는 보존하되,
    // tickInterval 이 바뀌면 진행률을 새 주기로 환산한다(Fire↔Poison 겹침 전환에서 조기/
    // 이중 tick 방지 — dot-tick-cadence). add 시 첫 tick 즉발(tickTimer=tickInterval).
    public static class CcEffectMerge
    {
        public static void Apply(ref DynamicBuffer<CcEffect> buffer, CcEffect incoming)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].kind != incoming.kind) continue;

                var slot = buffer[i];
                // 누적기 보존: 매 프레임 존 refresh 에도 리셋 금지(계약 4). incoming.tickTimer 무시.
                float carriedTimer = slot.tickTimer;
                // 주기가 바뀌면 "다음 tick 까지 진행률"을 보존(값 비례 환산) — 큰 주기에서
                // 쌓인 timer 를 작은 주기에 그대로 넘겨 조기 발동시키지 않는다.
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
                    remainingTime = math.max(slot.remainingTime, incoming.remainingTime),
                    tickInterval = incoming.tickInterval,
                    tickTimer = carriedTimer,
                };
                return;
            }

            // 신규 슬롯: 첫 tick 즉발(진입 즉시 1회). 연속(tickInterval=0)이면 무영향.
            incoming.tickTimer = incoming.tickInterval;
            buffer.Add(incoming);
        }
    }
}
