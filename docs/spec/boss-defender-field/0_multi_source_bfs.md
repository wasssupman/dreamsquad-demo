# 0 — Multi-source BFS 순수함수 + 테스트

## 목적

`FlowFieldBuilder` 에 N-소스 BFS 를 추가한다. 기존 단일-goal `Build` 는 1-소스 특수형으로 위임해 로직 중복 0. 방어유닛(벽 셀) → walkable 이웃 소스 수집 헬퍼도 여기 순수 계층에 둔다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/FlowFieldBuilder.cs`
- `Assets/_Project/Tests/EditMode/FlowFieldBuilderTests.cs`

## 구현

1. `public static void BuildFromSources(NativeArray<byte> walkMask, int2 gridSize, NativeArray<int2> sources, NativeArray<float2> outFlow, NativeArray<int> outDist)`
   - dist=MaxValue/flow=0 초기화는 기존과 동일.
   - 모든 유효 소스(in-bounds && walkable)를 dist 0 으로 seed 후 동일 BFS. 유효 소스 0 개면 초기화 상태 그대로 반환(전부 MaxValue — 계약 5 의 fallback 신호).
   - flow 채움 패스는 기존 코드 그대로 (dist 기반, `Dirs` 순서 타이브레이크 → 소스 순서 무관 결정론).
2. 기존 `Build(walk, gridSize, goal, flow, dist)` 는 `BuildFromSources` 에 goal 1개로 위임. 기존 semantics(경계 밖/벽 goal → 빈 필드) 는 "유효 소스 0" 규칙과 동치 — 기존 `FlowFieldBuilderTests` 무수정 통과가 이를 증명.
3. `public static int CollectDefenderSources(NativeArray<byte> walkMask, int2 gridSize, NativeArray<int2> defenderCells, NativeList<int2> outSources)`
   - 각 방어유닛 셀의 4-이웃 중 walkable 만 추가, 반환값 = 소스 수. 중복 셀 허용(BFS 에 무해 — dist 0 재삽입은 no-op).

## 완료 기준

- compile 클린.
- 기존 `FlowFieldBuilderTests` 전체 무수정 통과 (위임 무회귀).
- 신규 EditMode 테스트:
  - 소스 2개 → 각 셀 dist 가 두 소스 중 최근접 거리, flow 를 따라가면 최근접 소스 도착.
  - 소스 0개(빈 배열/전부 벽 셀 이웃 없음) → 전 셀 MaxValue, flow 전부 zero.
  - `CollectDefenderSources`: 벽 셀 방어유닛의 walkable 이웃만 수집, 이웃 0 방어유닛은 무기여.
  - 벽 너머 소스(도달불가) → 해당 컴포넌트 셀만 유한, 나머지 MaxValue.

확인 2026-07-11 · 커밋 `dc298ceb` (EditMode 신규 6종 + 기존 4종 무수정 통과)
