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

        // ⚠ **어느 드레인 지점이 이걸 실행하나**(skill-layer-migration unit 3e).
        //
        // 다섯 seam 이 이 큐 하나를 나눠 쓴다. 예전엔 「자기 순서에 큐에 있는 것 전부」를
        // 가져갔고, 그래서 **소유가 시스템 업데이트 순서에서 창발**했다 — 감지자가 시뮬
        // 안에 있는 동안은 우연히 맞았지만 두 가지가 무너진다:
        //   ① 경계 시스템이 파괴보다 뒤라 자기 죽음 이벤트를 경계 seam 이 집어갔다
        //      (폭발은 터지는데 계측이 엉뚱한 seam 에 찍혀 그물이 seam 을 못 짚는다)
        //   ② **시뮬 밖 생산자는 seam 을 고를 방법이 아예 없다** — 퇴근은 브리지 발이라
        //      프레임 첫 seam 이 집어가고, 그 seam 의 「시전자 생존」 가드에 걸려 버려진다.
        //
        // 그래서 **생산자가 자기 seam 을 말한다.** 남의 것을 집으면 큐 뒤로 돌려보낸다.
        public SkillSeam Seam;

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
        // unit 5b — 대상 수 상한(0 = 상한 없음)과 자기 포함 여부.
        // ⚠ **자기 포함이 별도 축인 이유**: 같은 filter 라도 카드 경로(악몽의 가호)는
        // 제외, 능력 경로(실드 셔틀)는 포함이다. filter 로 접으면 그 차이가 사라진다.
        public int Count;
        public bool IncludesSelf;
        public int StackId;
        public float VisualScale;
        // 발사 명세 슬롯 index. `DataIndex`(전역 에셋 표)와 **다른 축**이다 —
        // 이쪽은 host 자기 `PatternSlot` 버퍼의 자리다. −1 = 없음.
        public int PatternIndex;
        // 저작 스탯 축(`StatKind`). `Selector`(cc/stack)와 **다른 축**이라 겸직시키지 않는다 —
        // 한 슬롯이 둘 다 필요한 스킬이 나오는 순간 조용히 갈린다.
        public int StatSelector;
        // 저작 스택 축(`StackKind`). `Selector`(cc) · `StatSelector` 와 **또 다른 축**이다 —
        // 한 슬롯이 셋을 다 쓰는 스킬이 나오는 순간 겸직은 조용히 갈린다.
        public int StackSelector;
        // 저작 탄 궤적 축. **도메인은 이 값을 해석하지 않는다** — `DataIndex` 와 같은
        // 성격의 불투명 토큰이고, 뜻은 어댑터와 저작 계층만 안다.
        public int ProjectileMovement;
        public int ProjectilePayload;
        // 해저드 저작 index(`DataIndex` 와 다른 표). −1 = 없음.
        public int HazardDataIndex;
    }

    // 채널 수명주기는 `BattleBridge` 소유다 — 생성 Persistent / 싱글턴 파괴 / Dispose
    // 3점 세트. 하우스 패턴은 `DcTriggerFiredEvents` 와 같다.
    public struct SkillFiredEventsSingleton : IComponentData
    {
        public NativeQueue<SkillFiredEvent> queue;
    }
}
