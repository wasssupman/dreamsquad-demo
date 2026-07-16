# 3. 살찌운 제물 — 적 타겟 드래그 커밋 + 표식 인디케이터

## 목적

unit 2 의 메커니즘을 실제 조작으로 연결한다: 손패에서 카드를 끌어 **움직이는 악몽 위에 드롭**하면 최근접 적을 픽해 표식을 커밋하고, 표식 상태를 눈에 보이게 한다(기존 StatusFx 아키타입 재사용 — 신규 렌더 정거장 없음).

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` — 적-타겟 카드 판별 + 적 픽 드롭 경로
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (또는 partial) — `TryPickNearestEnemy(Vector3 worldPos, float maxRadius, out Entity enemy)` 픽 API
- StatusFx 계열 — `StatusFxKind.Marked` append + `StatusFxRegistry` 항목 + bridge 표식/해제 훅 (`unit-status-fx` 인프라)
- 씬 wiring — registry 프리팹 슬롯 배정 (unity-feature-wiring 스킬 절차)
- Play 검증

## 구현

**적-타겟 카드 판별** — 카드 `mechanics` 에 `BountyMark` payload 포함 여부로 판별(순수 데이터 판독, 신규 필드 없음). 판별되면 드래그 드롭이 타일/디펜더 대신 적 픽 경로를 탄다.

**적 픽** — 드롭 월드 좌표 기준 최근접 적, 반경은 SO 튜닝 노브(AwakeningConfig 또는 카드 duration 필드 재사용 — 구현 시 택1, 하드코딩 금지). 픽 실패(반경 내 적 없음) = 커밋 안 함: 카드 손패 잔류 + 게이지 무차감 (contract 9). bridge 픽 API 는 ECS 조회의 유일 창구 규칙에 따라 BattleBridge 에 둔다.

**커밋** — `DreamcatcherHandController.CommitMarkEnemy(entryId, enemy)` (unit 2) 호출. `TargetArrow` 는 기존 재사용 — 적색 틴트 등 시각 구분은 선택(비차단).

**인디케이터** —
- 표식 성공 시 bridge 가 `StatusFxSpawner` 로 `Marked` FX 를 적 뷰에 attach.
- `EnemyGone`(처치/유출) 시 해제. 적 뷰 소멸과 FX 수명이 어긋나지 않게 기존 StatusFx 사망 회수 경로 확인.
- MVP 프리팹 = 기존 폴백 아이콘 계열(어그로 "!" 선례)로 충분. 전용 현상금 문양 프리팹은 후속 후보.

**경계**:
- 슬로모(손패 조작 중) 상태에서 픽이 이동 중인 적을 상대로도 안정적인지 Play 로 확인 — 픽은 커밋 순간의 스냅샷이며 이후 이동은 무관.
- 기존 드롭 경로(부착/Active 타일·디펜더·포탈) 무회귀가 이 unit 의 최우선 검증 항목.

## 완료 기준

- [ ] compile 0 에러
- [ ] 에디터 Play: ① 드래그→적 드롭→표식 커밋 + 게이지 차감 + 인디케이터 표시 ② 처치/유출 시 인디케이터 소멸 + 카드 큐 복귀 ③ 반경 내 적 없는 드롭 → 무차감 + 카드 잔류
- [ ] 기존 카드(부착/Active) 드래그 경로 전부 무회귀 (Play 확인)
- [ ] 씬 wiring 완료 상태로 커밋 (registry 슬롯 미배정 금지)
