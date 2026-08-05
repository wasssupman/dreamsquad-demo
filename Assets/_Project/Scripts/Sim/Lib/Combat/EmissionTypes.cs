namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-H/4 — 대상 선택 규칙. 구 `PatternSelectionRule` 이식.
    /// ⚠ append-only, 값 고정(저작 복제 — <see cref="DcTriggerKind"/> 와 같은 사정).
    /// </summary>
    public enum PatternSelectionRule : byte
    {
        RoundRobin = 0,
        DeterministicShuffle = 1,
        /// 대상 선택을 하지 않는 방향 발사.
        None = 2,
    }

    /// <summary>
    /// battle-sim-extraction unit 18-H/4 — 한 발의 명세. 구 `PatternShotSpec` 이식.
    /// </summary>
    public struct PatternShotSpec
    {
        /// 패턴 min/max 각도 안의 정규화 위치. 실제 회전은 <see cref="PatternDirection"/> 몫.
        public float directionT;
        /// 직전 탄과의 간격. ⚠ **첫 탄은 트리거 프레임에 나가므로 index 0 값은 무시된다.**
        public float intervalAfterPreviousSec;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-H/4 — 발사 패턴 명세. 구 `PatternSpec` 이식.
    ///
    /// ⚠ 구 `shots` 는 `FixedList128Bytes`(**값 타입**)라 `PatternSpec` 을 복사하면 발 목록까지
    /// 복사됐다. 신 sim 의 배열은 참조라 그 성질이 **자동으로 오지 않는다** —
    /// <see cref="PatternShotRandomizer.Apply"/> 가 제자리 수정 대신 **새 배열을 만드는** 이유다.
    /// 이 struct 를 복사한 뒤 `shots[i]` 를 직접 고치면 원본 슬롯까지 오염된다.
    ///
    /// 저작 에셋 참조는 정수 핸들(`barrelDataIndex`)로 치환돼 있고 그 해석은 아키텍처 몫이다 —
    /// 이 struct 자체는 어느 아키텍처도 모른다.
    /// </summary>
    public struct PatternSpec
    {
        public int barrelDataIndex;
        public float damage;
        public PatternSelectionRule selection;
        public float minAngleDeg;
        public float maxAngleDeg;
        /// **트리거당 발수의 단일 source of truth.**
        public PatternShotSpec[] shots;
        public bool randomizeShotsPerTrigger;
        public float randomIntervalMinSec;
        public float randomIntervalMaxSec;
        /// 발마다 대상을 다시 뽑는가(산개) / 첫 대상에 집중하는가.
        /// ⚠ **잠금 신원 자체는 여기 없다** — 그건 아키텍처 바인딩이고 순수 계층은 "재추첨하는가" 만 답한다.
        public bool reselectPerShot;
        /// 스카이폴 예고 초. 저작 탄 데이터에 대응 필드가 없는 유일한 값이라 패턴이 소유한다.
        public float telegraphSec;

        public int ShotCount => shots?.Length ?? 0;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-H/4 — 발사 인스턴스의 **순수** 스케줄 상태.
    /// 구 `EmitterRuntime` 이식. 아키텍처 타입을 참조하지 않는다.
    /// </summary>
    public struct EmitterRuntime
    {
        /// 버스트가 아직 빚진 발수.
        public int burstRemaining;
        /// 다음 발까지 남은 초. 음수 잔여를 이월해 드리프트가 0 이다.
        public float timer;
        /// <summary>
        /// 선택 규칙의 결정론 소스. ⚠ 인스턴스는 트리거마다 생겼다 사라지는 transient 라
        /// **0 에서 시작하면 RoundRobin 이 영원히 같은 대상**을 고른다 — 영속 카운터는
        /// durable 소유자(<see cref="PatternSlot"/>)가 들고 `Begin` 이 시드로 받는다.
        /// </summary>
        public int fireCount;
        /// 현재 버스트 내 순번(베지어 스윙 소스).
        public int shotIndex;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-H/4 — 진행 중인 한 번의 발사. 구 `EmitterInstance` 이식.
    ///
    /// ⚠ `spec`·`template` 은 **시작 시점 스냅샷**이다 — 발사 도중 저작/버프가 바뀌어도
    /// 7번째 탄이 1번째와 달라지지 않는다.
    ///
    /// ⚠ `lockedTarget` 은 **index 가 아니라 엔티티**다. 후보 스냅샷은 프레임-로컬이라
    /// index 를 잠그면 프레임을 넘는 버스트에서 같은 index 가 다른 유닛을 가리킨다.
    /// </summary>
    public struct EmitterInstance
    {
        public PatternSpec spec;
        public EmitterRuntime runtime;
        /// 대상 의존 필드(target/impact/swingIndex)만 비어 있고 emitter 가 발마다 채운다.
        public ProjectileSpawnRequest template;
        public SimEntityId lockedTarget;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-H/4 — host 에 부착된 발사 명세 원본. 구 `PatternSlot` 이식.
    /// 트리거 슬롯은 여기로의 index 만 들고 실제 spec/template 은 이 버퍼에 산다.
    ///
    /// ⚠ <see cref="fireCountBase"/> 가 이 타입의 존재 이유 중 하나다 — 위 `EmitterRuntime.fireCount`
    /// 주석 참조. 카운터는 여기 남고 인스턴스는 시드만 받는다.
    /// </summary>
    public struct PatternSlot
    {
        public PatternSpec spec;
        public ProjectileSpawnRequest template;
        public int fireCountBase;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-H/4 — 한 발의 발사 명령. 구 `ShotOrder` 이식.
    /// 로직이 만들고 아키텍처가 소비한다.
    ///
    /// ⚠ **대상을 엔티티가 아니라 후보 배열 index 로 가리킨다** — 순수 계층이 아키텍처 신원을
    /// 모르기 위해서다(`ThreatTable.Leader` 의 alive 병렬 배열과 같은 관용구).
    /// </summary>
    public struct ShotOrder
    {
        public int shotIndex;
        /// &lt; 0 = 후보 없음(발사를 소모하고 건너뛴다).
        public int targetCandidateIndex;
        public float damage;
        public int barrelDataIndex;
        public float telegraphSec;
        public float directionT;
    }

    /// 발사 시점에 궤적이 요구하는 바인딩. emitter 가 알아야 하는 것은 이 셋뿐이다.
    public enum BindingClass : byte { Entity, Cell, Direction }

    /// <summary>
    /// battle-sim-extraction unit 18-H/4 — `MovementKind` → 바인딩 분류. 구 `MovementBinding` 이식.
    ///
    /// **emitter 의 분기 축이 이것**이지 개별 궤적이 아니다 — 궤적마다 분기하면 새 이동 수학이
    /// 생길 때마다 emitter 가 자란다. 기존 바인딩으로 분류되는 새 궤적은 emitter 변경 0 으로 발사된다.
    /// </summary>
    public static class MovementBinding
    {
        /// <summary>
        /// ⚠ C# 은 enum switch 의 전수성을 강제하지 못한다 — 분류 누락은 **테스트가** 잡는다.
        /// 새 `MovementKind` 를 추가하면 이 상수와 실제 개수가 어긋나 테스트가 깨지고,
        /// 그때 아래 분류를 갱신하게 된다.
        /// </summary>
        public const int KnownKindCount = 6;

        public static BindingClass Of(MovementKind kind)
        {
            switch (kind)
            {
                case MovementKind.HomingToEntity:
                case MovementKind.BezierHomingToEntity:
                    return BindingClass.Entity;

                case MovementKind.BallisticArcToPoint:
                case MovementKind.SkyFall:
                case MovementKind.GrenadeToCell:
                    return BindingClass.Cell;

                case MovementKind.DirectionalLinear:
                    return BindingClass.Direction;

                default:
                    // 미분류는 **미개통으로 흐른다** — emitter 가 발사를 소모하고 넘어간다.
                    // 조용한 오발사보다 눈에 보이는 무발사가 낫다.
                    return BindingClass.Direction;
            }
        }
    }
}
