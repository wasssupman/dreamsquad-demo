# 6 · 선택 리티클 — 조준 락온과 같은 시각 언어를 유닛 선택에

## 목적

유닛을 탭 선택하면(줌+슬로모+패널+플립북) 정작 **"어느 유닛이 선택됐는지"를 유닛 자체 위에 그리는 표식이 없다**. 드림캐쳐 손패 카드 스와이프 중 유닛 락온에 쓰는 리티클(8-arm 코너 브래킷) + 콜아웃(portrait+**유닛 이름**)을 선택 상태에도 띄운다 — 같은 의미(이 유닛을 보고 있다)는 같은 문법으로.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherFocusPresenter.cs` — `AimKind.Selected` 추가 + `BeginSelection(Entity, Vector2Int)` 진입점
- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs` — `Select`/`Close` 에서 구동

## 구현

- **프레젠터 재사용, 신규 뷰 금지.** `DreamcatcherFocusPresenter` 는 `DreamcatcherHandView.Awake→BuildCanvas` 가 생성·소유(`handView.Focus` 노출, canvas order 5). `DcInspectController` 는 기존 `handView` 참조로 도달한다. `focusConfig` 미할당 등으로 `Focus == null` 이면 조용히 생략(추가 연출일 뿐 선택 기능과 무관).
- **`BeginSelection` = `Begin(Selected, null)` + `_dimTarget=0` + `SetAim(default, entity, cell)`.** 선택 모드가 기존 per-frame 분기에서 받는 것: 리티클 valid 색(`_lockValid` else→true), 콜아웃 portrait+이름(`TryGetDefenderData(_lockedCell)`), 카운트 빈 문자열. 받지 않는 것: dim(줌+슬로모가 이미 스포트라이트), base-ring/불가 스윕/몸체 틴트(AttachAim·DefenderCast 전용), 확정 펄스.
- **리티클은 엔티티 스크린 렉트를 매 프레임 추종**(`TryGetUnitScreenRect`) — 인스펙트 줌 카메라 이동·유닛 사망(렉트 소실 시 자동 페이드아웃)에 별도 처리 불요.
- **콜아웃 위치 규칙은 조준/선택 공통 하나** (rev, 사용자 결정 2026-07-29): **리티클 프레임 상단 + `calloutFrameGap`**(SO, 기본 24px). 스무딩된 프레임(`_reticleCur`) 기준이라 콜아웃이 프레임과 한 몸으로 움직인다. 손끝 회피는 프레임 최소 크기(`reticleMinScreenSize`, 손끝 반경 초과 보장)가 담당 — 모드별 오프셋(`calloutScreenOffset` y=96, 락온 렉트 기준)은 은퇴(asset 잔존 키 무해). 두 모드의 리티클+콜아웃이 완전히 같은 구조가 되는 것이 의도.
- **`_reticleShown` 가드(컨트롤러).** `Close()` 는 `Blocked()` 동안 매 프레임 불린다 — 무조건 `Focus.End()` 하면 손패 카드 드래그가 방금 시작한 조준 세션을 끊는다. 우리가 켠 세션만 끝낸다. 선택 전환(A→B)은 `Close` 없이 `Select` 재진입 → `BeginSelection` 재호출로 리티클이 새 대상 위에서 pop(L6 `_lastLocked` 리셋). 전환 대상 셀 해석 실패 시엔 이전 리티클을 명시 `End()`.
- **배타 무결성은 기존 게이트가 보장.** 리티클이 떠 있는 동안 손패 오픈/드래그/이동모드 진입 → `Blocked()` → `Close()` → `End()` 가 먼저 걷힌 뒤에야 상대가 `Begin` 한다(순차, 세션 겹침 없음).

## 완료 기준

- [x] compile 클린 (에러/경고 0)
- [x] Play: 배치/전투 중 유닛 탭 → 리티클이 유닛 몸체를 감싸며 pop + 콜아웃에 portrait+**유닛 이름** 표기
- [x] Play: 다른 유닛 탭 전환 → 리티클이 새 유닛 위에서 pop(가로질러 날아가지 않음), 재탭 → 리티클/콜아웃 페이드아웃
- [x] Play: 선택 중 손패 오픈 → 리티클 소거 후 카드 드래그 락온 정상(조준 리티클과 충돌 없음), 이동모드 진입 시 리티클 소거
- [x] Play: 화면 딤(dim) 없음 — 조준 때만 딤

확인: 2026-07-29 사용자 Play 확인("확인함") — 콜아웃 위치 규칙 통일 rev(calloutFrameGap 24px) 포함. 커밋 해시는 git log 참조.
