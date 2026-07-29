# 2 — 드래그 중 부착 불가 유닛 일괄 붉은 틴트

## 목적

부착 가능 여부를 조준해 보기 전에 한눈에 안다. 텍스트를 읽게 하지 않는다(계약 5) —
화면 문법을 대칭으로 완성한다: **시안 링 = 되는 곳, 붉은 몸 = 안 되는 곳.**

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherFocusPresenter.cs` (틴트 스윕 소유)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 기존 `SetDefenderHoverHighlight`
  경로를 다중 대상에 쓸 수 있는지 확인, 필요 시 얇은 확장(읽기/뷰 전용, ECS 불변)

## 구현

### A. 스윕 대상과 수명

- `DreamcatcherCardDragSlot.BeginFocus`(AttachAim)가 이미 전 유닛을 열거해 `_attachable`
  스냅샷을 만든다 — **여집합(열거 전체 − attachable)** 이 틴트 대상이다. 추가 판정 없음.
- 적용 = 드래그 시작(`Begin`), 해제 = 포커스 종료(`End`/`OnDisable` 하드 클리어 경로 포함).
  드래그가 커밋/취소/ESC/강제 종료 어느 경로로 끝나든 원복 보장 — attach-lockon 계약 #10
  의 하드 클리어에 스윕 해제를 함께 태운다.
- 스냅샷 기준이므로 드래그 중 재평가하지 않는다(락온 valid 판정과 같은 정책).

### B. 틴트 색과 기존 메커니즘

- 색은 락온 invalid 틴트(`focusTintInvalid`, 붉음)와 **동일 값** — 같은 의미(불가)는 같은
  색으로. `DreamcatcherFocusConfig` 의 기존 노브를 재사용하고 새 색 노브를 만들지 않는다.
- 적용 경로는 기존 `BattleBridge.SetDefenderHoverHighlight` → `SpineUnitView.SetHoverHighlight`
  재사용(첫 on 에 `_savedTint` 저장, off 에 원복). 현재 단일 대상 가정이 있으면 다중 대상로
  확장하되, **뷰별 저장/원복 구조는 유지**한다.
- 락온 유닛과의 관계: 락온 유닛이 invalid 면 어차피 스윕 집합에 있어 같은 색. valid 면
  attachable 집합이라 스윕 대상이 아니고 기존 시안 틴트가 그대로 먹는다 — 충돌 없음.
  단 `FlashRoutine` 가드(hover 중 restore=`_savedTint`)가 다중 대상에서도 성립하는지 확인.

### C. 스코프

- **AttachAim(Unit/Squad)만.** DefenderCast(Active-DefenderUnit)는 캡 무관이라 불가 집합이
  없고, EnemyMark 는 적 대상이라 별개(기존 빨강 무효 신호 유지). base-ring 과 같은 스코프.
- 성능: 보드 유닛 수십 기 × 틴트 set 1회 — 드래그 시작 1회 비용, 무할당 유지.

## 완료 기준

- [x] 컴파일 통과, EditMode 신규 실패 0 (리그 검증)
- [x] Play — 부착 카드 드래그 시작 즉시 불가 유닛 전부가 붉게, 가능 유닛은 시안 링
- [x] Play — 캡(3/3) 유닛과 메커닉 불일치(예: 통통구슬×탄도) 유닛이 모두 붉은가
- [x] Play — 커밋/취소/ESC/바깥 탭/페이즈 이탈 후 틴트가 전부 원복되는가
- [x] Play — 락온 이동(유닛 A→B) 중 틴트가 어긋나거나 굳지 않는가
- [x] Play — dim(0.42) 아래에서 붉은 틴트가 실제로 판독되는가 (안 되면 색 값만 조정)

확인: 2026-07-29 사용자 Play 확인("이상없음") — 커밋 해시는 handoff 참조.
