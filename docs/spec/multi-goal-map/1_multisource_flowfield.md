# 1. 멀티-소스 flow field

## 목적

`BuildFlowField` 가 단일 골 대신 **goals 전체를 소스로** flow field 를 굽는다. 엔진(`BuildFromSources`)은 이미 있으니 호출만 바꾼다. 최근접-골 라우팅이 여기서 발생.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `BuildFlowField()` (≈672–710)

## 구현

1. 기존: `var goal = _generatedMap.goal; FlowFieldBuilder.Build(walk, gridSize, goal, flow, dist);` → **GeneratedMap-레벨 폴백**(리뷰 B1) 후 소스 배열로:
   ```
   // goals 미초기화/빈 생산자(폴백/legacy)도 안전하게 커버
   NativeArray<int2> src; bool ownsSrc;
   if (_generatedMap.goals.IsCreated && _generatedMap.goals.Length > 0) {
       src = _generatedMap.goals; ownsSrc = false;   // GeneratedMap 소유 — dispose 금지
   } else {
       src = new NativeArray<int2>(1, Allocator.Temp){ [0]=_generatedMap.goal }; ownsSrc = true;
   }
   FlowFieldBuilder.BuildFromSources(walk, gridSize, src, flow, dist);
   if (ownsSrc) src.Dispose();
   ```
   **소유권 주의**: `_generatedMap.goals` 를 직접 소스로 넘길 땐 **dispose 하지 않는다**(GeneratedMap 소유). 폴백 temp 만 dispose.
2. `FlowFieldSingleton { ... goalCell = _generatedMap.goal ... }` — `goalCell` 은 **primary(goals[0])** 로 유지. (유닛 2 후 이 필드는 무참조가 되지만 무해하게 남김 — 리뷰 m2.)
3. 나머지 싱글턴 필드(dist/flow/gridSize/tileSize/origin/version) 불변.

## 계약

- **폴백이 B1 을 막는다**: goals 를 안 채우는 생산자(라이브 `BuildFallbackLinear`·legacy 4종)에서도 `[goal]` 로 필드가 정상 빌드 → 판 정지 방지. (BuildFallbackLinear 는 유닛 0 이 goals 세팅도 하지만 이 폴백이 이중 안전.)
- `BuildFromSources` 는 유효 소스(경계 내+walkable) 0개면 빈 필드(전 셀 MaxValue) — 기존 단일-무효골과 동일 fail-open. goals walkable 은 유닛 0·3 이 보장.
- 단일 골 맵: 소스 1개 → 기존 `Build` 와 동일 결과(회귀 0, 리뷰 CONFIRM).
- flow field 는 Effects 소유 — 이 유닛은 굽는 쪽(BattleBridge)만, 시스템 소비 불변. 보스-디펜더 필드는 별도 `DefenderFieldSingleton`+자체 소스라 충돌 없음(리뷰 CONFIRM).

## 완료 기준

- [x] BuildFlowField 가 goals(폴백 포함) 소스로 BuildFromSources 호출, temp 소스만 dispose(원본 goals 미dispose)
- [x] 단일골 맵 flow/dist 가 기존과 동일(회귀 스냅샷 비교)
- [x] goals 빈 생산자(폴백 경로)에서도 [goal]로 정상 필드(판 정지 없음)
- [x] 2골 임시 맵에서 각 셀 dist 가 최근접 골 기준(execute_code/EditMode 검증)
- [x] compile 0 error, EditMode green
- [x] **ecs-reviewer** 통과(flow field 굽기 경로 변경)

확인 2026-07-23 — compile 0, EditMode 1274 green(WaveForceReschedule 5건은 execute_code 잔류 실패였고 도메인 리로드 후 통과 재확인). 실증: (a) 단일 소스 BuildFromSources([goal]) == 기존 Build(goal) dist·flow 바이트 동일, (b) 양끝 2골에서 dist=최근접 골(중앙 x5=4=min(5,4), x3=3). ecs-reviewer: 소유권/double-dispose·allocator·게이트웨이 경계·예외안전 전부 SOUND, 지적 0.
