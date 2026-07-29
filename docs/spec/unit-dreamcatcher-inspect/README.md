# Unit Dreamcatcher Inspect — 배치 유닛 탭 → 부착 드림캐쳐 상세

> 상태: **완료 2026-07-15** (units 0~5, 사용자 Play 확인 통과) · **unit 6 추가 완료 2026-07-29** (선택 리티클, 사용자 Play 확인)
>
> 커밋: `71fc4679`(기능 units 0~4) · `f54909de`(origin/main merge) · `5d7a2585`(탭 입력 잠복 결함 수정 — 이 spec 이 첫 보드 raw 탭 소비자라 드러남).
> 설계 critic 1회(REVISE→반영) + 코드리뷰 1회(APPROVE-WITH-CHANGES→전건 반영) + 사용자 Play 확인.
> 잔여: 실기기(Android) 터치 체감 · 포탈 2탭/카드 부착 touchup 직후 미개방(실제 포인터 입력 필요) — `3_wiring_play_validation.md` 하단.
>
> 배경: `docs/spec/unit-dreamcatcher-icons/` 후속 후보 "**아이콘 탭 → 카드 상세** [S] · 부착 카드 확인 UX" 의 승격.

## 목표

전투/배치 중 보드의 방어유닛을 탭하면, 그 유닛에 부착된 드림캐쳐 카드(Unit 부착 + Squad hosted)의 **성능 텍스트**를 유닛 옆 스택 패널로 띄운다. 패널이 열려 있는 동안 Battle 도메인에 슬로우 모션을 건다(배치 드래그·손패 오픈과 같은 문법). 순수 프레젠테이션 — ECS 변경 0, 채널 0, 신규 에셋 저작 0.

`unit-dreamcatcher-icons` 가 "**무엇이** 붙었나"(머리 위 미니 타로 아트)를 답했다. 이 spec 은 "**그게 뭘 하나**"(텍스트)를 답한다.

## 검증 질문

> 전투 중 배치된 유닛을 탭하면, 붙어 있는 드림캐쳐가 무엇을 하는지 슬로우 모션 속에서 읽고, 다시 탭해 닫을 수 있는가?

## 데이터 소스 (실측 2026-07-15)

- **부착 목록**: `DreamcatcherHandController.GetAttachments(List<(Entity host, DreamcatcherCard card)>)` + `AttachmentsChanged` 이벤트. 카드↔유닛 대응의 유일한 SoT (ECS `DcTriggerSlot` 은 베이크 값만 있고 카드 SO 참조가 없다 — Squad 카드는 슬롯을 아예 안 만든다).
- **픽킹**: `BattleBridge.TryPickDefenderAtScreen`(스파인 몸체 스크린 렉트) 1차 + `TryScreenToCell`→`TryGetDefenderAt`(발밑/quad 뷰) 2차. `DreamcatcherCardDragSlot.UpdateUnitHover` 와 같은 2단 패턴.
- **앵커**: `BattleBridge.TryGetUnitViewAnchor` — spine/enemy/fallback 풀 순회라 quad 뷰까지 커버.
- **텍스트**: `DreamcatcherCardText.Body(card)` 공용 포맷터 + `DreamcatcherHandController.CostOf(card)`.
- **상한**: `AwakeningConfig.maxAttachPerUnit`(3, Unit+Squad 합산) → 고정 3행, 오버플로 UI 불필요.

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_drag_state_read_api.md` | 계약(seam) | `DefenderDragPlacementController.IsDragging` 읽기 노출 — 기존 로직 변경 0 |
| 1 | `1_tap_select_and_slomo.md` | 입력+상태 | `DcInspectController` — press 픽킹 2단, 선택 토글, 슬로우 lease, 게이트·닫힘 경로 전량 |
| 2 | `2_inspect_panel_view.md` | 뷰 | `DcInspectPanelView` — 스택 행, LateUpdate 앵커 추종+플립, 툴팁 시각 문법 |
| 3 | `3_wiring_play_validation.md` | 배선+검증 | 씬 배선 + Play e2e |
| 4 | `4_unit_zoom_focus.md` | 범위+카메라 | 전 유닛 트리거(계약 8 개정) + 선택 유닛 줌인(CameraDirector 채널) |
| 5 | `5_handoff_summary.md` | 인계 | 종료 요약 + 되돌리지 말 것 |
| 6 | `6_selection_reticle.md` | 뷰(추가 2026-07-29) | 선택 시 조준 락온 리티클+콜아웃(portrait+이름) 재사용 — `DreamcatcherFocusPresenter.BeginSelection` |

## Feature-wide 계약

1. **읽기 전용 프레젠테이션.** ECS 컴포넌트/시스템/채널 변경 0. 부착 SoT 는 `DreamcatcherHandController` — 뷰는 그것만 믿는다(icons 계약 1 계승, ECS `DcTriggerSlot` 재독 금지).

2. **픽킹 재사용.** `TryPickDefenderAtScreen` 1차 + `TryScreenToCell`→`TryGetDefenderAt` 2차 폴백. **신규 Collider / `Physics.Raycast` 도입 금지** — 프로젝트에 하나도 없고, 틸트 빌보드라 보드 평면 레이캐스트는 몸체 포인팅을 놓친다(`SpineUnitView.cs:198` 주석). `TryScreenToCell` 사본 금지(`BattleBridge.cs:2704~2707` 계약).

3. **탭 = UI 밖 프레스다운으로 무장 → 릴리즈에 발동 + 이동 임계 24px** (rev: defender-relocation UX, 2026-07 — 원문 "press 규약·임계 금지"는 은퇴). 터치다운 즉시 발동하면 1초 홀드/드래그 조작을 방해해 릴리즈 판정으로 바뀌었고, `tapMoveThreshold`(24px)가 드래그 의도를 거른다. 원래 press 규약의 근거였던 "카드 부착 touchup 이 release 로 패널을 여는 문제"는 재발하지 않는다 — 탭 후보가 **UI 밖 프레스다운에서만** 무장되는데 카드 드래그의 프레스다운은 손패 카드(UI) 위이고, 손패 오픈 중엔 게이트가 막는다(`DcInspectController.cs:97~124` 주석이 현행 SoT). *(2026-07-29 selection-hand-attach critic L3 — stale 계약 교정.)*

4. **실행 순서 `[DefaultExecutionOrder(-50)]`** — `PlacementInput` 과 동렬. 포탈 2탭(`DreamcatcherCardDragSlot.Update`, order 0)이 같은 프레임에 `EndInteraction` 으로 `IsAiming` 을 내리기 **전에** 읽어야 한다. 늦게 읽으면 포탈 출구를 확정한 그 탭이 패널을 연다(`PlacementInput.cs:12~18` 이 기록한 aim-mode race 와 동형).

5. **배타는 파트너별 구체 신호로.** `GameManager.AimCanceled` **사용 금지 — 죽은 코드**(발행자·구독자 0, 마지막 발행처가 `8f52648c` 에서 삭제됨. `GameManager.cs:62~66` 주석은 미구현 설계 의도다).
   - 손패 오픈 → `DreamcatcherHandView.State == HandState.Hand`
   - 배치 드래그 → `DefenderSelector.DragController?.IsDragging` (unit 0 seam 2점). 컨트롤러 자체는 런타임 `AddComponent` 라 씬 배선 불가 — 수명 소유자 경유가 유일한 도달 경로다
   - Active/포탈 조준 → `GameManager.IsAiming` (Active 전용이지만 여기선 실제로 동작) + 계약 4 의 실행 순서
   - Unit/Squad 부착 드래그 → 별도 게이트 불필요. press 가 UGUI 손패 슬롯에서 시작하므로 UI 가드(아래)가 배제한다
   - 일시정지(`MenuPopup`, priority 100/scale 0) → 게이트 불필요. dim(sortingOrder 960)이 패널(9) 위를 덮는다

5b. **UI 가드는 `EventSystem.IsPointerOverGameObject()` 를 쓰지 않는다 — 즉석 `RaycastAll` 로 한다.**
   그 API 는 `EventSystem.Update` 가 세운 **지난 프레임** pointer 상태를 읽는다(`InputSystemUIInputModule`: 음수 id → `m_PointerStates[..].eventData.pointerEnter`, 그리고 *"calling this method earlier than that in the frame will make it poll state from last frame"* 명시). `EventSystem` 은 실행 순서 **0**(`[DefaultExecutionOrder]` 없음, 프로젝트 커스텀 오버라이드도 없음)인데 이 컨트롤러는 계약 4 때문에 **-50** — 항상 먼저 돈다. **터치는 hover 가 없어 press 프레임에 pointer 상태 자체가 없다** → 손가락이 UI 위에 있어도 `false` → 트레이/버튼을 눌러도 그 뒤 유닛이 선택된다.
   마우스에선 hover 잔상이 이 결함을 가리므로 **에디터에선 절대 재현되지 않는 Android 전용 버그**다. `PlacementInput.cs:63~65` 가 같은 패턴을 쓰지만 클릭 배치가 은퇴(`clickPlacementEnabled=false`)해 아무도 밟지 않았다 — **선례로 삼지 말 것.**
   → `EventSystem.current.RaycastAll(new PointerEventData(es){ position = screenPos }, hits)` 로 대체(실행 순서 무관, press 때만 도는 경로). 패널 자신은 전 Graphic `raycastTarget=false` 라 이 레이캐스트에 안 걸린다 = 패널 위 탭은 "빈 보드"로 취급돼 닫힌다(의도).

6. **앵커 추종은 `LateUpdate`.** `CameraDirector`(`[DefaultExecutionOrder(-90)]`)가 LateUpdate 에서 포즈를 확정한다. `Update` 추종은 지난 프레임 카메라를 읽어 킥/브리딩/페이즈 pitch 전환 중 패널이 유닛에서 미끄러진다(`DcIconStripView.cs:81~88` 선례, 커밋 `d815bf59`). 화면 밖/카메라 뒤(`z <= 0`)면 숨김.

7. **시간 제어는 TimeManager lease.** `Time.timeScale` 은 1 고정, 재구현 금지. 스케일 절대 0 아님(`AwakeningConfig.slomoTimeScale`, 손패와 동일 노브). 모든 닫힘 경로 + `OnDisable` 에서 Dispose(멱등). 손패와 동시 생존해도 무해(둘 다 priority 50 + 동일 scale → 유효 스케일 불변).

8. **선택은 부착과 무관** (rev 2, 사용자 결정 2026-07-15 — unit 4). 부착 0장 유닛도 탭하면 선택 + 줌 + 슬로우가 걸리고, **패널만** `_cards.Count > 0` 일 때 뜬다. 빈 상태 UI 는 만들지 않는다 — 줌 자체가 "이 유닛을 보고 있다"를 전달한다.
   **닫힘 경로 전량** — 같은 유닛 재탭(토글) / 다른 유닛 탭(전환) / 빈 보드 탭 / 손패 오픈 / 배치 드래그 / 조준 / 페이즈 이탈 / 선택 유닛 **사망**(앵커 소실) / `OnDisable`. 전 경로에서 lease 해제 + 줌 원복.
   *(rev 1 의 "부착 0장 = 무반응" 은 폐기 — 줌이 붙으면서 근거가 사라졌다. 부착이 0장이 된 것만으로는 닫지 않는다: 카드를 다 잃어도 유닛은 살아있다.)*

9. **페이즈 구동 teardown.** `GameManager.PhaseChanged` 에서 `phase != Placement && phase != Battle` → 강제 닫기(`DreamcatcherHandView.OnPhaseChanged` 선례). **BattleBridge teardown 훅 불필요** — icons spec 이 `d815bf59` 에서 `dcIconStripSpawner.Clear()` 를 뒤늦게 배선해야 했던 건 월드 스프라이트가 앵커 파괴 후에도 잔류하기 때문이다. UGUI 패널은 그 잔류가 없다. (선택 유닛 사망은 `AttachmentsChanged` 가 별도 구동.)

10. **`AttachmentsChanged` 구동 리빌드.** per-frame 은 앵커 추종만.

11. **보드 raw 탭 소비자는 `DcInspectController` 단일.** 후속(범위 표시 등)은 두 번째 탭 소비자를 만들지 말고 이 컨트롤러의 선택을 구독해 확장한다. 추상화를 미리 만들라는 뜻이 아니다(제약 8 준수) — 두 번째 소비자가 aim-mode race 를 재생산하는 것을 막는 계약이다.

12. **수치는 SerializeField / SO** (패널 색·보더·폭·간격·오프셋 = SerializeField, 줌 튜닝 = `CameraDirectionConfig`). 하드코딩 금지(제약 6).

13. **카메라는 `CameraDirector` 가 유일한 쓰기 주체** (기존 계약). 인스펙트는 **타겟만 피드**하고 카메라를 직접 만지지 않는다. 줌 NDC 는 **홈 포즈 기준**으로 산출 — 현재 포즈로 뽑으면 다가갈수록 오프셋이 사라져 진동한다. 피드 staleness(2프레임)로 자동 해제되므로 명시 Clear 불필요.

## 기각된 대안

**머리 위 아이콘 스트립 확장** (`DcIconStripView` 에 탭→확장). 스트립은 이미 앵커/수명주기/teardown 배선을 갖춘 가장 가까운 친척이지만, **월드 SpriteRenderer**(sortingOrder 14500, 빌보드)다. 리치 텍스트(색 태그 effects 줄 + description 블록)를 넣으려면 월드 캔버스/TMP 3D 가 필요하고 카메라 pitch(Draft 40°↔Battle 58°)·줌·외곽 타일 원근에서 가독성이 계속 흔들린다 — 정확히 `d815bf59` 가 싸운 축이다. 사용자가 지정한 시각 문법("덱 툴팁과 비슷한 형태")도 UGUI 다.
**역할 분담**: 스트립 = 무엇이 붙었나(어포던스), 패널 = 그게 뭘 하나(상세). 패널이 열려도 스트립은 유지한다.

## 스코프 밖 / 후속 후보

- **겹친 유닛 픽킹이 "렉트 중심 최근접" 이라 엉뚱한 유닛을 고를 수 있다** [S] · `BattleBridge.TryPickDefenderAtScreen` 은 점을 포함하는 렉트들 중 **중심이 가장 가까운** 것을 고른다 — 깊이(카메라 거리)를 안 본다. 실측(2026-07-15, 유닛 8기): 21케이스 중 1건에서 옆 유닛이 잡혔다(발 부분). 스크린 렉트가 loose AABB(무기까지 포함)라 인접 유닛끼리 겹친다. 정답은 **깊이 순 우선**(화면에 앞에 그려진 것) 또는 픽셀/콜라이더 정밀 판정. 현재 체감엔 안 걸리고, **카드 드래그 호버(`DreamcatcherCardDragSlot`)와 공유하는 계약**이라 바꾸면 그쪽도 같이 바뀐다.
- **배치 직후 1프레임 렉트가 작게 잡힌다** [S] · Spine 스켈레톤이 포즈 잡기 전 `MeshRenderer.bounds` 가 작다(실측 96x57 → 정착 후 161x101). 정착 후에는 안정적(meshY 0.737~0.739). 배치 직후 즉시 탭하는 경로에서만 문제.

- **트리거 진행도 뱃지** [S/M] · "4/5" 라이브 카운터. `DcTriggerSlot.counter`(Combat) 읽기에 BattleBridge 스냅샷 경로 신설 필요 + 같은 카드 2장 부착 시 entryId↔슬롯 매핑 부재(`instanceId` 는 ECS 쪽만). icons spec 후속 후보와 동일 항목.
- **이미 배치된 유닛 선택/탭 시 범위 표시** [S] · `docs/spec/README.md` Follow-up Backlog(range-preview 출처). 같은 탭 제스처 — 계약 11 로 확장 경로를 열어둠.
- **유닛 스탯 병기** · hand-drag-tooltip 후속 후보("Defender 조준 중 호버한 유닛의 스탯을 툴팁에 병기")와 병합 후보.
- **세로 오버플로 클램프 고도화** · 1차는 좌우 플립 + 상하 클램프. 3행 스택이 화면 상/하단에 걸릴 때의 정밀 배치는 Play 판정 후.

## 파이프라인 커버리지

N/A — 플레이 오브젝트 신설/생성→렌더 경로 변경 없음. 순수 UGUI 위젯 + 기존 픽킹 재사용.

## 설계 리뷰 이력

- **2026-07-15 critic REVISE → rev 2 반영.** rev 1 은 배타 메커니즘을 `IsAiming`/`AimCanceled` 재사용으로 설계했으나 `AimCanceled` 가 죽은 코드이고 `IsAiming` 은 Active 전용이라 성립 불가(C1). 좌표 함정도 절반만 우회(월드 +Y 는 회피, 실행 순서는 전이)(M1). 레이스 상대 오인(Defender 조준 → 실제는 포탈 2탭)(M2). teardown 경로 미지정(M3). 변경 표면 2파일 → 4파일(M4). 탭 소비자 단일성 계약 누락(M5). 실패 패턴 = **주석-깊이 조사, 호출처-깊이 아님** — 라인 번호는 다 맞았고 의미가 틀렸다.
