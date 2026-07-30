# selection-hand-attach — 유닛 선택 중 손패 등장 + 탭 즉발/D&D 부착

> 상태: **units 0~15 구현·커밋 완료 · 핵심 체감 Play 확인 · `unit 5` e2e 전량 훑기 대기**
> (units 9~15 인계는 `16_handoff_summary.md`)
>
> 커밋: units 0~8 은 `6_handoff_summary.md`, units 9~15 는 `16_handoff_summary.md` 참조.
> 설계는 critic REVISE(H5/M7/L7) 전건 반영 rev 2(`9fdaba6a`) + 사용자 결정 다수.
>
> **units 10~15 는 선택 모드의 프레젠테이션 재설계**다(2026-07-30 사용자 발의: 부착 패널이
> 리티클·맵을 가림). 기능 계약(0~9)은 건드리지 않는다 — 무엇을 보여주고 어디에 두느냐만
> 바꾼다. units 10~15 전부 **ECS 무변경**이다.

## 배경 / 목표

유닛 선택(unit-dreamcatcher-inspect)과 드림캐쳐 손패(dreamcatcher-use-flow)는 현재 **상호배타**다
(`DcInspectController.Blocked()` 가 손패 오픈 시 선택을 강제 Close). 이를 **파트너 관계**로 전환한다:

- 유닛을 선택하면 드림캐쳐 손패가 **항상** 자동 등장한다.
- 손패 카드 **탭 = 선택 유닛에 즉발 부착** (Unit/Squad 카드만). 불가 카드/불가 상태는 움찔 + 사유.
- 손패 카드 **D&D = 기존 그대로** (임의 유닛/타일/적 조준 — 모든 카드).
- 손패 유지 메커니즘(use-flow: 사용 후 유지·재딜인·사용 가능 0장 자동 닫힘)은 그대로 탄다.

## 검증 질문

> 유닛을 탭 선택하면 손패가 함께 나타나고, 카드를 탭해 그 유닛에 즉시 부착하거나 끌어서 다른
> 유닛에 부착할 수 있는가 — 그 과정에서 선택 상태·리티클·슬로모·손패 유지 규칙이 꼬이지 않는가?

## 사용자 결정 (2026-07-29)

1. 손패 등장 = 선택 시 **항상** (게이지 부족이면 전부 dim 으로 보임 — 멘탈 모델 일관).
2. 손패 오픈 중 다른 유닛 탭 = **선택 전환 + 손패 유지** (연속 부착 흐름이 핵심 가치).
3. 손패 오픈 중 빈 보드 탭 = **손패+선택 동시 해제**.
4. 탭 즉발 = **부착 카드(Unit/Squad)만**. 그 외(Active 전 계열/적 표식)와 불가 상태는 탭 시 **움찔 + 사유 표시, 무차감**.
5. (critic H4 회신) 손패 오픈을 **선택 모드 vs 일반 모드로 명시 분기**해 로직을 분리한다.
   **선택 중에는 슬로모가 상시 적용**(기존 인스펙트 계약 유지 — 선택 lease 가 소유).
   일반 오픈(항아리 단독)은 use-flow 계약 1(슬로모 = 카드를 잡은 동안만) 불변.

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_inspect_gate_split.md` | 리팩터 | `Blocked()` 를 close-trigger vs tap-gate 로 분리 + 조준 중 줌 피드 중단 |
| 1 | `1_selection_opens_hand.md` | 상태 결합 | 선택→손패 오픈(항상)·모드 분기·pending-open 래치·선택 수명(사망 커버) |
| 2 | `2_board_tap_routing.md` | 입력 라우팅 | catcher 를 press-스냅샷 탭 캐처로 재설계 — 유닛 픽=선택 전환 / 빈 보드=동시 해제 |
| 3 | `3_tap_instant_attach.md` | 기능 | 카드 탭 즉발 부착(Unit/Squad) + 명시 클릭 가드 + 움찔(flinching 소유) |
| 4 | `4_focus_session_handoff.md` | 연출 정합 | 리티클 재주장 — 트리거를 "세션 강제 종료 신호"로 확장 |
| 5 | `5_wiring_play_validation.md` | 배선+검증 | Play e2e (포탈/사망/조준 프레이밍/튜토리얼 경로 포함) |
| 6 | `6_handoff_summary.md` | 인계 | 커밋·핵심 파일·되돌리면 안 되는 의도 5건 + 남은 Play 검증 |
| 7 | `7_selection_scope_narrowing.md` | 정책(추가) | 유닛 주변 아이콘 버튼 숨김(코드 유지) + 선택 중 Active 카드 차단 |
| 8 | `8_exhausted_auto_release.md` | 정책(추가) | 부착으로 게이지 소진 시 선택까지 자동 해제(계약 7 예외 ①) |
| 9 | `9_awakening_button_full_release.md` | 정책(추가) | 선택 중 각성 버튼 = 기본 전투 상태 복귀(계약 7 예외 ②) |
| 10 | `10_effective_stat_read_seam.md` | 토대(추가) | 실효 스탯 pull API + 델타 계산 순수 함수 |
| 11 | `11_selection_detail_panel.md` | 프레젠테이션(추가) | 패널 좌측 고정 도킹 + 스탯 3종 + 델타 칩 |
| 12 | `12_reticle_simplification.md` | 프레젠테이션(추가) | 선택 모드 콜아웃 생략(조준 경로 불변) |
| 13 | `13_camera_rebalance.md` | 연출(추가) | dolly↓ + FOV 압축 + 전환 NDC 스무딩 + 연출 pitch |
| 14 | `14_reject_camera_kick.md` | 연출(추가) | 부착 거절 시 아주 짧은 카메라 킥 |
| 15 | `15_move_button_in_panel.md` | 정책(추가) | 이동 버튼을 패널로 — 유닛 주변 플립북 폐기 |
| 16 | `16_handoff_summary.md` | 인계 | units 9~15 커밋·되돌림 금지 7건·공간 실측·잔여 |

10 → 11 → 12 순서 필수(읽을 값 → 표시할 곳 → 중복 제거). 13·14 는 독립. 15 는 11 이후.

0 → 1 → 2 순서 필수(게이트 분리 없이는 손패 오픈이 선택을 죽인다). 3·4 는 1 이후 독립.

## Feature-wide 계약

1. **선택 상태 소유자는 `DcInspectController` 단일 유지.** 손패는 선택을 **전달받는** 파트너 —
   선택 Entity 를 뷰가 재판정하지 않는다. 전달 방향: 컨트롤러 → `DreamcatcherHandView`(기존
   `handView` 참조 재사용, 신규 씬 배선 0).
2. **`Blocked()` 분리.** close-trigger(선택을 닫음) = 배치 드래그·arm / 이동모드 / 페이즈 이탈.
   tap-gate(새 탭 후보만 막음, 선택 유지) = 손패 오픈 / `IsAiming`·카드 인터랙션.
   게이트는 탭 후보 **무장 분기 앞**에 둔다. 조준·카드 인터랙션 중에는 **줌 피드도 중단**한다
   (`FeedZoomTarget` — staleness 2프레임 자동 해제로 조준 프레이밍을 돌려준다, critic M4).
3. **보드 raw 탭 소비자는 순간마다 하나** (inspect 계약 11 계승 — 순차 핸드오프): 손패 닫힘 =
   `DcInspectController`(raw Pointer, -50) / 손패 열림 = **HandDismissTapCatcher**.
   catcher 는 UGUI Button 이 아니라 `IPointerDownHandler + IPointerClickHandler` 경량 컴포넌트
   (`GiftPhaseView.cs:264` TapCatcher 선례)다 — **press 프레임 스냅샷**(`_pressBlocked =
   AnyInteractionActive() || GameManager.IsAiming`, `pressPosition`)으로 판정하고(릴리즈 시점
   상태 판정 금지 — 포탈 커밋이 press 프레임에 상태를 지워 릴리즈 가드는 무효다, critic H1),
   **이동 임계**(`eventData.pressPosition→position` 거리 ≤ 임계, SerializeField)로 보드 스와이프를
   탭에서 걸러낸다(critic M2). 좌표는 `eventData` 를 쓴다(`Pointer.current` 금지).
4. **커밋 경로 단일.** 탭 즉발도 `CommitAttach → HandChanged(Used) → OnCardUsed` 를 그대로 지난다
   — 유지/자동닫힘/재딜인/무차감 거절(use-flow 계약)이 자동 적용. 별도 소비 경로 신설 금지.
5. **즉발 유효성 = 커밋 거절과 UI 일치**: `CanUse`(게이지) AND `CanAttachMore` AND
   `WouldDreamcatcherCardApply` — D&D `_attachable` 스냅샷과 동일 3판정(코드 검증 완료).
   불가 = 움찔 + 기존 브리핑 문안 재사용, 차감 0.
6. **`DreamcatcherFocusPresenter` 는 단일 세션.** 카드 조준 `Begin` 이 선택 리티클(`Selected`)을
   대체하는 것은 정상. 재주장 트리거는 인터랙션 종료 하나가 아니라 **"프레젠터 세션이 남에 의해
   종료됐다" 신호 전체**다(critic H2): ①슬롯 종료 깔때기(`NotifyInteractionEnded`) ②뷰의
   `Close()/ForceClose()` 가 `_focus.End()` 직후 발화하는 `FocusCleared` 이벤트 ③슬롯 `OnDisable`
   경로 커버. `Close()` 내부에서 `_focus.End()` 는 `CancelAllCardInteraction()` **앞**으로 옮긴다
   (뒤에 두면 방금 재주장한 리티클을 같은 함수가 지운다). 세션 스택 금지(과잉 추상화).
7. **닫힘 비대칭 (critic H3 재정의)**: **닫기 의도 탭**(빈 보드/선택 유닛 재탭)은 **선택 유무와
   무관하게** 손패를 닫는다 — 항아리 단독 오픈의 바깥 탭 dismiss(orb-dock 계약)를 보존한다.
   그 외 선택 해제 경로(사망·이동모드·페이즈·트레이 드래그)는 선택이 있었을 때만 손패를 닫는다.
   컨트롤러가 뷰를 닫는 공개 창구는 `CloseFromSelection()` **하나**다(뷰 `Close()` 는 private 유지).
   **손패 단독 닫힘이 선택을 데려가는 예외 2건** — 둘 다 "이 선택은 끝났다" 가 확정된 순간이다:
   **① unit 8** 카드를 **써서** 사용 가능 0장이 된 자동 닫힘(자원 소진). 게이지 0 인 채 선택만
   한 경우는 해당 없다 — 신호가 `OnCardUsed` 안에서만 나오므로 "사용이 있었다" 가 내포된다.
   **② unit 9** 선택 중 **각성 버튼(항아리) 탭**(명시적 그만하기). 무선택 오픈의 dismiss 는
   기존 그대로 손패만 닫는다. 이 둘로 "선택 있음 + 손패 닫힘" 상태는 도달 불가가 된다.
   유닛 사망은 부착 0장이면 `AttachmentsChanged` 가 안 오므로(critic M3) **앵커 소실 연속
   N프레임 → 선택 해제**를 컨트롤러 수명 규칙으로 추가한다.
8. **슬로모 소유권 분리 (사용자 결정 5)**: 손패는 자기 오픈이 선택 기인인지 안다
   (`SelectionTarget != Entity.Null` 로 파생 — 별도 상태 저장 금지). **선택 모드** = 선택
   lease(0.3×, 상시)가 슬로모를 소유하고 손패 held lease 는 잉여(동일 priority/scale 라 기계적
   무해 — `TimeManager` 승자 규칙 검증 완료). **일반 모드** = use-flow 계약 1 그대로(held 만).
   README 의 이 조항이 use-flow 계약 1 의 명시 예외 기록이다.
9. **카메라는 CameraDirector 채널 합성에 맡긴다** — 인스펙트 줌 + 손패 헤드룸은 독립 가중치
   채널. 단 조준 중 줌은 계약 2 의 피드 중단이 해소한다. 체감은 Play 검증 항목, 튜닝은 config 노브로만.
10. **수치는 SerializeField/SO** (움찔 트윈, catcher 이동 임계 등). 하드코딩 금지(제약 6).
11. **선택 정보의 창구는 좌측 패널 하나다** (units 11·12). 리티클은 "이 유닛이 대상"만 지시하고
    이름·개수·스탯·부착은 패널이 나른다. 패널은 **앵커를 추종하지 않는다** — 고정 도킹이라
    초점 침범이 0 이고, 선택 전환 시 자리에서 내용만 바뀐다. 유닛↔패널의 공간적 연결은
    리티클이 지므로 **리티클을 없애지는 않는다**. 조준 콜아웃은 역할이 달라(손가락에 가린
    대상의 정체) 그대로 유지한다 — 선택과 조준을 한 규칙으로 묶지 말 것.
12. **실효 스탯은 저장된 값이 아니라 파생값이다** (unit 10). ECS 는 재료(base + 배율)만
    나눠 들고 최종값은 소비 시점에 계산된다(`Health.max` 만 예외로 구워져 있다).
    **표시를 위해 sim 을 리팩터하지 않는다** (사용자 결정 2026-07-30): 산식 결정 로직은
    `ModifierMath`/`ModifierStats` 로 **이미 아키텍처와 분리돼 있고**, 남은 일은 그 plain 값을
    읽어 표시로 해석하는 것뿐이다. 표시 산식이 어긋나면 숫자만 틀리지만(cosmetic)
    `AttackSystem` 회귀는 판정·밸런스가 틀린다 — 심각도가 한 단계 다르므로 교환하지 않는다.
    따라서 units 10~13 은 전부 **ECS 무변경**이고 일반 code-review 대상이다.
    표시값은 **"조건 없는 타격당 피해"** 다: `attackerVsCc`·`frontmostMul`·`dcBounceMul` 은
    대상·시점 의존이라 유닛 하나로 값이 정해지지 않아 **넣을 수 없다**(사양이지 결함이 아니다).
    델타는 기본값 대비 증감이고 epsilon 내면 sign 0(칩 숨김)이라 `▲0` 노이즈가 없다.
    기본값은 선택 시 1회 캐시, 실효값은 매 프레임(대상 1 엔티티라 비용 무시 가능).
13. **카메라 부각은 dolly 단독이 아니라 dolly + FOV 합성이다** (unit 13). 그리고 `_inspectNdc`
    는 목표값을 **추종**한다 — 단, **가중치가 0 에서 올라오는 첫 피드는 스냅**한다.
    안 그러면 이전 유닛에서 가로질러 날아온다(선택 리티클의 "pop" 규칙과 같은 이유).

## 상태 꼬임 지점 → 해소 매핑 (critic 검증 완료)

| 충돌 | 근거 | 해소 |
|---|---|---|
| `Blocked()`: 손패 오픈 = 선택 강제 Close | `DcInspectController.Blocked()` | unit 0 분리 |
| `IsAiming` = 선택 Close + 조준 중 인스펙트 줌 잔존(M4) | 〃 + `CameraDirectionConfig` dolly 4.92 | unit 0 (gate 이동 + 줌 피드 중단) |
| 손패 오픈 중 보드 탭 전량 catcher 소유 | `IsOverUi` 가 catcher 히트 | unit 2 라우팅 |
| 포탈 출구 탭: 커밋=press, catcher 클릭=release → 릴리즈 가드 무효(H1) | `DreamcatcherCardDragSlot.cs:304-330` | unit 2 press-스냅샷 |
| catcher 이동 임계 부재 = 스와이프가 탭(M2) | Button 은 `IDragHandler` 없음 | unit 2 탭 캐처 재설계 |
| `Close()/ForceClose()/슬롯 OnDisable` 의 `Focus.End()` 가 재주장 깔때기 밖(H2) | `DreamcatcherHandView.cs:772·788` | unit 4 FocusCleared |
| 부착 0장 유닛 사망 = 이벤트 없음 → 좀비 선택(M3) | `DreamcatcherHandController.OnDefenderDied` 조기 return | unit 1 앵커 liveness |
| 침강 0.4초 `Transitioning` 창 = 손패 없는 선택(H5) | `StartSink`+strip fold | unit 1 pending-open 래치 |
| 상시 슬로모 × 상시 손패(H4) | 선택 lease 타임아웃 없음 | 계약 8 (사용자 결정 5) |
| 이동모드 × 항아리 오픈 = 목적지 지정 봉쇄(M6) | catcher 가 `IsOverUi` 히트 | unit 2 (수신 게이트 + relocation catcher 예외) |

## 파이프라인 커버리지

N/A — 플레이 오브젝트 신설/생성→렌더 경로 변경 없음. 기존 UGUI·커밋·연출 재사용.

## 설계 리뷰 이력

- **2026-07-29 critic REVISE → rev 2 반영.** H1 포탈 release-가드 무효(press 스냅샷으로 재설계) ·
  H2 `Focus.End()` 다중 소유(재주장 신호 확장) · H3 unit 1↔2 모순(닫기 의도 탭 규칙 재정의) ·
  H4 상시 슬로모(사용자 결정 5 로 명문화) · H5 Transitioning 창(래치). M1 "드래그가 클릭을
  삼킨다"는 오해였다(`eligibleForClick` 은 `pointerPress != pointerDrag` 일 때만 해제 — 명시 가드로
  교정, 레포 반례 `DraftCardView.cs:101`). M7 `Tween.PunchAnchoredPosition` 은 존재하지 않는 API 였다.
  검증 통과 항목(계약 4·5·8 기계층·11 계승, canvas order 10/9>5, 씬 배선 0)은 유지.

## 후속 후보 (범위 밖)

- **탭 즉발의 Active-DefenderUnit 확장** — 사용자 결정으로 이번엔 부착만. 체감 후 재평가.
- **선택 유닛 강조 base-ring** — 리티클과 중복이라 보류.
- **손패만 닫고 유닛 유지하는 2단계 해제** — Q3 에서 기각(동시 해제 채택). 불편 보고 시 재고.
- **카드 탭 즉발의 언두/확인 스텝** — 오탭 부착 보고가 나오면 검토.
- **겹친 유닛 픽킹 "렉트 중심 최근접"** — inspect 후속 후보와 동일 항목이 즉발 대상 결정에도 관여하게 됨. 실기기 오탭 체감 시 승격.
- **첫 세션 튜토리얼 문안** — `HandOpened` 원샷 힌트가 탭 즉발을 가르치지 않음(critic L4). 체감 후 문안 개정.
