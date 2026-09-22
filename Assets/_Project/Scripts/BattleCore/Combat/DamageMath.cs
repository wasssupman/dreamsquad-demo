// salvaged from Assets/_Project/Scripts/Battle/Units/{KillAttribution, ShieldSlot}.cs
//   + Battle/Combat/{TileAoe, AoeTargetCap}.cs (battle-core-rebuild unit 3)
// 이식 시 바뀐 것: `Entity` → `SimEntityId` · `DynamicBuffer`/`NativeArray` → `List`/배열.
//   규칙(최대 피해가 킬러 · 동점은 앞 · 출처 없음은 미귀속 / 같은 출처 max · 다른 출처 합 · FIFO)은
//   그대로다. 넷을 한 파일로 모은 이유: 전부 **피해 한 번이 해결되는 동안** 쓰이고, 그 순서
//   (배율 → 흡수 → 체력 → 귀속)가 곧 계약이라 한 자리에서 읽히는 편이 낫다.
using System.Collections.Generic;
using Unity.Mathematics;

namespace Wassup.BattleCore.Combat
{
    // 「이 처치는 누구의 것인가」.
    //
    // 그 틱 피해 중 **출처가 있는 최대치**가 킬러다. 동점이면 인박스 순서 앞.
    // 출처 없음(지속 피해·배치 스킬·환경·자해)은 **미귀속** — 처치 보상이 안 난다(의도).
    public static class KillAttribution
    {
        /// <summary>한 항목을 접는다. strict `&gt;` 라 동점은 먼저 접힌 쪽이 유지된다(결정론).</summary>
        public static void Consider(float amount, SimEntityId source,
                                    ref SimEntityId bestSource, ref float bestAmount)
        {
            if (!source.IsNone && amount > bestAmount)
            {
                bestAmount = amount;
                bestSource = source;
            }
        }
    }

    // 출처별 실드 슬롯의 병합·흡수.
    //
    // 같은 출처 재부여 = `max(잔량, 새 값)`(중첩 불가) · 다른 출처는 슬롯 추가(합산).
    // `Source` 는 **중첩 키**일 뿐 수명 링크가 아니다 — 부여자가 죽어도 잔여 실드는 산다.
    public static class ShieldMath
    {
        /// <summary>같은 출처 슬롯이 있으면 max 갱신, 없으면 새 슬롯. 삽입 순서 = 부여 순서.</summary>
        public static void Merge(List<ShieldSlot> slots, SimEntityId source, float amount)
        {
            if (amount <= 0f) return;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Source != source) continue;
                var slot = slots[i];
                slot.Value = math.max(slot.Value, amount);
                slots[i] = slot;
                return;
            }
            slots.Add(new ShieldSlot { Source = source, Value = amount });
        }

        /// <summary>
        /// 오래된 슬롯(앞)부터 차감하고 소진 슬롯은 제거한다(삽입 순서 유지 = 결정론).
        /// 반환 = 실드를 뚫고 나온 **관통 피해**. 0 이면 「완전 흡수 = 피격 아님」이다.
        /// </summary>
        public static float Absorb(List<ShieldSlot> slots, float damage)
        {
            while (damage > 0f && slots.Count > 0)
            {
                var slot = slots[0];
                if (slot.Value > damage)
                {
                    slot.Value -= damage;
                    slots[0] = slot;
                    return 0f;
                }
                damage -= slot.Value;
                slots.RemoveAt(0);
            }
            return damage;
        }

        public static float Sum(List<ShieldSlot> slots)
        {
            float total = 0f;
            for (int i = 0; i < slots.Count; i++) total += slots[i].Value;
            return total;
        }

        /// <summary>특정 출처가 이미 부여한 양(없으면 0). 재부여가 no-op 인지 미리 보는 데 쓴다.</summary>
        public static float ValueFromSource(List<ShieldSlot> slots, SimEntityId source)
        {
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].Source == source) return slots[i].Value;
            return 0f;
        }
    }

    // 광역 멤버십 — 「중심에서 N칸 안인가」.
    //
    // 모양은 **모서리가 둥근 원**이고 사거리 술어와 **같은 본체**를 쓴다. 순수 원
    // (`dx²+dy² ≤ r²`)으로 썼다가 되돌린 이력이 있다 — 반경 1 폭발이 대각을 통째로 잃어
    // 십자가 됐다(대각 칸은 중심거리 1.41 > 1). 격자에서 작은 반경의 원은 그렇게 무너진다.
    public static class TileAoe
    {
        /// <summary>체비셰프 타일 거리 — **격자 통계 전용**. 광역 멤버십에 쓰지 말 것.</summary>
        public static int TileDistance(int2 a, int2 b)
            => math.max(math.abs(a.x - b.x), math.abs(a.y - b.y));

        /// <summary>
        /// **자리형** 진입점. 칸 반폭은 인자가 아니라 이 함수의 성질이라 「내 몸」 자리에
        /// 틀린 값을 넘길 수 없다 — 원점이 «유닛» 인 폭발은 여기 오면 안 된다(제약 13).
        /// </summary>
        public static bool IsInRadius(int2 candidateCell, int2 centerCell, int tileRange,
                                      float targetBodyRadiusTiles = 0f)
            => Wassup.Skills.SkillMath.ReachFromCell(
                   candidateCell.x - centerCell.x, candidateCell.y - centerCell.y,
                   tileRange, targetBodyRadiusTiles);
    }

    // 광역 피해자 상한. 이미 반경을 통과한 후보 중 **중심 거리² 오름차순**으로 최대 `cap` 개.
    // `cap <= 0` = 무제한. 동률은 인덱스 오름차순 = 결정론.
    public static class AoeTargetCap
    {
        public static int SelectNearest(float[] distanceSq, int count, int cap, int[] into)
        {
            int want = cap <= 0 ? count : math.min(cap, count);
            if (want > into.Length) want = into.Length;
            if (want <= 0) return 0;

            // 선택 정렬 — n 이 작다(반경 안 후보는 수십 개 수준).
            int picked = 0;
            for (int n = 0; n < want; n++)
            {
                int best = -1;
                float bestKey = float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    if (AlreadyPicked(into, picked, i)) continue;
                    if (distanceSq[i] < bestKey)   // strict < → 동률은 앞 인덱스 승리
                    {
                        bestKey = distanceSq[i];
                        best = i;
                    }
                }
                if (best < 0) break;
                into[picked++] = best;
            }
            return picked;
        }

        private static bool AlreadyPicked(int[] into, int count, int idx)
        {
            for (int k = 0; k < count; k++) if (into[k] == idx) return true;
            return false;
        }
    }

    // 체력 비율 — 표시와 게이트가 같은 식을 본다.
    public static class HealthMath
    {
        public static float ComputeRatio(float value, float max)
            => max > 0f ? math.saturate(value / max) : 0f;
    }
}
