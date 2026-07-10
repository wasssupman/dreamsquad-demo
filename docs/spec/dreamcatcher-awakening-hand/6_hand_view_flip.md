# 6 — 손패 뷰 + 플립 전환 + 슬로모

## 목적

각성 버튼 토글로 하단 유닛 선택 스트립이 플립 연출과 함께 사라지고, 하단 중앙에 StS(슬레이 더 스파이어)풍 카드 손패가 나타난다. 손패가 열려 있는 동안 전투는 슬로모. 카드는 아직 표시 전용 — 드래그 사용은 unit 7~8.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` (신규)
- `Assets/_Project/Scripts/UI/DefenderSelector.cs` (패널 show/hide 훅 노출 — 최소 수정)
- 씬 배선 (unity-feature-wiring)

## 구현

1. **전환 컨트롤**: `AwakeningGaugeView.Toggled` 구독. 상태 2개(UnitStrip / Hand). 플립 연출 = 하단 패널 X축 회전(90° 접힘) 또는 스케일Y 플립 — 코드 트윈(코루틴). 전환 중 입력 잠금(연타 방어). **전환 상태의 단일 소유자는 이 뷰**(gauge 버튼은 신호만).
2. **슬로모 lease**: Hand 상태 진입 시 `TimeManager.Instance.Request(TimeDomain.Battle, config.slomoTimeScale, priority)` 보유, UnitStrip 복귀 시 `Dispose`(멱등 — 구 3중1 pause lease 패턴, 단 0f 가 아니라 감속 배율). OnDisable 에서도 해제. **게임은 멈추지 않는다.**
3. **손패 렌더**: `DreamcatcherHandController.HandChanged` + `Hand(handSize=5)` 조회로 카드 아이템 갱신. 카드 아이템 = `DreamcatcherCard.art` 이미지(dreamcatcher-card-art 관례) + 비용 뱃지(타입별 15/30/20, config 값). **Active 카드는 art 없으면 `skill.uiTint` 색 + 스킬명 폴백.** 하단 중앙 가로 배열 + 살짝 부채꼴(StS 참조 — `DraftCardFanView` 선례 참고, 재사용 강제 아님).
4. **빈 슬롯**: 큐 < handSize 면 빈 프레임 표시(전량 유출 시 5칸 모두 빈 손패).
5. **사용 불가 dim**: `CanUse(entryId)` false(게이지 부족 등) 카드는 dim + 드래그 차단 플래그(unit 7 이 소비).
6. **자동 복귀**: `HandChanged(Used)` 수신 시 UnitStrip 으로 자동 플립 백(+ 슬로모 해제). `Recovered` 는 복귀 없이 갱신만.
7. **배치 조작과 상호배타**: Hand 상태에서 유닛 배치 스트립은 숨김 — 의도된 동작.
8. **pending 중 토글 (critic H1)**: pending 활성 중 게이지 버튼 재클릭 = **pending 취소(무차감) 후 닫기** — 취소 규칙과 일관. unit 7 §4 와 상호 참조.
9. **phase 강제 클로즈 (critic H2)**: `GameManager.PhaseChanged` 구독 — Battle/Placement 이탈(Result 등) 시 강제 클로즈: UnitStrip 복귀(연출 생략 가능) + pending 드롭·무차감 + 슬로모 lease Dispose + 드래그/2탭 상태 해제. OnDisable 에서도 동일 정리(멱등).

## 완료 기준

- [ ] Play: 각성 버튼 클릭 → 유닛 스트립 플립 아웃 + 손패 플립 인(하단 중앙, 카드 아트/uiTint + 비용 뱃지 5장).
- [ ] 손패 열림 동안 전투가 `slomoTimeScale` 배속으로 느려지고, 복귀 시 정상 속도(연타/재진입에도 lease 누수 없음).
- [ ] 재클릭 → 유닛 스트립 복귀. 연타해도 상태 꼬임 없음.
- [ ] 게이지 부족 카드 dim 표시. 세로 모바일 비율에서 손패가 화면 밖으로 나가지 않음.

> 확인 2026-07-10 — 커밋 d4922d6c (플립/슬로모 사용자 Play 확인)
