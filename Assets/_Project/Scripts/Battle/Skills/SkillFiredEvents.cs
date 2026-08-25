using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Skills
{
    // skill-layer-foundation unit 4 — 감지(Burst)와 실행(managed concrete)을 잇는 채널.
    //
    // ⚠ **값 스냅샷이다.** 슬롯을 드레인 시점에 재독하면 안 된다. unit 0 이 4영역
    // 전수를 읽어 「드레인 시점 재질의로 대체 가능」이 **0건**임을 확인했다:
    //
    //   · 죽음 계열은 드레인 시점에 host 가 **이미 없다**. 코드가 그 이유를 적어 뒀다 —
    //     "통행 층은 killer 가 살아 있는 지금 읽는다", "the entity is gone before the
    //     bridge drains", "bake it into the event BEFORE ecb destroys the entity"
    //   · RESOLVE 계열의 `bestTarget` 은 9단계 오버라이드의 합성물이라 재현이 불가능하다
    //     (최근접 → 힐러 재랭킹 → priority → 적 락 → 어그로 → frontmost → 지속 락 →
    //      커밋 유지 → facing)
    //
    // 그래서 발화한 쪽이 그 순간의 값을 실어 보낸다.
    public struct SkillFiredEvent
    {
        public Entity Caster;          // 무효 가능 — 플레이어 시전(액티브)
        public int SkillId;            // 0 = legacy arm. 감지측 Burst 가 아는 유일한 키
        public int SlotIndex;          // 로그·중복 판별용. **params 의 출처가 아니다**

        // 발화 시점 스냅샷 ─────────────────────────────────────────
        public float3 FiredPosition;   // host 위치. 드레인 시점엔 이동·사망했을 수 있다
        public Entity Target;          // bestTarget. 재도출하면 타겟팅 규칙을 복제하게 된다
        public float3 TargetPosition;
        public float2 DirectionXZ;     // 넉백·브레스가 쓰는 **계산된** 방향
        public byte TargetTraversalLayers; // killer 사양 — 0 으로 새면 무제한 통과가 된다

        // params 값 스냅샷 ────────────────────────────────────────
        public float Magnitude;
        public float Duration;
        public int TileRange;
        public int Period;
        public int DataIndex;
        public int Selector;
        public float Speed;
        public float HitThreshold;
        public float SlamDamage;
        public int SlamTileRange;
        public int StackId;
    }

    // 채널 수명주기는 `BattleBridge` 소유다 — 생성 Persistent / 싱글턴 파괴 / Dispose
    // 3점 세트. 하우스 패턴은 `DcTriggerFiredEvents` 와 같다.
    public struct SkillFiredEventsSingleton : IComponentData
    {
        public NativeQueue<SkillFiredEvent> queue;
    }
}
