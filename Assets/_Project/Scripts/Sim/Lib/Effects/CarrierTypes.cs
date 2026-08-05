namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-F/4 — 포털 링크(캐리어 엔티티). 구 `PortalLink` 이식.
    /// `MovementSystem` 이 `entryRadius` 안에 든 이동체를 `exitWorld` 로 텔레포트한다.
    /// 텔레포트 후 방향은 **다음 프레임의 flow field 가 공급**한다 — waypoint 재매핑이 없다.
    /// </summary>
    public struct PortalLink
    {
        public SimVec3 entryWorld;
        public SimVec3 exitWorld;
        public float entryRadius;
        public float remaining;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-F/4 — 토네이도(캐리어 엔티티). 구 `TornadoField` 이식.
    ///
    /// ⚠ **매 프레임 살아 있는 필드를 조회한다** — 캐스트 시점 스냅샷이 아니다.
    /// 스냅샷 방식(구 per-attacker `TornadoPull`)은 지속 중에 진입한 적을 놓쳤다.
    /// </summary>
    public struct TornadoField
    {
        public SimVec3 centerWorld;
        public int tileRange;
        public float pullSpeed;
        public float remaining;
    }
}

namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-F/4 — "도약 비행 중" 이라는 사실. 구 `LeapFlight` 이식.
    /// 의미: **공격 불가 · 자기주도 이동 불가 · 피격 가능.**
    ///
    /// ⚠ **anti-계약**: 피해 정산과 **타겟 후보 수집**에 절대 넣지 말 것. 옆의
    /// `PendingDeployment` 는 피격까지 막지만 이건 아니다 — 넣는 순간 보스가 비행 내내 무적이
    /// 된다. **비대칭이 의도다**(판 밖 존재 = 무적은 궁극기 전용 축이 담당한다).
    ///
    /// fail-open: 태그 부재 = 전부 허용. 붙이는 쪽이 누락돼도 유닛이 조용히 마비되지 않는다.
    /// </summary>
    public struct LeapFlight { }
}

namespace Wassup.Sim.Movement
{
    /// <summary>
    /// battle-sim-extraction unit 18-F/4 — Combat→Movement 텔레포트 seam. 구 `BlinkRequestEvent` 이식.
    /// **위치는 Movement 소유**라 Combat 이 직접 쓰지 못한다 — 요청을 보내고 #44 가 대입한다.
    /// 채널은 **소비자 맥락**에 둔다(`AggroHitEvents` 선례).
    /// </summary>
    public struct BlinkRequestEvent
    {
        public SimEntityId entity;
        /// 착지 셀 중심. **y 는 소비자가 현재값을 유지**한다.
        public SimVec3 destWorld;
    }
}
