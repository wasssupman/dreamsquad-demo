# 6. Leak Investigation

## 목적

`BattleBridge.StartBattle()` 반복 호출 시 간헐적으로 뜨는 `Leak Detected : Persistent allocates ...` 경고의 원인을 찾고 해결한다. 반복 검증 루프가 안정화되지 않으면 이후 모든 시각/배치 변경의 재현성이 흔들린다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (맵 재생성 흐름)
- `Assets/_Project/Scripts/Data/GeneratedMap.cs` (Dispose 경로)
- `Assets/_Project/Scripts/Data/BackgroundPropPlacer.cs` (NativeArray lifecycle)
- 필요 시 ECS World 재초기화 경로
- 테스트: `Assets/_Project/Tests/EditMode/` 또는 PlayMode 반복 재현 하나

## 구현 가이드

1. 재현 경로 고정
   - Play 모드에서 `BattleBridge.StartBattle()` 을 30~100 회 자동 호출하는 debug 헬퍼 추가 (커밋 전 제거).
   - 또는 PlayMode test 로 같은 시나리오 반복 실행.
2. `NativeLeakDetection.Mode = Full` 설정으로 정확한 stack trace 확보.
3. 우선 의심 후보 점검:
   - 이전 `GeneratedMap.tiles / spawns` 의 `NativeArray<T>` 가 Dispose 되지 않고 새 맵으로 덮이는 경로.
   - `BackgroundPropPlacer.Generate` 의 `occupied`, `visited` (현재 Temp 할당이지만 예외 경로에서 finally 도달 여부 확인).
   - Entities ECS `World` 또는 `EntityManager` 재시작 시점에 남는 Persistent 할당.
4. 원인 수정 후 반복 테스트로 leak 0 건 확인.
5. debug 헬퍼 제거.

## 완료 기준

- `NativeLeakDetection.Mode = Full` 상태에서 `StartBattle` 100 회 반복 시 Console 경고 0.
- 기존 EditMode / PlayMode 테스트 전원 통과.
- 재발 방지용 Dispose 경로가 코드에 한 줄 이상 주석 또는 명시적 Dispose 로 문서화됨 (코멘트 과다 금지, 필요한 한 줄만).
- handoff 에 적힌 leak warning 항목이 해소됨.

## 주의

- 본 단계에서 시각/배치 로직은 건드리지 않는다. 원인 파일만 수정.
- `NativeLeakDetection.Mode` 는 디버깅 중 임시 설정. 최종 커밋에는 남기지 않는다.

확인 일자: 2026-04-24 / 커밋 해시: 2db71b9
