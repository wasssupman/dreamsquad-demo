using Unity.Mathematics;
using Wassup.Battle.Units;

namespace Wassup.BattleCore.Trigger
{
    // battle-core-rebuild unit 7a — 「무슨 일이 일어났나」의 **값 스냅샷**(S18 · 정정 9).
    //
    // 감지한 시점과 실행하는 시점이 다르다 — 그 사이에 시전자가 죽거나(사망 seam) 퇴근했거나(즉시 seam)
    // 대표 대상이 움직였다. 대표 대상은 타겟팅 오버라이드 사슬의 합성물이라 **나중에 다시 못 만든다**.
    // 그래서 감지자가 그 순간의 값을 싣고, 드레인은 되묻지 않는다.
    //
    // 자리↔몸은 **짝으로** 다닌다(제약 13):
    //   `SubjectPos ↔ SubjectBody`(시전자 = 그 사건의 주인) · `Site ↔ SiteBody`(사건이 일어난 자리 = 죽인 적 ·
    //   죽은 나 · 비워진 칸). **`SiteBody == 0` = 그 자리는 칸이다**(자리에 떨어지는 것) — 새 필드를 만들지
    //   않는 것이 이 형 구분의 표현이다. 퇴근 운석의 0 은 배선 누락이 아니라 의도다.
    public struct TriggerEvent
    {
        public Seam Seam;
        public TriggerKind Kind;

        /// <summary>그 사건의 주인(자기 규칙이 듣는 쪽) — 공격자 · 킬러 · 피격자 · 죽은 자 · 퇴근한 자.</summary>
        public SimEntityId Subject;
        public Faction SubjectFaction;
        public float3 SubjectPos;
        public float SubjectBody;
        /// <summary>
        /// 주인이 **이미 판에 없다**(자기 죽음 · 퇴근). 드레인이 시전자 생존을 요구하지 않고, 진영·몸은
        /// 위 스냅샷을 쓴다. 안 실으면 적의 작별 선물이 「플레이어 시전」으로 접혀 **적**을 겨눈다.
        /// </summary>
        public bool SubjectGone;
        /// <summary>주인의 체력(게이트 `Self` — 피격 N회는 **이 피격을 적용한 뒤**의 값).</summary>
        public float SubjectHp;
        public float SubjectMaxHp;

        /// <summary>사건의 대상(공격의 대표 대상). 없으면 None.</summary>
        public SimEntityId Target;
        /// <summary>대상의 체력(게이트 `EventTarget` — 처형타는 **피해 전** 값).</summary>
        public float TargetHp;
        public float TargetMaxHp;

        /// <summary>사건이 일어난 자리. 없으면 주인의 자리가 그 자리다(옛 `TargetPosition == 0` 폴백).</summary>
        public bool HasSite;
        public float3 Site;
        public float SiteBody;

        /// <summary>계산된 방향(주인 → 대상). 0 = 같은 칸 — 판정은 concrete 가 한다.</summary>
        public float2 Direction;
        /// <summary>
        /// 킬러 사양(통행 층) 스냅샷 — **감지자마다 다르다**: 공격·주기·처치 = 주인의 공격 층, 경계·피격·
        /// 파열·죽음·퇴근 = 0(무제한). 0 으로 새면 무제한 통과가 된다 — 그래서 감지자가 정한다.
        /// </summary>
        public byte TargetLayers;

        // 칸 조준(액티브 — 7b).
        public int2 CellA;
        public int2 CellB;
        public bool HasCellB;
    }

    /// <summary>줄 선 발동 하나 = (사건 스냅샷, 그 사건을 들은 규칙). 전순서 키는 (세대, 생산 순번).</summary>
    internal struct PendingFire
    {
        public TriggerEvent Evt;
        public Binding Binding;
        public int Generation;
        public int Seq;
        public int DueTick;
    }

    /// <summary>트리거 카운터의 순수 함수(← 옛 `DcTrigger`). EditMode 가 따로 고정한다.</summary>
    public static class TriggerCounters
    {
        /// <summary>N 번째마다 1회. period 0 = 영원히 안 난다(부착 검증의 순수 함수 쪽 가드).</summary>
        public static bool Tick(ref int counter, int period)
        {
            if (period <= 0) return false;
            counter++;
            if (counter < period) return false;
            counter = 0;
            return true;
        }

        /// <summary>
        /// 주기 누적 — 차면 1회 발화하고 **잔여를 이월**한다(드리프트 없음). 0 이하 주기는 발화도 누적도
        /// 안 한다(0 값 카드가 매 틱 발화하지 않게). 한 틱에 최대 1회.
        /// </summary>
        public static bool Periodic(ref float elapsed, float dt, float periodSeconds)
        {
            if (periodSeconds <= 0f) return false;
            elapsed += dt;
            if (elapsed < periodSeconds) return false;
            elapsed -= periodSeconds;
            return true;
        }

        /// <summary>
        /// 체력 경계 — 현재 체력이 다음 경계 `max·(1 − k·fraction)` **미만**이면 발화. 한 방에 여러 경계를
        /// 뚫어도 **1회**만 알리고 k 는 가장 깊은 경계까지 나간다(한 틱 다중 텔레포트 방지). k 는 단조
        /// 래치라 회복해도 되감기지 않는다(핑퐁 차단). fraction·max 가 0 이하면 안 난다.
        /// </summary>
        public static bool HealthThreshold(float hp, float maxHpRef, float fraction, ref int nextBoundary)
        {
            if (fraction <= 0f || maxHpRef <= 0f) return false;
            bool fired = false;
            while (hp < maxHpRef * (1f - nextBoundary * fraction))
            {
                nextBoundary++;
                fired = true;
            }
            return fired;
        }
    }
}
