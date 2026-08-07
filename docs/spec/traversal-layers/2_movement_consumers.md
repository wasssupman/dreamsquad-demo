# 2. 이동 소비처를 적별 필드로

## 목적

벽 판정·추격·순찰·보스 필드가 전부 "그 적의 필드"를 보게 한다. 벽 술어의 단일 정의(계약 4)는 유지하고 **인자만 확장**한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/MovementCellTrim.cs` — `IsWallCell`/`FillWalkMask`/`Apply` 가 필드 인덱스를 받음
- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs` — 적의 통행 마스크로 필드 선택
- `Assets/_Project/Scripts/Battle/Effects/AggroStateSystem.cs` · `Combat/AggroChaseMath.cs` · `Effects/PatrolFieldSystem.cs` · `Effects/DefenderFieldSystem.cs`
- 적 엔티티에 통행 마스크 컴포넌트(`TraversalMask : IComponentData`, Units 맥락 소유·스폰 시 1회 기록)

## 구현

1. **적 엔티티에 마스크 기록**: 스폰 시 `AttackUnitData.EffectiveTraversalLayers` → `TraversalMask` 컴포넌트(불변). 시스템은 이 값으로 필드를 고른다 — SO 를 sim 에서 다시 읽지 않는다(ECS 경계).
2. **MovementSystem**: 엔티티의 마스크로 `FieldFor(mask)` 인덱스를 얻어 flow/dist 를 읽는다. 마스크 미부착(레거시/픽스처)이면 primary 필드 — 현행 동작.
3. **IsWallCell**: 시그니처에 필드(또는 인덱스)를 받아 그 필드의 `flow==0` 으로 판정. 골 예외 규칙 그대로.
4. **어그로 추격·순찰**: `FillWalkMask` 가 그 적의 필드로 마스크를 만든다 — 지금 두 곳이 공유하는 단일 정의를 유지.
5. **보스 방어유닛 필드**(`DefenderFieldSystem`): 보스의 통행 마스크 기준으로 `walkMask` 를 만든다. 보스가 1종이면 현행과 동일.

## 완료 기준

- compile 클린. 마스크 1종 로스터에서 **이동 결과 무회귀**(EditMode 이동 테스트 전량 + Play 스모크에서 도달 시간·경로 동일).
- EditMode: 통행 층이 다른 적 2종이 같은 맵에서 **서로 다른 경로**를 타는 케이스, 마스크 미부착 엔티티의 primary 폴백.
- Play: 층 다른 적 2종이 섞인 웨이브에서 각자 자기 통행 영역만 밟는지 육안 + 콘솔 에러 0.
