# unit 7 — 스웜 기준 웨이포인트 스폰 가이드

## 목적

스폰 예고선과 실제 이동선을 일치시킨다. 같은 웨이브에 여러 스웜이 있을 때 레인 하나로
접지 않고, **스웜 × 실제 스폰 레인**마다 자기 적의 웨이포인트 경로를 예고한다.

현재 웨이브 모델의 `WaveSpawnGroup(unit,count,triggerOffsetSec)`을 스웜 식별 단위로 사용한다.
미래 스웜 저작 기능·고유 ID·레인 고정 규칙은 만들지 않는다. 이후 전용 ID가 생기면 예보 키만
교체하고, 가이드와 실제 스폰이 같은 펼침 결과를 읽는 구조는 유지한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/GeneratedWavePlan.cs` — 가이드 예보 plain 값 계약
- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — 실제 스폰 펼침과 스웜 식별을 한 계산에서 생산
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 큐잉된 웨이브의 가이드 예보 + 경로 조립 API
- `Assets/_Project/Scripts/Presentation/SpawnAlertPresenter.cs` — lane 배열을 다중 guide 표시로 전환
- `Assets/_Project/Tests/EditMode/` + PlayMode 라이브 확인
- `Assets/_Project/Scripts/Data/Decks/Deck_WaypointLab.asset` — 생성 웨이브 체감 덱
- `Assets/_Project/Data/Enemies/Enemy_WaypointBasicAlt.asset` — 두 번째 경로를 쓰는 주황 검증 적

## 구현

### 예보 단위

`SpawnGuideForecast`는 `swarmIndex`, `laneIndex`, `firstSpawnSec`, `waypointPathIndex`,
`traversalLayers`를 갖는다. 경로를 그리는 데 필요하지 않은 SO 참조는 Presentation으로 넘기지 않는다.

- 같은 스웜이 여러 레인에 실제 배정되면 레인마다 출발점이 다르므로 가이드도 여러 개다.
- 같은 레인에 여러 스웜이 배정되면 각각 별도 가이드다. lane 기준 최소 시각으로 접지 않는다.
- 경로 미저작(`-1`) 또는 런타임 무효 인덱스는 골 슬롯으로 안전 폴백한다.

### 예보와 실제 스폰의 단일 정본

`ExpandWave`와 예보가 RoundRobin/PerGroupTimeline/lane 산식을 따로 재구현하지 않는다.
그룹 인덱스와 확정 lane을 보존한 상세 펼침 결과를 한 번 만들고, 실제 `_pending`과 가이드 예보가 함께 소비한다.
`SpawnUnit`에서 lane 공식을 다시 계산하지 않는다.
강제 웨이브·Wave 1·상한 진행도 모두 기존 `QueueWave` 한 진입점을 유지한다.

### 경로 조립

`TryGetSpawnPathSim`은 `(lane, waypointPathIndex, traversalLayers)`를 받아 다음 구간을 이어 붙인다.

```
spawn → waypoint[0] → ... → waypoint[N-1] → goal
```

각 구간은 해당 `(목적지, 통행층)` flow 슬롯과 기존 `PathSmoothing.TryStepTarget`을 사용한다.
방향·평활화·장애물 판정을 새로 구현하지 않는다. 경로 없는 적은 기존 골 가이드와 동일하다.

### 체감 검증 콘텐츠

로비 맵 오버라이드에서 `D1:MovementLab`을 고르고 일반 매치를 시작한다. 이 맵 전용
`Deck_WaypointLab`은 경로 0의 청록 스웜과 경로 1의 주황 스웜만 생성한다. 첫 스폰 2.5초 전부터
스웜별 실제 레인 가이드가 나타나며, 각 색 적이 자기 가이드와 같은 웨이포인트 순서로 이동해야 한다.
검증 덱은 dev encounter에만 연결하고 프로덕션 랜덤 풀에는 넣지 않는다.

## 완료 기준

- [x] 컴파일 에러 0 · EditMode 전량 그린
- [x] 같은 레인의 서로 다른 두 스웜이 예보 2개로 유지(레인 최소값 병합 금지)
- [x] 같은 스웜이 여러 레인에 배정되면 실제 레인별 가이드 생성
- [x] 웨이포인트 가이드가 저작 순서대로 지점을 지나 골까지 이어짐
- [x] 미저작 적 가이드는 기존 스폰→골 경로와 동일
- [x] Play: 여러 스웜의 가이드가 각 실제 이동선과 일치하고 잔상 없이 종료

완료 확인: 2026-08-11 — 사용자 D1 MovementLab 플레이 확인. 스웜×실제 lane 예보,
활성 LineRenderer, 경로 0·1 실이동 일치를 PlayMode 1/1로 고정했다. EditMode 2,140건
중 실패 0. 리뷰에서 lane 최소값 호환 API와 `ExpandWaveDetailed` 이중 API를 제거하고,
확정 lane을 상세 펼침 한 번에서 실제 스폰·가이드가 함께 소비하도록 단일화했다.
