using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // ECS mirror of DefenderUnitData's CC fields. Written once at spawn by
    // BattleBridge; read by AttackSystem (knockback, Unit 5) and the on-place
    // dispatch (push, Unit 6). All fields default 0 → existing behavior unchanged.
    public struct DefenderCcData : IComponentData
    {
        public float knockbackDistance;
        public float knockbackDuration;
        public float onPlacePushDistance;
        public float onPlacePushDuration;
        public float onPlacePushRadius;
        // sleep-fighter-defender — RESOLVE 주 타겟에게 Sleep N초. 0 = disabled.
        public float sleepOnHitSec;
        // knockup-fighter-defender — 히트한 **전 대상**에게 Stun N초(공중 띄우기의 심 실체).
        // 스코프가 sleepOnHitSec(주 타겟 1체)과 다르다 — 둘을 하나로 합치면 투머치토커가 깨진다.
        public float knockupOnHitSec;
        // 띄우기 연출 최고 높이(view 공간). 심은 이 값을 **읽지도 쓰지도 않고** 그대로 실어
        // 보내기만 한다 — 순수 뷰 값이 ECS 를 경유하는 형태는 `ProjectileRef.visualScale` 선례와
        // 같다. 값 자체가 아키텍처를 모르고 뷰만 해석한다(제약 10 의 shape).
        public float knockupVisualHeight;
    }
}
