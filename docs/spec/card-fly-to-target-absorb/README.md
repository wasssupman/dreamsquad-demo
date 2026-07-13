# card-fly-to-target-absorb — 카드가 타겟으로 날아가 찰싹 흡수

**상태: 초안 (다음 세션 이어서 진행) 2026-07-13**

## 목표

스와이프-부착이 확정되는 순간, 그 카드가 **손패 자리에서 타겟(유닛/타일)으로 날아가 찰싹 꽂히며
흡수되는 묵직한 임팩트** 연출을 더한다. "이 카드의 효과가 저 유닛에게 갔다"는 인과를 시각·타격감으로 강화.

## 확정 설계 (2026-07-13 브레인스토밍 결론)

- **출발점 = 손패 카드(A-1)**: 스와이프한 그 카드가 자기 손패 자리에서 발사(인과 최강). "덱에서"의
  느낌은 딜 자체가 덱에서 오는 걸로 이미 성립하므로, 확정 순간엔 손패→타겟이 자연스럽다.
- **날아가는 카드 = UGUI**: 안착 즉시 녹아 사라지므로(머무름 없음) 평평-스티커 문제 없음. 월드 카드 오브젝트 불필요.
- **아이콘 도킹 없음**(사용자 확정): 머리 위 부착 아이콘(`DcIconStripSpawner`)으로 수렴하지 않는다. 그냥
  **유닛에 흡수**. 부착 아이콘은 기존 `AttachmentsChanged` 경로로 별개로 뜬다.
- **묵직한 임팩트**(사용자 확정): 가벼운 찰싹이 아니라 **링 충격파 + 흔들림 + 유닛 펀치 + 흰 플래시 + 버스트 + SFX**.

### 시퀀스

1. **비행**: 손패 카드가 타겟 스크린 좌표(월드→스크린, 유닛 이동 **매프레임 추적**)로 **가속 접근**(Ease.InBack).
   임팩트 직전 살짝 커짐(anticipation).
2. **찰싹(1~2프레임)**: 닿는 순간 카드 **스쿼시 splat**(가로↑ 세로↓) + 동시에 **유닛(월드) 묵직 반응**:
   Spine 펀치 스케일 + 흰 플래시 틴트 + **링 충격파/버스트 파티클**(월드) + 미세 흔들림 + SoundManager "찰싹" 틱.
3. **흡수(~0.08s)**: 카드가 유닛으로 빨려들며 축소·페이드 → 소멸. **안 머문다.**

## 어색함 분석 (load-bearing — 왜 이 구조인가)

- **머리 부착 아이콘은 월드-스페이스 빌보드**다(`DcIconStripSpawner`: 유닛 월드 앵커 + 월드 offset 2.6 +
  billboardCamera). UGUI 아님. → 만약 아이콘으로 도킹하려 했다면 UI(2D)→월드 핸드오프가 필요했는데,
  **도킹을 안 하므로 그 문제 자체가 없다.**
- 남는 어색함 위험 = "평평한 UI 카드가 원근 유닛 위에 **머무는** 스티커." → **안착 즉시 dissolve** 로 회피
  (닿고 0.1s 내 소멸). 남는 인상은 **유닛(3D)의 묵직 반응**이지 2D 카드가 아니다.
- **필수 조건 3개**: (1) 안착 즉시 dissolve(머무름 금지) (2) 타겟 스크린 좌표 매프레임 추적(유닛 행진)
  (3) 임팩트 반응은 **유닛/타일(월드)** 에서 발생.

## 배선 (grounded — 실제 훅)

- **트리거**: `DreamcatcherCardDragSlot` 의 커밋 성공(`ok`) 직후 — `CommitAttach`(Unit/Squad),
  `CommitActiveDefender`(유닛 셀), `CommitActiveTile`/`CommitActivePortal`(타일). `CommitNow(commit)` 의 `ok` 분기.
- **타겟 월드 좌표**: 유닛 = `bridge.TryGetUnitViewAnchor(host, out Transform)`(DcIconStripSpawner 와 동일 게이트웨이).
  타일 = 셀→월드(`BattleBridge` 셀 변환; `TryScreenToCell` 역/기존 cell→world 헬퍼 확인 필요).
- **월드→스크린**: 카메라 project(billboardCamera/Camera.main). 매프레임 재투영(유닛 이동).
- **유닛 반응**: `SpineUnitView` 에 펀치/흰-플래시 추가(현재 `SetHealthTint`/`SetHoverHighlight` 틴트만 있음) —
  bridge 게이트 경유. 링/버스트 = `VfxSpawner`(`SpawnPlacementRing`/`SpawnMeteorBurst` 재사용 또는 전용 `SpawnCardAbsorb`).
- **VFX 소유**: 메커닉-소유 원칙([[feedback_mechanic_vfx_owned_by_mechanic]]) — 흡수 VFX 는 드림캐쳐/카드 메커닉이
  선언·구동. StatusFx 에 kind 분기 금지.

## 구현 문서 목록 (예정)

| # | 작업 구분 | 목적 |
|---|---|---|
| 0 | 카드 비행 presenter | 손패 카드(UGUI) → 타겟 스크린 좌표(추적) 가속 비행 + 스쿼시 splat + 즉시 dissolve. `CommitAttach` 성공에서 트리거. |
| 1 | 묵직 임팩트 반응 | 유닛(월드) Spine 펀치 + 흰 플래시 + 링 충격파/버스트 + 미세 흔들림 + SFX. |
| 2 | 타일 타겟 일반화 + 배선/Play | Active-Tile/Portal 은 타일 월드로 같은 찰싹. 씬 배선 + Play 검증. |
| 3 | handoff | 인계 요약. |

## feature-wide 계약 (초안)

- **손패 카드 발사, UGUI, 안착 즉시 dissolve**(스티커 방지). 월드 카드 오브젝트 안 씀.
- **아이콘 도킹 금지**(사용자 확정). 부착 아이콘은 기존 `AttachmentsChanged` 로 별개.
- **임팩트 반응은 타겟(월드)** 에서 — 유닛 펀치/플래시/링/흔들림. 카드는 사라지고 유닛 반응이 주역.
- **타겟 스크린 좌표 매프레임 추적**(유닛 행진 중에도 정확히 안착).
- **커밋 성공 시에만**(취소/실패는 카드 손패 복귀, 연출 없음). 실패 커밋은 비용 0 계약 유지.
- **순수 프레젠테이션**: ECS 시뮬 변경 0. 트리거는 기존 커밋 경로의 성공 콜백에 얹기만.
- **VFX 메커닉-소유**: 흡수 링/버스트는 카드 메커닉이 선언·구동.

## 파이프라인 커버리지

- 날아가는 카드 = 런타임 UGUI(플레이 오브젝트 아님) → N/A.
- **임팩트 링/버스트 = 월드 VFX 플레이 오브젝트** → `docs/reference/object-pipeline-map.md` 의 VFX 아키타입 대조 필요
  (신설/재사용 여부는 unit 1 착수 시). 유닛 펀치/플래시는 `SpineUnitView` 프레젠테이션 확장.

## 열린 결정 (다음 세션 착수 전)

- **흔들림 종류**: 카메라 셰이크(전체 화면, 프로젝트에 현재 없음) vs **유닛-로컬 흔들림/작은 킥**(국소). 묵직함엔
  카메라 셰이크가 강하나 전투 전체를 흔들어 과할 수 있음 — 미세 카메라 킥 or 유닛-로컬 중 결정.
- **링 충격파 VFX**: 기존 `VfxSpawner.SpawnPlacementRing`/`SpawnMeteorBurst` 재사용 vs 전용 저작(unity-vfx-authoring).
- **카드 고스트 비주얼**: `UiCardFaceMesh`(크럼플 카드 페이스) 재사용 vs 단순 스냅샷 스프라이트.
- **타일 셀→월드 헬퍼**: `BattleBridge` 에 이미 있는지(cell→world 게이트) 확인, 없으면 추가.
- **타이밍 값**: 비행 ~0.28s, dissolve ~0.08s, 펀치/플래시 duration 등 실측 튜닝.
- **`SpineUnitView` 펀치/플래시 API**: 신설(bridge 게이트). 다수 호출처 생기면 hit 반응 일반화 검토.

## 비목표 / 후속 후보

- **아이콘 수렴 도킹** — 사용자가 제외. 필요 시 별도.
- **비-부착 카드(Active 스킬)의 타겟별 차등 연출** — 이번은 공통 찰싹. 스킬별 특화는 후속.
- **화면 전역 카메라 임팩트 시스템** — 다른 타격감(처치/보스)과 공유할 셰이크 서비스는 별도.

## 연결 문서

- 선행: `docs/spec/unit-dreamcatcher-icons/`(부착 아이콘 = 월드 빌보드, 도킹 안 함 근거),
  `docs/spec/dreamcatcher-hand-deal-in/`(손패 = UGUI, 드래그/커밋 경로).
- 대상 코드: `DreamcatcherCardDragSlot`(트리거), `BattleBridge`(앵커/셀 게이트웨이), `VfxSpawner`/`SpineUnitView`(반응).
- 참고 선례: `GiftPhaseView`(PrimeTween 곡선 fly + scale→0), `DcIconStripSpawner`(유닛 앵커 월드→스크린).
