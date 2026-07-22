# unit-overhead-ui — 방어/적 공통 머리 위 UI

> 상태: 완료 2026-07-18 · 구현 커밋 `780810a1`. 기존 `unit-health-display` / `unit-dreamcatcher-icons`는 Legacy 경로로 보존한다.
> **확장 진행 2026-07-22**: 드림캐쳐 행 위에 **스택 이상효과 아이콘 행**(피로도/열기 등 + 카운트) 추가 — unit 6~. 아래 [확장 섹션](#확장-스택-이상효과-아이콘-행-2026-07-22) 참조.

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
| 5 | `5_handoff_summary.md` | 구현 커밋·검증·잔여 실기기 확인 인계 |

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

---

## 확장: 스택 이상효과 아이콘 행 (2026-07-22)

**상태**: 초안 2026-07-22 — unit 6~. 기존 오버헤드 UI(체력바 → 드림캐쳐 행) 위에 **스택 이상효과 아이콘 행**을 추가한다. 지금까지 피로도·열기 같은 스택 이상효과는 시각화가 없었다(열기는 스택슬롯도 없음). StatusFx(몸통 VFX)는 구조가 달라 재사용하지 않는다(사용자 결정).

### 목표

유닛 머리 위 UI 최상단에 **활성 스택 이상효과를 아이콘 + 카운트 배지**로 표기한다. 아이콘은 종류(피로도/열기/…)별, 배지는 현재 스택 수. `ShowCards`(드림캐쳐 행) 패턴을 미러한 `ShowStacks` 행으로, DC 행 위에 배치.

### 소스 = 듀얼 수집 (A, 사용자 결정)

- **StackModifierSlot** 기반 스택(피로도·블리드 등): `StackKind` + `stackCount` (Effects 소유, RO).
- **HeatAccrual**(열기): `stacks` (Effects 소유, RO — 스택슬롯 아님). 열기 설계는 불변, 인디케이터 gather 에서만 두 소스를 합친다.

### 작업 단위 (확장)

| # | 문서 | 태그 | 목적 |
|---|---|---|---|
| 6 | `6_stack_row_contract.md` | [pure/data] | `UnitOverheadUiStyle` 스택행 파라미터(높이·gap·아이콘크기·배지) + `UnitOverheadLayout` 스택행 오프셋(순수, DC행 위) + `StackIconRegistry` SO(kind→sprite) + 뷰가 받는 plain DTO(kind+count) 정의 |
| 7 | `7_stack_row_view.md` | [presentation] | `UnitOverheadView.Show` 스택 목록 인자 + `ShowStacks`(아이콘 Image + 카운트 배지, ShowCards 미러, DC행 위 배치·풀링) |
| 8 | `8_dual_source_gather.md` | [ECS-read/bridge] | `BattleBridge.SyncMonoUnitViews` 듀얼소스 gather(`StackModifierSlot` RO + `HeatAccrual` RO → 유닛별 stack DTO) → `UnitOverheadUiLayer.SetUnit` → `view.Show` 배관 |
| 9 | `9_icon_art_codex.md` | **[Codex/art]** | 피로도·열기 아이콘 sprite **생성 — Codex 경로로 수행**(이 세션 밖). 하단 [아이콘 아트 브리프](#아이콘-아트-브리프-unit-9--codex) 가 입력. 산출 = sprite 에셋(+import 계약) |
| 10 | `10_wiring_play_verify.md` | [wiring] | 생성된 아이콘 임포트 + `StackIconRegistry`/스타일 SO 배선 + 씬 배선 + Play 실측 |
| 11 | `11_handoff_summary.md` | — | 인계 |

### 확장 계약

- **배치**: 체력바 → (CardGap) → 드림캐쳐 행 → (StackGap) → **스택 아이콘 행**(최상단). `UnitOverheadLayout` 에 스택행 오프셋 순수 계산 추가.
- **소스 = 듀얼(A)**: `StackModifierSlot`(kind+count) + `HeatAccrual`(Heat count). **BattleBridge 만 ECS RO 읽기**(제약 1). Presentation 은 plain DTO(kind+count)만 소비 — ECS/Battle 타입 미참조(overhead-ui 계약 승계).
- **아이콘 = 레지스트리 구동**: `StackIconRegistry`(kind→sprite). 매핑 없는 kind 는 표시 생략(피로도·열기부터, 나머지는 후속). Heat 는 `StackKind` 밖이라 레지스트리에 Heat 전용 키.
- **카운트 배지**: 아이콘 위/모서리에 현재 스택 수. 오버헤드엔 숫자 요소가 없어 신규(TMP 또는 digit 스프라이트). 1 이상만 표시.
- **표시 상한**: 최대 N개(예: 3) — 초과 시 정책(생략/+N)은 unit 6 계약에서 확정.
- **teardown**: 기존 `UnitOverheadUiLayer.Clear()` 경로 승계(신규 채널 없음).
- **StatusFx 미사용**: 구조 상이(사용자 결정). 이 확장은 오버헤드 UI 계층에서만.

### 열린 결정 (unit 6 착수 시 확정)

- **적에도 스택행 표시?** — 열기는 적에도 붙는다(모든 유닛). DC행은 defender 전용이지만 스택행은 열기 때문에 적 표시가 자연스러움 vs 적 클러터. (제안: **적도 표시**, 열기가 전 유닛 대상이므로.)
- **표시 대상 kind** — 초기 = 피로도 + 열기. Bleed/Fire/… 는 레지스트리에 아이콘 추가하면 자동 노출(후속).
- **배지 렌더** — TMP(선명, TMP 의존) vs digit 스프라이트(오버헤드 sprite 일관). (제안: 오버헤드가 Image 기반이니 digit 스프라이트 또는 작은 TMP — unit 6 에서 결정.)

### 코드↔아트 디커플링 (Codex 경로)

- **아이콘 생성(unit 9)은 Codex 경로로 세션 밖에서 수행**. 이 세션은 브리프 제공 + 코드/배선(6·7·8·10)만 담당.
- **아이콘 부재에도 무크래시**: `StackIconRegistry` 가 kind→sprite 없으면 그 스택은 **표시 생략**(폴백). 따라서 unit 6~8 코드는 아이콘 도착 전에 먼저 구현·검증 가능하고, 아이콘이 들어오면 registry 채움(unit 10)만으로 활성화된다.

### 아이콘 아트 브리프 (unit 9 · Codex)

Codex 가 이 브리프를 입력으로 스택 아이콘 sprite 를 생성한다. **아트 디렉션 = CLAUDE.md Visual Direction 준수**(캐주얼 디펜스: 작은 인게임 크기에서 읽히고, 밝고·깔끔·단순 실루엣, 모바일 색/대비. RPG/타로/다크 판타지 금지).

- **생성 대상**: `피로도(Fatigue)`, `열기(Heat)` 2종. (레지스트리 확장형 — Bleed/Fire 등은 후속에 추가.)
- **피로도**: 지침·탈진 심볼(예: 처진 땀방울/방전/늘어짐). 탁한 회청·보라 톤. 작은 크기에서 "지쳐있음" 이 즉시 읽혀야.
- **열기**: 뜨거움·온천 심볼(예: 붉은 김/아지랑이/온도계 상승). 따뜻한 오렌지·레드. "뜨겁다" 가 즉시 읽혀야. (온천 테마지만 물리적 온천 오브젝트 강제 아님.)
- **포맷**: 정사각 PNG, **투명 배경**, source 256×256 권장, 중앙 실루엣·여백 최소.
- **import 계약**: Sprite(Single), mipmap off (기존 카드 아트 import 계약 선례 — `DreamcatcherCatalogSyncTests` 아트 규약 참고).
- **아이콘엔 숫자 미포함**: 카운트는 런타임 배지가 별도로 그린다 — 아이콘은 종류 심볼만.
- **네이밍/위치**: `Assets/_Project/Art/StackIcons/` (프로젝트 아트 관례 따름) — `icon_stack_fatigue`, `icon_stack_heat`.
