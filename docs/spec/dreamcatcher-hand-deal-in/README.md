# dreamcatcher-hand-deal-in — 각성 손패 딜링 등장 연출

**상태: 설계 승인 대기 (2026-07-13)**

## 목표

플레이 중 각성 버튼을 눌러 손패가 열릴 때, 현재 전체-패널 X축 폴드(단일 연출)를
**버튼에서 카드가 딜링되는 등장 연출**로 교체한다. 각성 버튼 ↔ 손패의 공간적
인과("각성 게이지를 소모해 이 손패를 뽑았다")를 시각적으로 만든다.

- **진입**: 카드가 각성 버튼 정확한 좌표에서 시작 → 좌→우 스태거 → 부채꼴 위치로 OutBack 안착.
- **입체감(①)**: 딜 궤적/안착에 원근 틸트.
- **미세 커브(②)**: 안착 순간 카드가 살짝 휘었다 펴지는 flex.
- **퇴장**: 카드가 역스태거로 버튼으로 수렴·축소 후 디펜더 strip 폴드 인 (진입과 대칭).

## 연결 문서

- 형제 spec: `docs/spec/gift-phase/` — PrimeTween 스태거 딜/셔플/fly-out 선례(`GiftPhaseView`). 이 spec 은 그 handoff 가 남긴 "연출 시각 상세조정은 후속 스펙" 항목의 실체.
- 대상 코드: `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs`, `AwakeningGaugeView.cs`.

## 구현 문서 목록

| # | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 딜-인 코어 | `0_deal_in_core.md` | 버튼 좌표 시작 → 스태거 딜 → 부채꼴 OutBack 안착. 트레이 페이드. 튜닝 SerializeField. |
| 1 | 입체감 ① | `1_perspective_tilt.md` | 딜 궤적/안착 원근 틸트. 캔버스 원근 모드 라이브 확인. |
| 2 | 미세 커브 ② | `2_micro_flex.md` | 안착 시 flex(버텍스 서브디바이드 커브 or squash-stretch 폴백). |
| 3 | 퇴장 수렴 | `3_close_converge.md` | 카드 역스태거로 버튼 수렴·축소 → strip 폴드 인. |

(구현 종료 시 `4_handoff_summary.md` 추가.)

## feature-wide 계약

- **딜 소스 = 라이브 버튼 rect**. 손패는 Placement/Battle 중에만 열리고 그때 각성 버튼은
  활성 상태 → `AwakeningGaugeView` 가 `PanelRect` 를 노출하고, HandView 가 스크린 좌표 변환
  (`RectTransformUtility`)으로 버튼 중심을 손패 패널 로컬로 환산한다. GiftPhaseView 의 하드코딩
  `FlyTarget` 근사는 이 spec 에선 쓰지 않는다(버튼이 살아있으므로 정확 좌표 가능).
- **딜 목적지 = 기존 부채꼴 geometry**. `slot.homePos`/`slot.homeRotZ`(EnsureSlots 산출)는 불변.
  딜은 buttonLocal → home 로 이동시킬 뿐 카드 배치식은 바꾸지 않는다.
- **PrimeTween Sequence 는 필드로 보유하고 teardown 에서 stop**. `ForceClose`/phase 이탈/`OnDisable`
  에서 반드시 `Stop()`(leak·late-land 방지). GiftPhaseView `StopSequence()` 미러.
- **딜/수렴 진행 중 드래그 금지**. `Transitioning` 이 딜 시퀀스 진행 상태를 포함하도록 확장.
- **Unity `Time.timeScale` 은 항상 1**(전투 슬로모는 TimeManager 도메인). PrimeTween 기본 타이밍
  (`Time.deltaTime`)이 곧 실시간 → UI 가 슬로모에 안 눌린다. 별도 unscaled 처리 불요(완료 기준에서 확인).
- **슬로모 lease·페이즈 가드·drag 서비스 계약 불변**. `Open/Close/ForceClose` 의 lease·suppress·
  strip 복원 로직은 유지하고, flip 코루틴만 딜 연출로 교체한다.
- **순수 프레젠테이션. ECS 변경 0, 채널 변경 0.** 덱 사이클·게이지 경제·카드 데이터/아트 불변.

## 파이프라인 커버리지

N/A — 플레이 오브젝트(유닛/적/투사체/해저드/VFX)를 신설하거나 생성→렌더 경로를 바꾸지 않는다.
대상은 런타임 빌드 UGUI 카드 위젯(`DreamcatcherHandView._slots`)의 등장/퇴장 트위닝뿐이다.
`docs/reference/object-pipeline-map.md` 의 아키타입 어디에도 해당하지 않는 순수 UI 연출.

## 비목표 / 후속 후보

- **③ 꼬깃꼬깃 → 쫙 펴짐(종이 구김)** [M] · 촘촘한 서브디바이드 메시 + `_Unfold` 노이즈 변위
  버텍스 셰이더 + 모바일 성능 검증이 묶인 독립 작업. 딜링 연출과 한 커밋에 섞으면 스코프가 터진다.
  별도 spec 초안으로 분리. (unit 2 의 진짜 버텍스 커브가 이 작업의 서브디바이드 토대와 겹치므로
  ② 를 squash-stretch 폴백으로 확정하면 커브 전량을 이쪽으로 이관.)
- **카드 딜 SFX** [S] · 딜/안착/수렴에 SoundManager 틱. 연출 확정 후.
- **사용 카드 소비 연출** [S] · 현재 use → Refresh+Close(자동복귀). 소비되는 1장만 별도 강조 후 나머지 수렴.
