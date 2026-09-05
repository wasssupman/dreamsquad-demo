using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // enemy-detection-range unit 1 — **감지 반경**(칸). Combat 소유, 스폰 시 1회 베이크되고
    // 전투 중 불변이다. `DetectionSystem` 이 읽고, `MovementSystem` 은 유출/공성 면제 판정에
    // `tiles < 0` 만 읽는다(RO).
    //
    // ⚠ **부착 자체가 게이트다.** `detectionRange == 0` 인 적에게는 이 컴포넌트를 붙이지
    // 않는다 — 그 부재가 곧 「오늘과 같은 경로」이고, 분기 하나 대신 아키타입으로 가른다.
    //
    // `tiles < 0` = **무제한**(구 `huntsDefenders`). 보스 3종 + `Enemy_DreamShard` 가 그 값이다.
    // 무제한만이 leak-proof(골 셀을 밟아도 공성 전환 안 함)를 갖는다 — 유한 반경 감지는
    // 골 전환을 건드리지 않는다. 그 분리가 없으면 감지가 이 게임의 유일한 패배 통로
    // (골 → 마음 HP → 스트레스 100 → 남은 시간 몰수)의 조절기가 된다(unit 3).
    public struct DetectionRange : IComponentData
    {
        public float tiles;

        public bool Unlimited => tiles < 0f;
    }
}
