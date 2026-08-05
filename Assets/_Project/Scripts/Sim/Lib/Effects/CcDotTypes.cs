namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-D — wake-on-hit 요청. 구 `CcClearRequest` 이식.
    /// Units(피해 정산) → Effects(CC 해제) seam 의 페이로드.
    /// </summary>
    public struct CcClearRequest
    {
        public SimEntityId entity;
        public CcKind kind;
    }

    /// 해저드 런타임 로그의 종류. 구 `HazardRuntimeEventType` 이식. ⚠ append-only.
    public enum HazardRuntimeEventType : byte
    {
        ZoneApply = 0,
        DotDamage = 1,
    }

    /// <summary>
    /// 해저드 런타임 로그 1건. 구 `HazardRuntimeEvent` 이식(`int2` → <see cref="SimInt2"/>).
    /// **상태 해시에 실리지 않는다** — 규칙이 아니라 관측이다.
    /// </summary>
    public struct HazardRuntimeEvent
    {
        public HazardRuntimeEventType eventType;
        /// 로그 태그로만 남은 값(저작 토큰과 동일) — `CcKind.DoT` 가 여기 쓰인다.
        public CcKind kind;
        public SimInt2 cell;
        public SimEntityId target;
        public float scalar;
        public float amount;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-D — "행동 불가(공격+이동 정지)" 판정 단일 소스.
    /// 구 `CcActionLock` 이식. 순수 함수 — 소비자(공격·이동)는 <see cref="CcEffect"/> 를
    /// **읽기만** 해서 게이트한다.
    /// </summary>
    public static class CcActionLock
    {
        /// lock-set 단일 소스. 새 lock 종류는 **여기만** 추가한다.
        public static bool IsLock(CcKind kind) => kind == CcKind.Stun || kind == CcKind.Sleep;

        public static bool IsLocked(System.Collections.Generic.List<CcEffect> buffer)
        {
            if (buffer == null) return false;
            for (int i = 0; i < buffer.Count; i++)
                if (IsLock(buffer[i].kind)) return true;
            return false;
        }

        /// <summary>
        /// 보스 CC 면역. **행동정지(Stun/Sleep)와 넉백(Impulse)을 막는다 — 출처 불문.**
        /// lock-set 을 <see cref="IsLock"/> 에서 조회하므로 새 lock 종류가 추가되면 면역이 자동 동행한다.
        ///
        /// ⚠ 한때 `직접 출처` 조건이 앞에 있어 스택 임계발 CC 는 통과했다. 그 근거
        /// ("DoT 가 CcEffect 버퍼를 공유하니 kind 로만 막으면 스택 DoT 까지 죽는다")는
        /// DoT 가 전용 채널로 빠지며 사라졌고, 축은 면역에 구멍만 유지하고 있었다.
        /// 스택 카드는 여전히 보스전에서 산다 — 감속은 StatModifier, DoT 는 DotApply 라
        /// 둘 다 이 술어를 지나지 않는다.
        /// </summary>
        public static bool IsBossImmune(CcKind kind) => IsLock(kind) || kind == CcKind.Impulse;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-D — DoT 이산 tick 누적 산식. 구 `DotTick` 이식.
    /// plain 값 입출력, 아키텍처-blind(제약 10 모범).
    /// </summary>
    public static class DotTick
    {
        /// 안전 상한 — 극단적 dt/미세 interval 에서의 무한 루프 방지. 결정론 유지, 실사용 도달 불가.
        public const int MaxTicksPerFrame = 1024;

        /// <summary>
        /// `tickTimer` 를 `dt` 만큼 진행하고 이번 프레임 지급할 청크 수를 반환한다.
        /// `tickInterval &lt;= 0` 은 연속 DoT 전제(호출측이 별도 처리) — 0 을 돌려주고 timer 는 불변.
        /// </summary>
        public static int Advance(ref float tickTimer, float tickInterval, float dt)
        {
            if (tickInterval <= 0f) return 0;

            tickTimer += dt;
            int ticks = 0;
            while (tickTimer >= tickInterval && ticks < MaxTicksPerFrame)
            {
                tickTimer -= tickInterval;
                ticks++;
            }
            return ticks;
        }
    }
}
