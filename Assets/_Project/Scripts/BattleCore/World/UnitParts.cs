using Unity.Mathematics;
using Wassup.BattleCore.Move;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 2 — 개체의 **이동·감지·어그로·순찰·점유** 부분.
    //
    // 전부 nullable 부분 객체(UML §2 의 `o--`)다. 「컴포넌트가 있나」 분기를 정책 값이나
    // null 로 표현하는 것이 옛 아키타입 게이트의 후계다 — 옛 전투는 `detectionRange == 0` 이면
    // 컴포넌트를 **안 붙여** 분기 하나를 아키타입으로 갈랐고, 그 규칙 문장(**「감지 0 = 오늘과
    // 같은 경로」**)을 명시로 남기지 않으면 의미가 소멸한다.
    //
    // ⚠ 부분이 늘면 `Unit.Reset` 도 같이 고친다. 안 고치면 전 판의 값이 새 개체에 새어
    // 들어오고, 그 버그는 결정론 테스트에서만 «가끔» 보인다.

    /// <summary>교전 중 이동 정책. 저작 값이라 코어가 기본값을 고르지 않는다.</summary>
    public enum EngageMovement : byte { Halt = 0, Advance = 1, Pulse = 2 }

    // 이동체의 상태. 방어유닛에는 없다(움직이지 않는다).
    public sealed class MoveState
    {
        /// <summary>몸 반지름(칸). 통과 여유 &lt; 밀어냄 폭이면 교착이라 **군집 통과로 검산한다.**</summary>
        public float Radius = 0.25f;

        public float Speed;

        /// <summary>이 유닛이 지나갈 수 있는 층 비트. 0 = 미주입 → 기본 마스크로 읽는다.</summary>
        public byte TraversalLayers;

        /// <summary>마지막 자기주도 진행 방향. 넉백 반사·연출이 읽는다.</summary>
        public float2 LastMoveDir;

        /// <summary>
        /// 「코어가 이 유닛을 멈췄다」. **케이스 열거가 아니라 결과 관찰**이다 —
        /// 진입 시 true, **실제로 움직인 지점에서만** false. 새 이탈 경로가 생겨도 자동으로
        /// 정지에 편입된다(열거식이면 분기가 늘 때마다 조용히 샌다). CC 잠금도 함께 접는다.
        /// </summary>
        public bool HoldingGround = true;

        // 경로 진행. `PathIndex` 는 스폰 시 `WaypointRouting.ResolvePathIndex` 가 정한다.
        public int PathIndex = -1;
        public int WaypointIndex;

        /// <summary>골 도달은 **1회 고정**이다. 붙으면 이동 루프에서 빠진다.</summary>
        public bool PastGoal;

        /// <summary>거점 목적지(있으면). 웨이포인트 **뒤**에 오는 스텝 소스다.</summary>
        public bool HasStructureDest;
        public int2 StructureDest;

        /// <summary>교전 중 이동 정책(저작).</summary>
        public EngageMovement Engage = EngageMovement.Halt;

        /// <summary>이번 틱에 쌓인 외력(넉백 등). **상태와 무관하게** 적용되고 소비 후 지워진다.</summary>
        public float3 PendingImpulse;

        /// <summary>순간이동 요청. 위치는 이동이 소유하므로 **여기서만** 쓴다.</summary>
        public bool HasBlink;
        public float3 BlinkTo;

        /// <summary>자기주도 이동 잠금(CC·도약 비행). 외력은 이 게이트 밖이다.</summary>
        public bool Locked;

        /// <summary>순찰 유닛의 이번 틱 자기주도 방향. zero = 정지.</summary>
        public float2 PatrolStep;

        public void Reset()
        {
            Radius = 0.25f;
            Speed = 0f;
            TraversalLayers = 0;
            LastMoveDir = float2.zero;
            HoldingGround = true;
            PathIndex = -1;
            WaypointIndex = 0;
            PastGoal = false;
            HasStructureDest = false;
            StructureDest = int2.zero;
            Engage = EngageMovement.Halt;
            PendingImpulse = float3.zero;
            HasBlink = false;
            BlinkTo = float3.zero;
            Locked = false;
            PatrolStep = float2.zero;
        }
    }

    // 감지 상태.
    //
    // ⚠ **네 박자 상수는 코드 상수다**(M9). 노브로 올리면 정의표 해시가 움직여 골든 빨강이
    // 「조건 드리프트」로 읽힌다. 그리고 **표식 쿨(6) &gt; 억제(5)** 관계 자체가 계약이다 —
    // 뒤집히면 표식이 억제 창 안에서 두 번 난다.
    public sealed class Detection
    {
        /// <summary>0 = 감지 없음 · &gt;0 = 반경(칸) · &lt;0 = 무제한.</summary>
        public float Range;

        public bool Unlimited => Range < 0f;

        /// <summary>지금 무언가를 물고 있나. 0→1 전이에서만 발견 사건이 난다.</summary>
        public bool Hunting;

        /// <summary>물고 있는 대상. 관성 중에는 비어 있을 수 있다.</summary>
        public SimEntityId Target = SimEntityId.None;

        /// <summary>관성 — 대상을 잃어도 잠깐 더 문 것으로 친다.</summary>
        public float Grace;

        /// <summary>막힘 누적 — 「사냥 중인데 못 가고 있다」가 이어지면 놓는다.</summary>
        public float Stuck;

        /// <summary>억제 — 막힘으로 놓은 직후 재감지 금지. 상태와 무관하게 흐른다.</summary>
        public float Suppress;

        /// <summary>표식 쿨 — 발견 사건의 최소 간격.</summary>
        public float MarkCooldown;

        /// <summary>유한 감지가 쓰는 대상 지향 추격판. 무제한은 공용 사냥판을 탄다.</summary>
        public ChaseFieldCache Chase;

        public void Reset()
        {
            Range = 0f;
            Hunting = false;
            Target = SimEntityId.None;
            Grace = 0f;
            Stuck = 0f;
            Suppress = 0f;
            MarkCooldown = 0f;
            Chase = null;
        }
    }

    // 어그로. 한 타입이 두 역할을 맡는다 — 가디언은 `Capacity`/`Held` 를, 끌려간 적은
    // `Target`/`Remaining` 을 쓴다(UML §2 가 한 상자로 그린 이유).
    public sealed class Aggro
    {
        /// <summary>가디언이 동시에 붙들 수 있는 수. 적 쪽에서는 의미 없다.</summary>
        public int Capacity;

        /// <summary>지금 붙들고 있는 수. 매 틱 full recompute 라 drift 가 없다.</summary>
        public int Held;

        /// <summary>끌려간 대상(가디언). 적 쪽에서만 의미 있다.</summary>
        public SimEntityId Target = SimEntityId.None;

        /// <summary>
        /// 남은 시간. **0 = 무기한 센티널**(히트 획득). 시한 도발만 감소한다 — 뒤집지 말 것.
        /// </summary>
        public float Remaining;

        /// <summary>도발로 붙었나. 도발은 **수용량과 선점 둘을 우회**한다(나중에 부른 쪽이 이긴다).</summary>
        public bool Taunted;

        /// <summary>가디언 인접 칸까지의 추격판. 획득 시 1회 굽고 장애물이 바뀌면 무효화된다.</summary>
        public ChaseFieldCache Chase;

        public void Reset()
        {
            Capacity = 0;
            Held = 0;
            Target = SimEntityId.None;
            Remaining = 0f;
            Taunted = false;
            Chase = null;
        }
    }

    // 순찰 소환물. 자기 거점 박스 안에서만 돈다.
    //
    // ⚠ **골 판정도 함께 갈아탄다** — 박스 안에 골 칸이 들어와도 `PastGoal` 이 안 붙는다.
    // 붙으면 삼중 동결이 난다: ⑴ 이동 루프에서 빠져 영구 동결 ⑵ 골 도달 파괴 루프는 적만
    // 대상으로 해서 파괴도 안 됨 ⑶ 살아 있으니 소환사가 남은 판 내내 재소환하지 못한다.
    public sealed class Patrol
    {
        /// <summary>박스 중심(소환사 칸). 구역 판정·사격 위치 수집의 기준.</summary>
        public int2 Anchor;

        /// <summary>대기·복귀 칸. 「여기 서 있으면 정지」의 기준 — 중심과 **다른 칸이다.**</summary>
        public int2 Home;

        public int Radius = 1;

        /// <summary>소환사. 죽으면 소환물도 소멸한다.</summary>
        public SimEntityId SummonedBy = SimEntityId.None;

        public void Reset()
        {
            Anchor = int2.zero;
            Home = int2.zero;
            Radius = 1;
            SummonedBy = SimEntityId.None;
        }
    }

    // 배치 유닛의 점유. **앵커는 min 코너**이고 대표 칸은 없다 — 짝수 변엔 중심 칸이 없어서
    // 대표 칸은 정수 나눗셈 동전 던지기였고 사거리를 반 칸 옮겼다.
    public sealed class Footprint
    {
        public int2 Anchor;
        public int Width = 1;
        public int Height = 1;

        /// <summary>발밑 = 유닛이 서는 점 = 하단 행 가로 중앙. 사거리 원점·몸 원이 전부 이 점이다.</summary>
        public float3 FootPosition(float tileSize)
            => new float3((Anchor.x + (Width - 1) * 0.5f) * tileSize, 0f, Anchor.y * tileSize);

        public void Reset()
        {
            Anchor = int2.zero;
            Width = 1;
            Height = 1;
        }
    }
}
