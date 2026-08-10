using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // Phase 9 — Effects 맥락이 소유하는 정적 flow field.
    // Allocator.Persistent 로 할당, 판 종료 / OnDestroy 에서 dispose.
    // Movement 맥락이 읽기 전용으로 consume.
    public struct FlowFieldSingleton : IComponentData
    {
        // traversal-layers unit 1a — 라우팅은 **슬롯별 flat stride** 다: `[slot * CellCount + cell]`.
        // 슬롯 = 통행 마스크 하나. 지금은 슬롯 1개뿐이라 인덱스가 셀 인덱스와 같지만,
        // **직접 인덱싱하지 말고 `FlowSlot(slot)` / `DistSlot(slot)` 뷰를 쓸 것** — 슬롯이
        // 늘어나는 순간(unit 1b) 직접 인덱싱은 조용히 다른 슬롯을 읽는다.
        //
        // ⚠ 왜 싱글턴을 여러 개로 쪼개지 않았나: `SystemAPI.GetSingleton<T>()` 는 매치가
        // 2개 이상이면 throw 한다. 이 컴포넌트 소비처 15곳 중 11곳은 라우팅을 아예 안 읽고
        // tileSize/gridSize/origin(=기하)만 읽는다. 그래서 **기하는 1벌, 라우팅만 N벌**이며
        // 둘은 한 컴포넌트 안에 산다. (`NativeArray<NativeArray<T>>` 는 불법이라 stride 다.)
        public NativeArray<float2> flow;        // [MaskCount * CellCount] 단위 방향. goal = zero.
        public NativeArray<int>    dist;        // [MaskCount * CellCount] 골까지 남은 비용. 도달불가 = int.MaxValue.
        // 슬롯 → 그 슬롯이 라우팅하는 통행 마스크 값. **오름차순**(결정론, 계약 5).
        // 미생성 = 슬롯 1개(primary). IsCreated 불변식에는 넣지 않는다(픽스처 보호).
        public NativeArray<byte>   maskValues;
        // continuous-agent-movement unit 1 — 정적 walk 마스크(1 = tiles == Walk)의 단일 소유자.
        // 이전엔 DefenderFieldSingleton 이 들고 있었다(그쪽 주석: "goal field 가 저장하지 않는 값").
        // 정적 벽은 goal field 가 정본이므로 이리로 옮겼고, DefenderFieldSystem 은 읽기만 한다.
        // ⚠ 두 싱글턴이 같은 배열을 들면 double dispose 로 죽는다 — 소유·해제는 여기 하나뿐.
        // IsCreated 불변식에는 넣지 않는다(goals 와 같은 이유 — 픽스처가 뒤집히는 걸 막는다).
        public NativeArray<byte>   walkMask;
        // traversal-layers unit 0 — 셀이 여는 층 비트(`PlacementLayer`: Ground = 배치지면,
        // Path = 경로). `walkMask` 와 같은 수명·같은 길이다.
        //
        // ⚠ 이름은 `placeMask` 에서 왔지만 **배치 전용 데이터가 아니다.** `PlacementLayer.cs:8`
        // 이 "층 이름은 **공간** 기준(어떤 종류의 칸인가)이지 유닛 클래스 기준이 아니다" 라고
        // 못박고 있고, 통행 판정도 같은 비트를 읽는다(rev 2 계약 1 — 셀 배열을 두 벌 만들지
        // 않는다). walkMask 가 "걸을 수 있나" 1비트라면 이쪽은 "어떤 종류의 칸인가" 전체다.
        //
        // 소비자는 traversal-layers unit 2b 부터. unit 0 은 값을 넘기기만 한다.
        // IsCreated 불변식에는 넣지 않는다(walkMask·goals 와 같은 이유 — 픽스처 보호).
        public NativeArray<byte>   cellLayers;
        // continuous-agent-movement unit 5 (D1-b) — 필드가 마지막으로 반영한 차단 셀 집합의
        // 시그니처. FlowFieldRebuildSystem 이 유일 writer. 0 = 장애물 없음.
        public uint                blockedSignature;
        public int2                gridSize;    // (Width, Height)
        public int2                goalCell;    // 픽스처 폴백 전용 기준(goals 미설정 시 IsGoalCell 이 이걸 씀).
                                                // 프로덕션 BuildFlowField 는 goals 를 항상 채우므로 여기선 읽히지 않음.
        public NativeArray<int2>   goals;       // multi-goal-map — 골 집합. 미설정 시 goalCell 폴백.
        public float               tileSize;
        public float3              origin;      // board 월드 원점 (Tilemap 모드 = zero 고정). map-origin-placement.
        public int                 version;     // 디버그 / Phase 10 event-driven rebuild 마커

        public bool IsCreated => flow.IsCreated && dist.IsCreated;

        // ── traversal-layers unit 1a — 슬롯 접근 ────────────────────────────────
        // CellCount 를 필드로 두지 않고 gridSize 에서 파생한다 — 직접 초기화하는 EditMode
        // 픽스처 수십 개가 새 필드를 안 채워도 그대로 서게 하기 위함이다.
        public int CellCount => gridSize.x * gridSize.y;
        public int MaskCount => (flow.IsCreated && CellCount > 0) ? flow.Length / CellCount : 1;

        public const int PrimarySlot = 0;

        // 슬롯 → 그 슬롯의 통행 마스크. maskValues 미생성(픽스처)이면 기본 슬롯 마스크.
        public byte MaskAt(int slot)
            => (maskValues.IsCreated && slot < maskValues.Length)
                ? maskValues[slot] : TraversalSlots.DefaultMask;

        // 유닛의 통행 층 → 슬롯. 못 찾으면 primary(현행 동작) — 마스크 미부착 엔티티와
        // 픽스처가 그대로 돈다.
        public int SlotFor(byte unitLayers)
        {
            if (!maskValues.IsCreated) return PrimarySlot;
            for (int i = 0; i < maskValues.Length; i++)
                if (maskValues[i] == unitLayers) return i;
            return PrimarySlot;
        }

        // 슬롯의 라우팅을 **길이 CellCount 인 뷰**로 준다. 소비자(순수 함수 포함)는 stride 를
        // 모른 채 0..n-1 로 인덱싱하면 된다.
        public NativeArray<float2> FlowSlot(int slot) => flow.GetSubArray(slot * CellCount, CellCount);
        public NativeArray<int>    DistSlot(int slot) => dist.GetSubArray(slot * CellCount, CellCount);

        // multi-goal-map 유닛 2 — 셀이 골인가. goals 설정 시 멤버십(1~4 소량 루프),
        // 아니면 primary goalCell 폴백. goals 를 안 채우는 EditMode 픽스처는 기존 단일-goalCell
        // 동작 그대로 유지 → 도달/wall예외/해저드검증이 골 개수에 무관해진다.
        public bool IsGoalCell(int2 cell)
        {
            if (goals.IsCreated && goals.Length > 0)
            {
                for (int i = 0; i < goals.Length; i++)
                    if (goals[i].Equals(cell)) return true;
                return false;
            }
            return cell.Equals(goalCell);
        }

        public void Dispose()
        {
            if (flow.IsCreated) flow.Dispose();
            if (dist.IsCreated) dist.Dispose();
            if (goals.IsCreated) goals.Dispose();
            if (walkMask.IsCreated) walkMask.Dispose();
            if (cellLayers.IsCreated) cellLayers.Dispose();
            if (maskValues.IsCreated) maskValues.Dispose();
        }
    }
}
