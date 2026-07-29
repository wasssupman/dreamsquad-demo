# 6 — Handoff Summary (units 0~8 구현 완료 · **Play 검증 전량 미실시**)

> 갱신 2026-07-29 (units 7·8 추가 반영 + 원격 세션 인계). 다음 작업자는 **여기부터** 읽는다.

## 지금 상태 한 줄

코드는 다 들어갔고 컴파일은 깨끗하지만, **에디터 Play 로 확인한 것은 unit 0 하나뿐이다.**
다음 세션의 첫 일은 기능 추가가 아니라 **Play 검증**이다.

## Commit (전부 `origin/main` + `gitlab/master` 에 반영 완료)

| 해시 | 내용 |
|---|---|
| `6c152b74` | (선행 spec) unit-dreamcatcher-inspect unit 6 — 선택 리티클 + 콜아웃 위치 규칙 통일 |
| `af500701` → `9fdaba6a` | 이 spec 초안 → critic REVISE(H5/M7/L7) 전건 반영 rev 2 |
| `9d719953` | unit 0 — `Blocked()` → `MustClose()` / `TapGated()` 분리 + 조준 중 줌 피드 중단 |
| `1c51e312` | unit 1 — 선택 시 손패 자동 오픈 + pending-open 래치 + 앵커 liveness |
| `4ea13544` | units 2~4 — 보드 탭 라우팅 + 탭 즉발 부착 + 리티클 재주장 |
| `c8029fcb` | 문서 반영 |
| `61101ed9` | unit 7 — 유닛 주변 아이콘 버튼 숨김 + 선택 중 Active 카드 차단 |
| `79efeaeb` | unit 8 — 부착으로 게이지 소진 시 선택까지 자동 해제 |

## Implemented

- 유닛 선택과 손패가 **공존**한다. `MustClose()`(배치 드래그·arm / 이동모드)만 선택을 닫고,
  손패 오픈·조준은 `TapGated()`(새 탭만 차단)로 내려갔다.
- 선택 시 손패가 **항상** 열린다. 침강(`Transitioning` ~0.4초) 중 선택은 래치로 예약돼 전이
  종료 첫 프레임에 열린다. 선택 기인 오픈은 항아리 `Pulse()` 를 쏘지 않는다.
- 손패 오픈 중 보드 탭: **유닛 = 선택 전환(손패 유지)** / **빈 보드·재탭 = 동시 해제**.
- 카드 **탭 = 선택 유닛에 즉발 부착**(Unit/Squad). 불가는 좌우 움찔 + 사유, 차감 0. D&D 는 불변.
- 선택 리티클(콜아웃 = portrait + 유닛 이름)이 조준 세션에 밀려도 종료 시 **1회 재주장**된다.
- 조준 중에는 인스펙트 줌 피드를 끊어 타일/출구 조준 프레이밍을 돌려준다.
- 선택 유닛 사망은 **앵커 소실 연속 3프레임**으로 감지해 닫는다.
- **unit 7**: 유닛 주변 아이콘 버튼(이동+더미2)은 노출하지 않는다(`showActionFlipbook=false`).
  선택 중 **Active 카드 사용 불가**(드래그·탭 양 경로, 조준 진입 자체가 없다).
- **unit 8**: 부착으로 쓸 카드가 0장이 되면 손패 침강 + **선택도 해제**해 기본 진행으로 복귀.

## Key Files

- `UI/Dreamcatcher/DcInspectController.cs` — 선택 상태 소유자. `MustClose`/`TapGated`/`AimingNow`,
  `TickSelectionAnchor`, `OnBoardTapped`, `CloseByIntent`, `ShowSelectionReticle`,
  `OnFocusSessionReleased`, `OnUsableCardsExhausted`, `showActionFlipbook`
- `UI/Dreamcatcher/DreamcatcherHandView.cs` — `SelectionTarget`/`InSelectionMode`,
  `OpenForSelection`/`CloseFromSelection`, `TickPendingSelectionOpen`, `BoardTapped`,
  `InteractionEnded`/`FocusCleared`/`UsableCardsExhausted`, `FlinchSlot`
- `UI/Dreamcatcher/HandDismissTapCatcher.cs` — press-스냅샷 탭 캐처(신설)
- `UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` — `OnPointerClick` 즉발, Active 차단, `CommitNow`
- `UI/Dreamcatcher/DreamcatcherFocusPresenter.cs` — `BeginSelection`, `TryCaptureConfirmCenter`
- `UI/DefenderRelocationController.cs` — `IsOverUi` 캐처 단독 히트 예외

## Verified

- `dotnet build Wassup.Runtime.csproj` → **0 error / 0 warning** (편집 파일 전부 이 어셈블리)
- Unity 스크립트 컴파일 후 콘솔 error/warning **0**
- **Play: unit 0 만 사용자 확인(2026-07-29). units 1~8 전량 미검증.**

## Notes (되돌리면 안 되는 의도)

1. **`FocusCleared` 는 `_focus.End()` 뒤에 발화**한다. 앞으로 옮기면 재주장분을 그 `End()` 가 지운다.
2. **캐처는 press 프레임 스냅샷으로 판정**한다. release 시점 판정으로 되돌리면 포탈 출구 탭의
   릴리즈가 보드 탭으로 새어 선택을 전환/해제한다(커밋이 press 프레임에 상태를 내린다).
3. **탭 즉발의 가드 0(`_dragging || IsPortalAiming`)을 지우지 말 것.** UGUI 는 드래그로 이어진
   press 의 클릭을 삼키지 않는다 — 지우면 "손패로 되돌려 취소" 가 즉발 부착으로 차감된다.
4. **`flinching` 플래그** 없으면 `SpringSlots` 가 셰이크를 매 프레임 홈으로 끌어 뭉갠다.
5. **선택 슬로모는 상시 유지**가 사용자 결정(README 계약 8, use-flow 계약 1 의 명시 예외).
6. **unit 8 이벤트 발화 위치**: `BindEmpty` 뒤 + `Close()` 앞. 벗어나면 소모 카드 재표시 또는
   리티클 1프레임 깜빡임이 생긴다(`8_exhausted_auto_release.md` 참조).
7. **`showActionFlipbook=false` 인 동안 재배치는 도달 불가**다(이동 버튼이 유일 진입구). 기능을
   지운 게 아니라 문을 닫은 것 — 토글로 즉시 복귀.
8. **게이지 0 인 채 선택만 한 경우는 자동 해제하지 않는다**(사용자 결정: 그대로 둔다). unit 8 은
   "사용이 실제로 있었다" 를 신호에 내포시켜 이 비대칭을 의도적으로 유지한다.

## Follow-up (다음 세션 우선순위)

1. **Play e2e** — `5_wiring_play_validation.md` 시나리오 1~12 + `7`·`8` 완료 기준. 우선순위:
   ① 카드 탭 연속 부착 ② 포탈 입구/출구 커밋 후 선택 불변 ③ 침강 중 재선택 래치
   ④ 부착 0장 유닛 사망 ⑤ 게이지 소진 시 동시 해제 + 확정 펄스 ⑥ Active 차단
2. **실기기(Android) 스모크** — 탭↔드래그 판별·캐처 탭/스와이프 판별은 터치에서만 드러난다.
   입력 프레임 순서가 이 spec 의 핵심이라 **PlayMode 테스트로는 대체 불가**(문서에 명시).
3. **카드 고스트 비행 × 줌아웃 겹침** 체감(unit 8). 거슬리면 비행 완료 콜백까지 해제 지연 —
   콜백 배선이 추가되므로 체감 후 판단.
4. 카메라 체감(인스펙트 줌 + 손패 헤드룸)이 과하면 config 노브만 튜닝(코드 분기 금지).

## 환경 인계 주의 (다른 머신에서 pull 받는 경우)

- **커밋만 넘어간다.** 이 워크트리에는 다른 세션/에디터가 만든 **미커밋 dirty 가 남아 있다** —
  폰트 `.asset` 3종, `LiberationSans SDF - Fallback.asset`, `Redbull_Mat.mat`,
  `ProjectSettings/*`, `.claude/settings.json`, 미추적 `InitTestScene*.unity`·`.meta` 몇 개.
  전부 이 spec 과 무관하고 **의도적으로 커밋하지 않았다**. 새 환경에는 반영되지 않는다.
- `ProjectSettings/ProjectSettings.asset` 은 **읽기만** 할 것(과거 iOS 서명값 소실 이력).
- **여러 세션이 같은 워크트리를 공유한다.** 이 작업 중 `DefenderRelocationController.cs` 는 타
  세션의 `placement-thumb-occlusion` 작업과 겹쳐 **내 hunk(`IsOverUi`)만 선별 스테이징**했다.
  같은 파일을 다시 만질 때 `git diff` 로 타 세션 hunk 를 먼저 확인할 것.
- 씬 배선은 **추가 요구 0** — 전 유닛이 기존 `handView` 등 기존 참조만 쓴다. 새 SerializeField
  배선이 필요해 보이면 설계 위반이니 되돌린다. `showActionFlipbook`·움찔·캐처 임계는 런타임
  생성 컴포넌트의 기본값이라 씬 배선이 아니다.
