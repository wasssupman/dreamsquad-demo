# placement-cell-snap

상태: **구현 완료 2026-07-17** (units 0~5, 커밋 `922db9b9` + `a3812079`) + **unit 7 rev 2026-07-18** (끈적함 액체 타일 — Play 검증 통과, 커밋 대기). 인계: `6_handoff_summary.md`.
작성일 2026-07-16

## 목표

드래그 배치 시 포커스 타일이 손가락 미세 이동에 "휙휙" 바뀌어 오배치되는 문제를,
**타일 경계 히스테리시스(A)** + **고스트의 타일 중심 스냅(C)** 으로 잡는다.
키링(링=손가락 / 유닛=아래 매달림 / 줄) 시인성과 감각은 살린다.

**검증 질문**: 경계 근처에서 손가락을 미세하게 떨어도 포커스 타일이 튀지 않고,
유닛 고스트가 배치될 타일 중심에 또렷하게 앉으며, 링→유닛 줄이 여전히 잘 보이는가?

## 배경 · 진단

포커스 타일은 `DefenderDragPlacementController.UpdateHoverAtTarget()` 에서 **매 프레임**
`손가락 목표점 → BoardSpace.ToSim → DebugWorldToCell(반올림)` 으로 결정된다.
포인터→셀 경로에는 **히스테리시스·데드존·스무딩이 전혀 없다** (탐색으로 확증).
경계에 데드밴드가 없어 두 타일 사이에서 터치 좌표 지터만으로 반올림이 A↔B 로 튄다 — 오배치의 직접 원인.
현재 유닛은 손가락보다 화면상 `totalDrop`(유닛 키 + 줄 길이) **아래**에 그려진다(`TryComputeRingUnit`) — 시인성 설계는 이미 있음.

## 접근 (A+C)

- **A 히스테리시스** — 현재 포커스 셀을 끈끈하게 유지, 손가락이 경계를 여유(margin)만큼 확실히 넘을 때만 이웃 셀로 전환(2D 슈미트). 경계 지터를 흡수 → 플리커 제거. *논리 셀에만* 적용.
- **C 고스트 타일 스냅** — 유닛 스프링 rest 타깃을 포커스 셀 **중심(view world)** 으로. 링은 손가락 유지, 줄이 손가락~타일 사이로 늘어나 키링이 더 또렷. 논리 셀(A)과 시각 위치를 일치시켜 목적지를 확정.

## 작업 단위 목록

| # | 파일 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_cell_snap_resolver.md` | foundation | 순수 함수 `PlacementCellSnap.Resolve`(frac-cell→int cell, 히스테리시스) + EditMode 테스트 |
| 1 | `1_hysteresis_hover.md` | feature (A) | Resolve 를 `UpdateHoverAtTarget` 에 배선 + bridge frac-cell read 헬퍼 + margin SO |
| 2 | `2_ghost_tile_snap.md` | feature (C) | 유닛 스프링 타깃을 포커스 셀 중심으로 스냅, 링/줄 유지, Update 순서 정리 |
| 3 | `3_settle_to_commit.md` | feature | 인접 칸 이동에 commitDelay(디바운스) + 큰 점프 즉시확정. 실시간 추종 끊기 |
| 4 | `4_commit_pop.md` | feature | 확정 셀 변경 시 하이라이트에 스케일-페이드 팝. 고스트 안 얼리고 스냅 느낌 |
| 5 | `5_touch_skew_fix.md` | bugfix | 손가락→판정 타일 좌우 오프셋의 카메라(퍼스펙티브) 의존 제거. 보드-수평을 손가락 열에 정렬 |
| 7 | `7_stickiness_blob.md` | feature (rev) | 히스테리시스 시각화 — 포커스 하이라이트 자체가 액체(SDF 셰이더): 고정 테두리 + 손가락 방향 번짐 + 스프링 관성 |

의존: `0 → 1 → (2 되돌림) → 3 → 4`. `5` 는 독립. `7` 은 1(신호)·4(팝 punctuation) 위에 얹힘. 각 단위는 독립 컴파일·Play 검증 가능.

**설계 반전 기록(unit 2)**: unit 2(고스트를 셀 중심으로 스냅)는 **되돌렸다** — 고스트가 셀에 얼어붙으면
키링 줄/스윙(스프링·댐핑·길이)이 사라지기 때문. 대신 고스트는 손가락을 연속 추종(키링 유지)하고,
"타일 판정 안정화"는 논리 레이어(unit 1 히스테리시스 + unit 3 디바운스)가, "스냅 느낌"은 하이라이트
확정 팝(unit 4)이 담당한다. 코드에는 `ResolveFocusAndTarget` 이 셀만 확정하고 스프링 타깃은 raw feet 로 남는다.

**D&D UX 정합**: 이 조합(오프셋 고스트=키링 + 스냅/양자화 + 하이라이트-릴리즈 계약 + 히스테리시스)은
Clash Royale / iOS 네이티브 드래그의 표준 정답과 동일하다. unit 3(settle-to-commit)은 그 위에 모바일
정밀 배치를 위한 시간 레이어를 더한 확장.

## Feature-wide 계약

- **정책/변환 분리**: 셀 선택 정책(히스테리시스)은 순수 함수 `PlacementCellSnap.Resolve`. 좌표 변환(origin/tileSize)은 bridge read 헬퍼가 담당. 정책 함수는 아키텍처 타입을 모른다(제약 10) → EditMode 테스트.
- **히스테리시스는 논리 셀에만**: 뷰/카메라 스무딩(KeyringSim 스프링, CameraDirector, 컷신 틸트)과 **분리 유지**. 기존 "흔들리는 유닛 위치는 셀 결정에 안 쓴다" 설계 존중.
- **셀 공간 일관성**: 히스테리시스는 `frac = (sim - boardOrigin)/tileSize` 공간(셀 중심=정수, 경계=±0.5)에서 계산. `DebugWorldToCell = round(frac)` 과 동일 공간이라 커밋 셀과 드리프트 없음.
- **고스트 스냅**: 스프링 **rest 타깃만** 셀 중심으로. 스프링 자체는 부드럽게 유지(딱딱한 순간이동 금지) — 이동 중 스윙 살고 멈추면 타일에 안착. 실제 배치 유닛엔 스냅/히스테리시스 없음.
- **시인성 계약**: 유닛은 손가락보다 화면상 `totalDrop` 아래 유지(기존). 스냅은 이를 quantize 할 뿐 손가락 밑으로 끌어올리지 않는다. 부족 시 `ropeLength` 로 드롭을 벌린다(코드 무변경).
- **데이터 주도**: `stickMargin`(타일 분수) 등 튜닝값은 `DragSwaySettings` SO. 하드코딩 금지.
- **상태 리셋**: 히스테리시스/throttle 상태는 세션 시작 / 오프보드 시 리셋(`ClearHover` 가 `hoverTile`·`_debounce` 정리).
- **릴리즈 규칙 (리뷰 수정 2026-07-17)**: `EndDrag` 는 throttle tick 을 **기다리지 않는다** —
  `ResolveFocusAndTarget(0f, forceCommit:true)` 로 손가락 최종 위치를 **히스테리시스만** 통과시켜 즉시 재해석 후 커밋.
- **액체 하이라이트 대체 규칙 (unit 7 rev, 2026-07-18)**: `stickyBlobEnabled` 이면 포커스 셀의
  타일 하이라이트(`SetPlacementHover`)를 **호출하지 않는다** — 액체 쿼드가 하이라이트 본체(같은 셀에 시각 개체 2개 금지).
  테두리는 확정 칸 고정 = 릴리즈 계약. 머티리얼은 반드시 `TileSetData.placementLiquidMaterial` 에셋 참조(Shader.Find 스트리핑 금지).
  하이라이트·팝도 같은 호출에서 갱신 → "표시 칸 == 배치 칸" 유지. 없으면 빠른 드롭이 최대 interval(0.5s) 전
  stale 칸에 배치되는 회귀(리뷰 확정). throttle 은 **드래그 중 표시**에만 적용된다.
- **범위**: 뷰·입력 계층 국소 변경. ECS/데이터/배치 로직·NativeQueue 채널 무변경.

## 파이프라인 커버리지

**N/A** — 신규 플레이 오브젝트 없음. 드래그 프리뷰 고스트/링/줄은 기존
(`placement-drag-preview-polish` / `keyring-cord-preview`)이고, 생성→렌더 경로를 바꾸지 않는다.
본 spec 은 포커스 셀 **선택 정책**과 고스트 **목표 위치**만 바꾼다.

## 후속 후보 (D&D UX 폴리시 — 별도 unit/spec)

- **드래그 유닛 반투명**: 고스트 자체를 60~70% 알파로 → 발밑 지형/셀 비침(현재는 적만 `SetEnemiesDimmed`). 오프셋과 병행 시 가림 완화.
- **릴리즈 실패 복귀**: 무효 드롭 시 현재 `FlashPlacementReject`(빨강 플래시)에 더해 고스트가 트레이로 되돌아가는 애니 + 햅틱((a) 계열, 오배치=자원소모라 안전).

## 비목표 (기각/스코프 밖)

- ~~시간 기반 디바운스~~ → unit 3 에서 "인접만 delay + 큰 점프 즉시" 형태로 편입(뭉개짐 회피).
- 드래그 후 별도 탭/홀드 확정 제스처, 타깃팅 화살표, 탭-탭(키링 오프셋-프록시로 이미 구조적 해결 — 불필요/스코프 밖).
- 방향 제스처화(Candy Crush): 목적지가 자유 그리드라 인접-스왑 제약이 없어 **적용 불가**.
- 배치 완료 유닛의 상시 스냅/흔들림, 드롭 bounce/착지 반동.
- fallback capsule 프리뷰(3D 프리미티브 — 각도/스냅 어색함 없음, 스킵).
