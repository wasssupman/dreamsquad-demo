using System.Collections.Generic;

namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-H/3 — 보스 전용 위협 표의 한 줄. 구 `ThreatEntry` 이식.
    /// 공격한 방어유닛별 **누적** 피해이고 감쇠가 없다. 보스 스폰만 이 버퍼를 갖는다 —
    /// 일반 적은 최근접/어그로 정책을 그대로 쓴다.
    /// </summary>
    public struct ThreatEntry
    {
        public SimEntityId attacker;
        public float cumulativeDamage;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-H/3 — Combat→Combat 귀속 채널. 구 `ThreatHitEvent` 이식.
    /// ⚠ 피해자가 위협 버퍼를 **가졌고** 공격자가 살아 있는 방어유닛일 때만 나간다 —
    /// 브리지 캐스트 스킬(owner = Null)은 귀속되지 않는다.
    /// </summary>
    public struct ThreatHitEvent
    {
        /// 위협 표의 주인(보스).
        public SimEntityId victim;
        public SimEntityId attacker;
        public float amount;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-H/3 — 위협 순수 계산. 구 `ThreatTable` 이식.
    /// </summary>
    public static class ThreatTable
    {
        /// <summary>
        /// 살아 있는 공격자 중 누적 피해 최대. **동률은 낮은 simId 가 이긴다.**
        /// 표가 비었거나 산 공격자가 없으면 `Null`(호출부가 폴백한다).
        ///
        /// ⚠ 구 시그니처의 `simIds` 병렬 배열이 **사라졌다** — 구 sim 은 `Entity` 에 simId 가
        /// 없어 호출부가 나란히 실어 줘야 했지만, 신 sim 의 `SimEntityId` 가 곧 그 축이다.
        /// 동률 규칙 자체는 그대로다(생존 여부만 아키텍처 상태라 여전히 병렬 배열로 받는다).
        /// </summary>
        public static SimEntityId Leader(List<ThreatEntry> entries, List<bool> alive)
        {
            var best = SimEntityId.Null;
            float bestDamage = 0f;
            int bestSimId = int.MaxValue;
            for (int i = 0; i < entries.Count; i++)
            {
                if (!alive[i]) continue;
                var e = entries[i];
                if (e.attacker.IsNull) continue;
                if (best.IsNull
                    || e.cumulativeDamage > bestDamage
                    || (e.cumulativeDamage == bestDamage && e.attacker.Value < bestSimId))
                {
                    best = e.attacker;
                    bestDamage = e.cumulativeDamage;
                    bestSimId = e.attacker.Value;
                }
            }
            return best;
        }

        /// <summary>
        /// 생산자 쪽 게이트 + enqueue 를 한 자리에 모은다 — 착탄 지점들이 한 줄로 남고,
        /// 귀속 규칙이 바뀌면 여기 한 곳만 고친다.
        /// `credit` 는 투사체 단위 불변식(채널 존재·owner 非Null·owner 가 방어유닛)을 접은 값이고,
        /// **피해자별 버퍼 검사만** 여기서 한다.
        /// </summary>
        public static void TryCredit(SimChannel<ThreatHitEvent> channel, bool credit,
                                     SimWorld world, SimEntityId victim, SimEntityId owner, float amount)
        {
            if (!credit || !world.HasBuffer<ThreatEntry>(victim)) return;
            channel.Enqueue(new ThreatHitEvent { victim = victim, attacker = owner, amount = amount });
        }

        /// 공격자당 한 줄로 접어 누적한다(보스 수명 내내, 감쇠 없음).
        public static void Accumulate(List<ThreatEntry> table, SimEntityId attacker, float amount)
        {
            for (int i = 0; i < table.Count; i++)
            {
                if (table[i].attacker != attacker) continue;
                var e = table[i];
                e.cumulativeDamage += amount;
                table[i] = e;
                return;
            }
            table.Add(new ThreatEntry { attacker = attacker, cumulativeDamage = amount });
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-H/3 — 폭발 AoE 대상 상한 선별. 구 `AoeTargetCap` 이식.
    ///
    /// 이미 셀 범위 필터를 통과한 후보 중 착탄 중심 거리² 오름차순 최대 `cap` 개.
    /// **`cap &lt;= 0` = 무제한**(기존 메테오/스킬/보스 경로 무회귀)이고, 그때는 인덱스 순서를
    /// 그대로 돌려준다. 동률은 낮은 인덱스 — <see cref="Wassup.Sim.Effects.ShieldTargeting"/> 와
    /// 같은 선택 정렬·같은 결정론이다.
    /// </summary>
    public static class AoeTargetCap
    {
        public static void SelectNearest(List<float> distanceSq, int cap, List<int> results)
        {
            results.Clear();
            int total = distanceSq.Count;
            if (cap <= 0)
            {
                for (int i = 0; i < total; i++) results.Add(i);
                return;
            }

            int count = cap < total ? cap : total;
            if (count <= 0) return;

            var picked = new bool[total];
            for (int n = 0; n < count; n++)
            {
                int best = -1;
                float bestKey = float.MaxValue;
                for (int i = 0; i < total; i++)
                {
                    if (picked[i]) continue;
                    if (distanceSq[i] < bestKey) // strict < → 동률은 앞 인덱스
                    {
                        bestKey = distanceSq[i];
                        best = i;
                    }
                }
                if (best < 0) break;
                picked[best] = true;
                results.Add(best);
            }
        }
    }
}
