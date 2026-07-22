# dreamcatcher-orb-dock

> 상태: **초안 2026-07-23** — 사용자 승인 대기. 배경: `docs/plans/2026-07-23-dreamcatcher-orb-dock-design.md`

## 목표

우하단 각성 버튼을 제거하고, 통합 트레이 오른쪽에 **인접하되 분리된 드림캐쳐 구슬 독**을
신설한다. 구슬 안에는 스러진 유닛들의 미니 피규어(Spine 프리즌, 스케일 ~0.2)가 물리로
떨어져 쌓이며, 더미의 부피가 곧 각성 재화 게이지다. 구슬 탭=손패 열기, 재탭·손패 바깥
탭=닫기. 킬/아군 사망 위치에서 구슬로 날아오는 흡수 비행이 도착하는 순간 피규어가 떨어져
축적의 인과가 눈에 보인다.

검증 질문: *"모바일 가로 양손 그립 플레이 중, 시선·엄지 동선 위에서 각성 재화의 축적이
읽히고, 드림캐쳐 덱 열기/닫기가 코너 조준 부담 없이 되는가?"*

## 작업 단위

| # | 문서 | 목적 |
|---|---|---|
| 0 | `0_figure_physics_core.md` | 원형 컨테이너 원-원 충돌 순수 시뮬 코어 + EditMode 테스트 |
| 1 | `1_orb_dock_view.md` | 구슬 독 뷰 — 트레이 우측 분리 배치, 탭=`Toggled` 승계, 소형 숫자 병기, 코너 버튼 은퇴 |
| 2 | `2_figure_pool_spawn.md` | 피규어 풀/스폰 — Spine `UpdateMode.Nothing` 프리즌, 게이지 양자화(1피규어=5, 상한 20) |
| 3 | `3_absorb_flight.md` | 흡수 비행 — 킬/아군 사망 월드 위치 → 구슬 화면 위치, 도착 시 피규어 드롭 |
| 4 | `4_spend_and_close_ux.md` | 소비 연출(카드 사용 시 피규어 방출) + 손패 바깥 탭 dismiss |
| 5 | `5_wiring_verification.md` | 씬 배선·튜토리얼 suppress 승계·16:9/20:9 Play 검증 |

## Feature-wide 계약

- **독은 유닛 트레이와 분리된 형제 오브젝트**다. 인접(작은 갭)하되 트레이의 손패 플립에
  불참하고, 손패 열림 중에도 같은 자리에 상주한다(재탭=닫기의 전제). Battle 페이즈 전용
  노출과 튜토리얼 `SetSuppressed` 경로는 기존 버튼에서 승계한다.
- **구슬 = 게이지 + 열기/닫기 토글 단일 오브젝트**. 탭 시 기존
  `Toggled → DreamcatcherHandView` 계약을 그대로 사용하고, open/close 상태 소유자는
  변함없이 `DreamcatcherHandView`다.
- **게이지 1차 표현은 피규어 더미의 부피**다(1피규어=재화 5, 상한 20개=100). 소형 숫자를
  병기하되, `awakening-hud-resource-button`의 "수치 1순위" 계약은 이 spec 이 대체한다.
- **게이지 값의 source of truth 는 `DreamcatcherHandController.Gauge`** 그대로다. 뷰는
  `GaugeChanged`를 구독해 목표 피규어 수를 맞출 뿐, 경제(획득량·코스트·상한)를 변경하지
  않는다. 순수 프레젠테이션.
- **피규어 = 스러진 유닛의 미니어처**. 스폰 시 해당 유닛 스킨의 사망 애니 마지막 프레임을
  1회 적용 후 `UpdateMode.Nothing`으로 동결. 전투 시작 시 풀에 선생성. 실기기 드로우콜
  문제 확인 시에만 스틸 베이크로 후퇴(후속 후보).
- **물리는 순수 함수 시뮬** (제약 10). 원형 경계 + 원-원 충돌의 plain step 함수를 EditMode
  테스트한다. Physics2D·UGUI Mask 금지 — 클리핑은 물리 경계와 림 오버레이 스프라이트가
  대신한다(코스트 물통 스텐실 사고 전례).
- **닫기 UX**: 구슬 재탭 + 손패 바깥 탭 dismiss 둘 다 지원. 카드 드래그 중이거나 드래그
  실패로 카드가 복귀하는 경우는 닫힘으로 승격하지 않는다.
- **우하단 코너는 비운다**. `AwakeningGaugeView`는 은퇴시키고, Placement 의 `전투 시작`
  우하단 계약(`ingame-ui-upgrade`)과 좌하단 `NextWaveDock`은 변경하지 않는다.

## 기존 계약 대체

`docs/spec/awakening-hud-resource-button/README.md`의 "Battle 우하단=각성 버튼" 계약을
본 spec 이 대체한다. 젤리 버스트 아트·액체 충전면·상시 affordance 계약은 버튼과 함께
은퇴한다. 손패(`dreamcatcher-awakening-hand`)의 게이지 경제·`Toggled` 계약은 유지.

## 파이프라인 커버리지

N/A — ECS 플레이 오브젝트 생성→렌더 경로 변경 없음. 피규어·흡수 비행은 모두 UGUI/HUD
프레젠테이션 계층이며, ECS 쪽은 기존 `EnemyKilledAwakening`/`DefenderDied` bridge 이벤트를
구독만 한다.

## 후속 후보

- 스와이프 업/다운 여닫기 제스처 (C안 계승).
- 만땅 오버플로우 낭비 경고 연출 (손실 회피 유도).
- 피규어 스틸 베이크 폴백 (실기기 드로우콜 문제 확인 시).
- 피규어 식별용 실루엣/색 단순화 아트.
- NextWaveDock 캐주얼 리스킨 (기존 spec 후속 후보 승계).
