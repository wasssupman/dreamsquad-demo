// salvaged from Assets/_Project/Scripts/Battle/Effects/{CcEffectMerge, DotEffectMerge}.cs
//   (battle-core-rebuild unit 6a) — 병합 정책의 **단일 소스**. 두 파일이 같은 규칙을 복사해
//   갖고 있던 것을 하나로 접었다(둘이 갈리면 「존은 되는데 카드는 안 되는」 차이가 난다).
using Unity.Mathematics;

namespace Somnia.Battle.BattleCore.Effects
{
    public static class CcMerge
    {
        /// <summary>
        /// 주기가 바뀔 때 **「다음 틱까지의 진행률」을 새 주기로 비례 환산**한다(F6).
        ///
        /// 안 하면 큰 주기에서 쌓인 타이머가 작은 주기로 그대로 넘어가 **조기 발동**한다
        /// (화염 장판 → 독 장판으로 갈아탈 때 실제로 그랬다). 어느 한쪽이 연속(주기 0)이면
        /// 환산할 진행률이 없으므로 그대로 둔다.
        /// </summary>
        public static float CarryTimer(float timer, float oldPeriod, float newPeriod)
        {
            if (oldPeriod <= 0f || newPeriod <= 0f || oldPeriod == newPeriod) return timer;
            return timer / oldPeriod * newPeriod;
        }

        /// <summary>
        /// 군중 제어 슬롯의 병합. **시간은 긴 쪽, 벡터·출처는 들어온 값.**
        ///
        /// ⚠ 종류당 슬롯 하나다 — 기절 둘을 겹쳐 쌓지 않는다. 「가장 긴 것 하나」가
        /// 행동 불능의 정답이고, 그것이 지속 피해(출처별 공존)와 정반대인 지점이다.
        /// </summary>
        public static CcSlot Merge(in CcSlot slot, float seconds, float3 vector, SimEntityId source)
            => new CcSlot
            {
                Active = true,
                Remaining = slot.Active ? math.max(slot.Remaining, seconds) : seconds,
                Vector = vector,
                Source = source,
            };
    }
}
