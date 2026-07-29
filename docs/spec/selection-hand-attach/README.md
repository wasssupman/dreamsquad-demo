# selection-hand-attach — 유닛 선택 중 손패 등장 + 탭 즉발/D&D 부착

> 상태: **초안 2026-07-29** (사용자 UX 결정 4건 반영, 구현 대기)

## 배경 / 목표

유닛 선택(unit-dreamcatcher-inspect)과 드림캐쳐 손패(dreamcatcher-use-flow)는 현재 **상호배타**다
(`DcInspectController.Blocked()` 가 손패 오픈 시 선택을 강제 Close). 이를 **파트너 관계**로 전환한다:

- 유닛을 선택하면 드림캐쳐 손패가 **항상** 자동 등장한다.
- 손패 카드 **탭 = 선택 유닛에 즉발 부착** (Unit/Squad 카드만). 불가 카드/불가 상태는 움찔 + 사유.
- 손패 카드 **D&D = 기존 그대로** (임의 유닛/타일/적 조준 — 모든 카드).
- 어제 확정된 손패 유지 메커니즘(use-flow: 사용 후 유지·재딜인·사용 가능 0장 자동 닫힘·press~release 슬로모)은 **그대로 탄다**.

## 검증 질문

> 유닛을 탭 선택하면 손패가 함께 나타나고, 카드를 탭해 그 유닛에 즉시 부착하거나 끌어서 다른
> 유닛에 부착할 수 있는가 — 그 과정에서 선택 상태·리티클·슬로모·손패 유지 규칙이 꼬이지 않는가?

## 사용자 결정 (2026-07-29)

1. 손패 등장 = 선택 시 **항상** (게이지 부족이면 전부 dim 으로 보임 — 멘탈 모델 일관).
2. 손패 오픈 중 다른 유닛 탭 = **선택 전환 + 손패 유지** (연속 부착 흐름이 핵심 가치).
3. 손패 오픈 중 빈 보드 탭 = **손패+선택 동시 해제**.
4. 탭 즉발 = **부착 카드(Unit/Squad)만**. 그 외(Active 전 계열/적 표식)와 불가 상태는 탭 시 **움찔 + 사유 표시, 무차감**.

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_inspect_gate_split.md` | 리팩터 | `Blocked()` 를 close-trigger vs tap-gate 로 분리 — 손패/조준이 선택을 죽이지 않게 |
| 1 | `1_selection_opens_hand.md` | 상태 결합 | 선택→손패 오픈(항상)·해제→손패 닫기 + 선택 타겟 전달 seam |
| 2 | `2_board_tap_routing.md` | 입력 라우팅 | 손패 오픈 중 보드 탭: dismissCatcher → 유닛 픽=선택 전환 / 빈 보드=동시 해제 |
| 3 | `3_tap_instant_attach.md` | 기능 | 카드 탭 즉발 부착(Unit/Squad) + 불가 움찔 피드백 |
| 4 | `4_focus_session_handoff.md` | 연출 정합 | 카드 조준이 선택 리티클을 대체한 뒤 종료 시 재주장 |
| 5 | `5_wiring_play_validation.md` | 배선+검증 | 씬 배선 확인 + Play e2e (카메라 합성 체감 포함) |

0 → 1 → 2 순서 필수(게이트 분리 없이는 손패 오픈이 선택을 죽인다). 3·4 는 1 이후 독립.

## Feature-wide 계약

1. **선택 상태 소유자는 `DcInspectController` 단일 유지.** 손패는 선택을 **전달받는** 파트너 —
   선택 Entity 를 뷰가 재판정하지 않는다. 전달 방향: 컨트롤러 → `DreamcatcherHandView`(기존
   `handView` 참조 재사용, 신규 씬 배선 0).
2. **`Blocked()` 분리.** close-trigger(선택을 닫음) = 이동모드 / 배치 드래그·arm / 페이즈 이탈.
   tap-gate(새 탭만 막음, 선택 유지) = 손패 오픈 / `IsAiming`(Active 조준) / 카드 인터랙션.
3. **보드 raw 탭 소비자는 순간마다 하나** (inspect 계약 11 계승 — 동시 경쟁 금지, 순차 핸드오프 허용):
   손패 닫힘 = `DcInspectController`(raw Pointer, -50) / 손패 열림 = `HandDismissCatcher`(UGUI Button).
   catcher 는 **카드 인터랙션 진행 중(`AnyInteractionActive()`/포탈 조준) 클릭 무시** — 포탈 출구
   탭의 릴리즈가 catcher 클릭으로 손패를 닫는 기존 엣지를 차단한다.
4. **커밋 경로 단일.** 탭 즉발도 `CommitAttach → HandChanged(Used) → OnCardUsed` 를 그대로 지난다
   — 유지/자동닫힘/재딜인/무차감 거절(use-flow 계약)이 자동 적용. 별도 소비 경로 신설 금지.
5. **즉발 유효성 = 커밋 거절과 UI 일치**: `CanUse`(게이지) AND `CanAttachMore` AND
   `WouldDreamcatcherCardApply`(D&D 의 `_attachable` 스냅샷과 같은 3판정). 불가 = 움찔 트윈 +
   기존 브리핑 문안 재사용, 차감 0.
6. **`DreamcatcherFocusPresenter` 는 단일 세션.** 카드 조준 `Begin` 이 선택 리티클(`Selected`)을
   대체하는 것은 정상이고, 인터랙션 종료 시 선택이 살아 있으면 **재주장**(`BeginSelection` 재호출)
   한다. 프레젠터에 세션 스택을 만들지 않는다(과잉 추상화 금지).
7. **닫힘 비대칭**: 선택 해제(빈 보드 탭/유닛 사망/이동모드/페이즈)는 **손패도 닫는다**.
   손패만 닫히는 경로(항아리 토글/사용 가능 0장 자동 닫힘)는 **선택을 유지**한다.
8. **슬로모 중첩 무해 전제 유지** — 선택 lease 와 손패 held lease 는 동일 priority(50)·동일
   scale(`AwakeningConfig.slomoTimeScale`) 이라 유효 스케일 불변. 이 노브를 갈라놓지 않는다.
9. **카메라는 CameraDirector 채널 합성에 맡긴다** — 인스펙트 줌 + 손패 헤드룸은 독립 가중치
   채널이라 기계적 충돌 없음. 체감(줌인+피치다운 동시)은 Play 검증 항목, 튜닝은 config 노브로만.
10. **수치는 SerializeField/SO** (움찔 트윈 진폭/시간 등). 하드코딩 금지(제약 6).

## 상태 꼬임 지점 → 해소 매핑 (2026-07-29 실측)

| 충돌 | 현행 동작 | 해소 |
|---|---|---|
| `Blocked()`: 손패 오픈 = 선택 강제 Close | `DcInspectController.cs Blocked()/Update` | unit 0 분리 |
| `IsAiming`(Active 카드 드래그) = 선택 Close | 〃 | unit 0 (tap-gate 로 이동) |
| 손패 오픈 중 보드 탭 전량 catcher 소유 (`IsOverUi` 가 catcher 히트) | 유닛 탭 불가, 닫기만 | unit 2 라우팅 |
| 포탈 출구 탭 릴리즈 = catcher 클릭 → 손패 닫힘 | 잠복 엣지 | unit 2 가드(계약 3) |
| 카드 조준 `BeginFocus`/`EndInteraction→Focus.End()` 가 선택 리티클 소거 | inspect unit 6 리티클 | unit 4 재주장 |
| 이동모드 목적지 탭이 catcher 에 먹힘 | — (신규 조합) | 계약 7: 이동모드 진입 = 손패 닫기 |

## 파이프라인 커버리지

N/A — 플레이 오브젝트 신설/생성→렌더 경로 변경 없음. 기존 UGUI·커밋·연출 재사용.

## 후속 후보 (범위 밖)

- **탭 즉발의 Active-DefenderUnit 확장** — 사용자 결정으로 이번엔 부착만. 체감 후 재평가.
- **선택 유닛 강조 base-ring** — 손패 오픈 중 선택 유닛에 시안 링(조준 문법 확장). 리티클과 중복이라 보류.
- **손패만 닫고 유닛 유지하는 2단계 해제** — Q3 에서 기각(동시 해제 채택). 불편 보고 시 재고.
- **카드 탭 즉발의 언두/확인 스텝** — 오탭 부착 보고가 나오면 검토(현재는 드래그 임계=DPI 보정이 방어).
