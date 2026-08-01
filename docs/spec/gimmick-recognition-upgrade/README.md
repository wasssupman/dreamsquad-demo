# gimmick-recognition-upgrade

> 상태: **완료 2026-08-01** (unit 0~3, `afada286`~`921829a4`). **사용자 Play 확인 통과** — 선물→리빌→배치 흐름 · 등장음 톤(Take1) · 첫 세션 튜토리얼 · 배치 페이즈 카드 미노출. 매판 달라지는 기믹을 **진입 순간 한 번**에 인지시킨다. 인계: [`4_handoff_summary.md`](4_handoff_summary.md). 후속 후보는 `docs/spec/README.md` Follow-up Backlog → "기믹 인지".

## 상위 목표

기믹이 4종 돌아가는데 플레이어가 **어떤 기믹인지 모른다**는 피드백을 해소한다. 원인 두 가지 — ① 이름이 정서 카피뿐이라 룰을 유추할 수 없고, ② 안내가 배치 페이즈 우상단에 3초 뜨고 접혀 배치 조작과 인지 예산을 다툰다.

해법의 무게중심은 **진입 순간**이다. 전투 중 상시 배지는 만들지 않는다 — 정보량이 과하고 UI 영역을 먹는다(2026-07-31 사용자 결정). 대신 진입 리빌 하나가 제 몫을 하도록 **정보량을 줄이고 채널을 늘린다**: 텍스트 3줄 → 아이콘 + 룰 라벨 + 한 줄 + 색조 + 움직임 + 효과음.

## 검증 질문

1. **"리빌을 한 번 보고 이번 판이 무슨 기믹인지 말할 수 있는가?"** — 아이콘 + 2~4자 룰 라벨 + 한 줄.
2. **"리빌이 배치 조작과 경쟁하지 않는가?"** — 배치 타이머 시작 *전*에 끝난다.
3. **"리빌이 끝난 뒤 화면에 흔적이 남지 않는가?"** — 배치 화면은 지금과 동일하게 깨끗하다.
4. **"기믹 비활성 매치에서 완전 무변화인가?"** — 페이즈 자체를 건너뛴다.

## 배경 (현행 구조)

- 안내 = `GimmickGuideView`(`Scripts/UI/GimmickGuideView.cs`) 하나. `Placement` 에서만 우상단 카드 슬라이드-인 → 3초/첫 드래그/"가즈아" 탭에 칩으로 접힘 → Placement 이탈 시 소멸.
- 문구 = `GimmickData.displayName`(정서 카피) + `description`(3줄). 룰을 말하는 필드가 없다.
- 페이즈 = `GamePhase { None, Draft, Gift, Placement, Battle, Result, Tally }`(`Core/GameManager.cs:17`).
- 배치 진입 = `GiftPhaseView`(5비트 서사) → `PlacementPhaseView.BeginPlacementPhase()`(그 안에서 `SetPhase(Placement)` + 코스트 리셋 + `bridge.BeginPlacement()` + 30초 타이머).
- 배정 = `GameManager.AssignGimmick`(매치당 1회, 결정론). 이 spec 은 **배정 로직을 건드리지 않는다**.

## 에셋 실사 (2026-07-31, 스코프 결정 근거)

| 기믹 | 보유 자산 | 리빌 활용 |
|---|---|---|
| 번아웃 | `VFX/Burnout_Smoke.prefab` (실사용 중) | 슬롯에 즉시 연결 |
| 레드불 | 3D 모델 + `Models/Redbull_Mat.mat`, `VFX/LastRun_Torchlight.prefab` | 슬롯에 즉시 연결 |
| 사직서 | 없음 — `ResignationPresenter` 가 절차적 흰 큐브 | 슬롯 비움 → 절차적 폴백 |
| 과열 | 없음 (Onsen spec 이 전용 FX 를 후속으로 보류) | 슬롯 비움 → 절차적 폴백 |

에셋 편차가 크므로 **"4종 전부 보드 위 시연" 은 스코프에서 뺀다**. 보드 위 3D 연출은 이 프로젝트에서 이미 데인 곳이기도 하다(벤더 VFX 3대 함정: 비활성 그룹 · 정렬 대역 충돌 · 바닥 평면 XY↔XZ 불일치 — `docs/reference/lessons/`). 연출을 아트 의존에서 떼어내 **전면 오버레이 + nullable VFX 슬롯**으로 간다.

## feature-wide 계약

1. **문구는 4단, 각자 자기 화면 하나만 책임진다.** `ruleLabel`(2~4자) · `summary`(15~25자 한 줄) **신설**, `displayName`(정서 카피, 리빌 부제) · `description`(3줄 상세) **유지**. 지금 `displayName` 이 룰 자리까지 겸직해서 생긴 문제가 이 spec 의 출발점이다. **대상 뱃지 필드는 만들지 않는다** — `summary` 문구가 이미 대상을 말한다("**내 유닛은** 오래 둘수록"). 필드를 늘리는 건 이번 지적("정보량 과다")과 어긋난다.
2. **`GamePhase.Gimmick` 은 enum 맨 뒤(7)에 append.** `Data/Camera/CameraDirectionConfig.asset` 이 페이즈를 raw int 로 직렬화한다(`phase: 1/3/4/5`, `breathPhases: 010000000300000004000000`). `Gift` 뒤에 끼워 넣으면 `Placement` 이후가 한 칸씩 밀려 카메라 포즈·브리딩이 어긋난다. 시간 순서(Gift→Gimmick→Placement)와 enum 값이 어긋나는 건 `Tally` 가 만든 전례다. 전 코드가 `==` 비교라 순서 의존은 없다.
3. **리빌은 배치 타이머 앞에서 완결된다.** `BeginPlacementPhase()` 가 타이머·코스트·`bridge.BeginPlacement()` 를 한 묶음으로 시작하므로 연출은 그 호출 **이전**에 끝난다. 연출 중 배치 입력은 존재하지 않는다(검증 질문 2).
4. **훅 지점은 `GiftPhaseView.ProceedToPlacement()` 하나.** 선물 페이즈의 단일 퍼널이라 정상 종료(:625)·첫 판 튜토리얼 스킵(:136)·`giftConfig` 미배선(:150)·TestMode fast-forward(:158)가 전부 여기로 모인다. `BeginGift()` 의 미배선 폴백(:125)과 `BattleBridge.EnterPlacementOrGift()`(:451)는 **선물 뷰 자체가 없을 때의 fail-open 경로**라 손대지 않는다 — 연출은 부가물이고 배치 도달이 우선이다. 재시작(`OnRestartRequested`)은 result-screen-lobby-exit unit 0 으로 호출처가 제거돼 현재 dormant.
5. **연출 자산은 nullable 슬롯.** `GimmickRevealConfig` 가 기믹당 `{ tintColor, revealVfxPrefab, sfxClip }` 을 갖되 프리팹·클립은 **null 허용**이며 없으면 절차적/무음 폴백. 번아웃·레드불은 기존 프리팹을 꽂아 즉시 연출이 붙고, 사직서·온천은 비운 채 균일하게 돈다. 나중에 아트가 생기면 **코드 0줄로** 슬롯에만 꽂는다. `StackIconRegistry.IconFor` null 폴백과 같은 디커플링 패턴.
6. **리빌이 끝나면 흔적 0.** 배치·전투 화면에 기믹 UI 를 남기지 않는다. 상시 배지·칩·진행 표시는 이 spec 의 **비목표**다.
7. **기믹 없음 = 완전 무변화.** `AssignedGimmick == null` 이면 페이즈를 건너뛰고 기존 클린 플레이 경로 그대로.
8. **첫 판은 리빌 생략.** 첫 세션 튜토리얼은 배치 자체를 배우는 판이다. `TutorialProgress.ShouldRunCore(profileSO)` 로 판정 — 훅 지점이 튜토리얼 스킵 경로까지 삼키는 퍼널이라 **리빌 뷰가 스스로** 판정해야 한다.
9. **하드코딩 금지 유지.** 문구·아이콘·색조·프리팹·클립·타이밍 전부 SO(`GimmickData` / `GimmickRevealConfig`). UI 고정 문구("이번 판 특수 룰")만 코드.
10. **커밋 격리 = 롤백 가능성.** 아래 참조.

## 커밋 격리 규율 (2026-07-31 사용자 요구)

각 유닛이 **독립적으로 revert 가능**해야 한다. 그러려면 두 규칙을 지킨다.

- **삭제를 신규와 같은 커밋에 섞지 않는다.** 기존 배치 카드의 은퇴는 리빌 신설과 **별도 유닛(3)** 이다. unit 1 착지 시점엔 리빌과 기존 카드가 잠시 **공존**한다 — 중복이지만, 리빌을 실기로 보고 판단한 뒤 카드를 걷어낼 수 있다. 리빌이 별로면 unit 3 를 안 하거나 unit 1 만 revert 하면 기존 카드가 그대로 남는다.
- **의존은 한 방향(0→1→2→3).** 역순 revert 가 성립한다. 효과음(2)을 신설(1)에서 떼어낸 것도 같은 이유 — 소리가 어색하면 그 커밋만 되돌린다.

공유 워크트리 위생: 병행 세션이 있으므로 **스테이징은 경로 명시로만** 하고, 씬 변경은 내 hunk 만 격리한다. 스테이징한 채 대기하지 않는다.

## 작업 단위

| 파일 | 작업 구분 | 문서 | revert 시 | 상태 |
|---|---|---|---|---|
| 0 | 데이터 | `0_copy_fields_and_icons.md` | 문구만 원복, 기존 카드 정상 동작 | 완료 |
| 1 | 신규 페이즈 | `1_gimmick_phase_reveal.md` | 리빌 사라짐, 기존 카드 복귀 | 완료 |
| 2 | 사운드 | `2_reveal_sfx.md` | 무음 리빌로 복귀 | 완료 |
| 3 | 은퇴 | `3_guide_card_retirement.md` | 기존 배치 카드 부활 | 완료 |
| 4 | handoff | `4_handoff_summary.md` | — | 완료 |

## 파이프라인 커버리지

N/A — 데이터 SO 문구/아이콘 + MonoBehaviour View(리빌) + 페이즈 enum 만 다룬다. 새 플레이 오브젝트나 생성→렌더 경로 신설·변경 없음. 기존 VFX 프리팹은 **소비만** 한다(`revealVfxPrefab` 슬롯). 기믹의 플레이 오브젝트(레드불·사직서)는 각 season-gimmick spec 에서 파이프라인 등록 완료. `docs/reference/object-pipeline-map.md` 신규 대조 대상 아님.

## 비목표

- 전투 중 상시 기믹 배지/칩/진행 표시 (2026-07-31 사용자 결정 — 정보량 과다, UI 영역 점유).
- 보드 위 3D 시연형 연출 (에셋 편차 + 벤더 VFX 3대 함정).
- 기믹당 보이스 라인 (사용자 경험상 어색 — 효과음으로 대체).
- 기믹 룰·수치·배정 로직 변경.

## 후속 후보 (현 spec 범위 밖)

> **2026-08-01 spec 종료와 함께 `docs/spec/README.md` 의 Follow-up Backlog → "기믹 인지" 로 이관됨.** 아래는 spec 진행 중의 기록이고, 최신 목록은 백로그가 source of truth 다.

- **회상 경로** — `MenuPopup`(일시정지)에 `description` 한 줄, 결과 화면에 "이번 판 특수룰" 한 줄. 신규 UI 영역 0 이고 룰↔결과 인과를 붙여 다음 판 인지를 올린다. 이번엔 "진입 시점에만 집중" 지시로 제외.
- **진행 표시 + 발동 펄스** — 사직서 3/5, 열기 4/6, 임계 도달 시 신호. ECS→Bridge→View 데이터 seam 이 필요해 성격이 다르다.
- **사직서·온천 전용 리빌 VFX** — `revealVfxPrefab` 빈 슬롯에 꽂기만 하면 되도록 구조는 이 spec 에서 열어둔다.
- 기믹별 효과음 4종 세분화(이번엔 공용 1개 + nullable 오버라이드).
- 로비에서 다음 판 기믹 예고 (배정이 배틀 씬 `GameManager.Start` 라 구조 변경 필요).
- `GimmickData` 시트 임포터 — 문구가 4단으로 늘어 시트 관리 가치가 올라간다.
