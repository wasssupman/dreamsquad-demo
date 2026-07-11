# 1 — DefenderFieldSingleton + 매 프레임 재빌드 시스템

## 목적

방어유닛-지향 필드의 저장소(싱글톤)와 유일 writer(Effects ISystem)를 만든다. 이 unit 까지는 시뮬 동작 변화 0 — 필드가 만들어질 뿐 아무도 안 읽는다(소비는 unit 2).

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/DefenderFieldSingleton.cs` (신규)
- `Assets/_Project/Scripts/Battle/Effects/DefenderFieldSystem.cs` (신규)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (생성/teardown)

## 구현

1. **`DefenderFieldSingleton : IComponentData`** — `FlowFieldSingleton` 미러 + walkMask:
   - `NativeArray<byte> walkMask` / `NativeArray<float2> flow` / `NativeArray<int> dist` (전부 Persistent), `int2 gridSize`, `float tileSize`, `float3 origin`. `Dispose()` 패턴 동일.
   - walkMask 는 goal field 가 저장 안 하는 값이라 여기서 보유 — BFS 순회와 소스 수집이 쓴다.
2. **BattleBridge**: `BuildFlowField()` 에서 goal field 생성 직후 defender field 싱글톤도 생성 — 이미 만든 `walk` 배열을 Persistent 로 복사, flow/dist 는 초기값(빌드는 시스템 몫). `TeardownFlowField()` 에 dispose+destroy 추가 (기존 멱등 패턴 그대로).
3. **`DefenderFieldSystem : ISystem`** (Effects, Burst):
   - `[UpdateInGroup(typeof(BattleSimGroup))] [UpdateBefore(typeof(MovementSystem))]` (Combat 의 `EnemyAiStateSystem` 이 이미 쓰는 cross-context 순서 선언 선례).
   - `RequireForUpdate<DefenderFieldSingleton>`.
   - 매 프레임: 방어유닛 쿼리 `WithAll<FactionTag, Health, LocalTransform>().WithNone<PendingDeployment, DeadTag>()` + `faction == Faction.Defender` 필터(FSM 후보 풀과 동일 조건) → 셀 변환(`GridMath.WorldToCell`) → `CollectDefenderSources` → `BuildFromSources` 로 싱글톤 배열 in-place 갱신. 구조 변경 0.
   - 방어유닛 0 → `BuildFromSources` 가 전 셀 MaxValue 로 리셋 (stale 필드 잔존 불가).

## 완료 기준

- compile 클린, 기존 EditMode 전체 무회귀.
- Play smoke: 전투 시작/재시작(redraft) 반복에 Persistent leak 경고 없음 (teardown 멱등).
- 시각/동작 변화 없음 확인 (아직 소비자 없음).

확인 2026-07-11 · 커밋 `dc298ceb` (Play 에서 배치 직후 소스 dist 0 확인, 판 재시작 다회 leak/에러 없음)
