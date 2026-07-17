# unit-overhead-ui — 방어/적 공통 머리 위 UI

> 상태: 구현 완료, Play 시각 검증 대기 2026-07-18. 기존 `unit-health-display` / `unit-dreamcatcher-icons`는 Legacy 경로로 보존한다.

## 목표

방어유닛과 적의 체력 표기를 하나의 `UnitOverheadView` 구조로 통일한다. 체력바는 유닛 실루엣 상단에서
1920×1080 reference pixel 기준 5px 위에 상시 표시한다. 방어유닛은 체력바 위 5px에, 한 타일의
화면 투영 폭 안으로 최대 3장의 부착 드림캐쳐 미니 카드를 표시한다.

`ui_mockup.png`는 방향 참고만 사용하고 실제 BattleScene 캡처를 최우선 기준으로 삼는다.
축소된 Game View에서도 외곽선이 남도록 2px대 네이비/와인 프레임, 드롭섀도, 상단 하이라이트를 사용한다.

## 작업 단위

| # | 문서 | 작업 |
|---|---|---|
| 0 | `0_contract_and_layout.md` | reference pixel 좌표/레이아웃 순수 계산과 스타일 계약 |
| 1 | `1_unified_health_view.md` | 공통 Layer/View + 방어/적 스타일 변형 |
| 2 | `2_bridge_legacy_switch.md` | BattleBridge Legacy/Unified 상호배타 라우팅 |
| 3 | `3_dreamcatcher_row.md` | 방어유닛 전용 최대 3장 행, 기존 부착 registry 재사용 |
| 4 | `4_wiring_validation.md` | 씬/SO 배선, EditMode·Play·Android 검증 |

## Feature-wide 계약

- `BattleBridge`만 ECS `Health`를 read-only로 읽는다. Presentation은 plain ratio만 소비한다.
- `UnitHealthPresentationMode.Legacy/UnifiedOverhead`는 상호 배타적이다. `Both` 모드는 만들지 않는다.
- 레거시 SO·스포너·타일 게이지·마이크로바·드림캐쳐 스트립은 삭제하지 않는다.
- 5px은 BattleScene CanvasScaler와 같은 1920×1080 reference pixel, height-match 기준이다.
- 기준점은 Transform 루트가 아니라 실제 Renderer 화면 Bounds의 top-center다.
- 체력바 bottom은 실루엣 top보다 5px, 드림캐쳐 row bottom은 체력바 top보다 5px 위다.
- 방어 체력바는 한 타일 투영 폭의 88%, 적은 74%. Style SO의 min/max 폭으로 안전하게 clamp한다.
- 가로 중심은 무기까지 포함한 Renderer Bounds가 아니라 유닛 visual pivot, 세로는 실제 Renderer top을 사용한다.
- 드림캐쳐 미니 카드는 기준 높이 28.8px, 카드 간격 4px로 배치하며 3장 전체 폭은 타일 영역 안에서 자동 축소한다.
- 드림캐쳐 행은 방어유닛만. BountyMark 적은 `Marked` StatusFx만 표시한다.
- 부착 source of truth는 `DreamcatcherHandController._attachedTo`; `AttachmentsChanged` 이벤트 구동을 유지한다.
- 체력바는 상시 표시하되 만피 alpha를 낮춰 클러터를 줄인다.
- 과한 금속 장식·3D 판타지 프레임·네온은 금지. 현 게임의 flat-shaded cartoon outline을 유지한다.

## 파이프라인 커버리지

| 정거장 | UnifiedOverhead |
|---|---|
| 데이터 SO | `UnitOverheadUiStyle` |
| ECS 상태 | 기존 `Health` read-only, 신규 컴포넌트 없음 |
| 생성/갱신 | `BattleBridge.SyncMonoUnitViews` → `UnitOverheadUiLayer.SetUnit` |
| Mono 이벤트 | `DreamcatcherHandController.AttachmentsChanged` |
| View/Pool | entity별 `UnitOverheadView`, Layer pool |
| 씬 배선 | BattleBridge mode/layer + Layer hand/style |
| teardown | `UnitOverheadUiLayer.Clear()` |

## 비목표

- StatusFx 전체를 새 레이아웃으로 이관하지 않는다. 이번 unit에서는 충돌 없는 오프셋만 검증한다.
- HP 숫자, 이름표, 보스 전용 프레임, 클릭 상세 UI는 추가하지 않는다.
- 기존 `DreamcatcherCard.art`를 즉시 대체하지 않는다. 전용 icon은 후속이며 art 폴백을 유지한다.
