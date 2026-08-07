# 1. 통행 마스크별 flow field

## 목적

통행 집합이 다르면 경로가 다르다. 지금은 전 적이 공유하는 field 1개라, 적별 통행 층이 의미를 가지려면 **마스크 값마다 field 를 따로** 만들어야 한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/FlowFieldSingleton.cs` — 필드 집합화
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `BuildFlowField`(로스터에서 마스크 수집 → 마스크별 BFS), `TeardownFlowField`
- `Assets/_Project/Tests/EditMode/` — 수집 결정론·필드 내용

## 구현

1. **로스터 수집**: 이 매치에 등장 가능한 적(`_resolvedDeck` 의 웨이브 스케줄)에서 `EffectiveTraversalLayers` 값 집합을 모아 **오름차순 정렬**(결정론, 계약 3). 1종이면 지금과 동일한 field 1개다. 3종 초과면 경고(계약 7).
2. **마스크별 BFS**: 각 마스크 m 에 대해 `walk_m[i] = (TraverseLayersAt(i) & m) != 0` 을 만들고 기존 `FlowFieldBuilder.BuildFromSources(walk_m, …, goals, flow_m, dist_m)`. 골 집합은 공통.
3. **싱글턴**: `FlowFieldSingleton` 이 `flow/dist` 단일 쌍 대신 **마스크 정렬 배열 + per-mask flow/dist** 를 갖는다. 소비자는 `FieldFor(mask)` 로 인덱스를 얻는다(선형 탐색, 종류 ≤3). 기존 단일 필드 소비자를 위해 **primary(첫 마스크) 접근자**를 남겨 픽스처·비-적 소비자(예: 골 도달 판정)가 깨지지 않게 한다.
4. **수명**: 전부 `Allocator.Persistent`, `TeardownFlowField` 가 배열 전체 dispose. 부분 실패 시 이미 만든 필드도 회수(누수 금지).
5. **스폰·골 통행 보장**(계약 6): 각 마스크 필드에서 스폰이 골에 도달 못 하면 **에러 로그 + 그 마스크는 primary 필드로 폴백**한다(적이 영원히 제자리인 판을 만들지 않는다).

## 완료 기준

- compile 클린. 마스크 1종(현행 로스터)에서 field 내용이 이 유닛 이전과 **바이트 동일**(회귀 방지 축).
- EditMode: 마스크 수집이 로스터 순서와 무관하게 오름차순 결정론, 마스크 2종이면 field 2개·각각 자기 walk 집합으로 BFS, 도달 실패 마스크의 폴백, dispose 누수 없음.
