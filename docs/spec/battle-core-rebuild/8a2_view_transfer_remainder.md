# 8a2 — 뷰 이전 잔여: 장부가 「새 주인」이라 적었지만 실체가 없던 7행 (조각 E · 8c 뒤 · 9 앞)

> 8c 의 `--owners` 대조(2026-09-25)가 찾아낸 것이다. `rule-holders`·`bridge-methods` 가 새 주인을 배정했는데 새 씬에는 호출처가 0 인 **옛 플레이어 가시 기능**. 8a 의 「미정 0 은 배정의 끝이지 실현의 끝이 아니다」 가 다시 확인된 사례라 8c 안에서 이식하지 않고 이 unit 으로 뗀다. **unit 9 가 옛 코드를 지우기 전에 끝나야 한다.** 리드 판단(2026-09-25): 1~6 은 「기획 그대로」 이식, 7 은 새 트레이스에 같은 사건이 있으면 흡수.

## 목적

옛 판에서 플레이어가 보던 것 7 + 진단 채널 1 을 새 씬에 실체로 만든다. 규칙은 이미 코어에서 돈다 — 옮기는 것은 **그리기·흐리기·느리게 하기·트레이스**뿐이다. 끝나면 `check_ledgers.py --owners` exit 0.

## 변경 대상 (정본 표는 `8c_…md` 「미실현 뷰 7건」 — 옛 `파일:줄` 과 새 자리는 거기서 읽는다)

| # | 옛 기능 | 옛 근거 | 새 층의 닿을 자리 |
|---|---|---|---|
| 1 | 효과 타일 칸 표시(어느 칸이 효과 타일인지 판 위에) | rule-holders T15 · `BattleBridge.cs`(효과 타일 그리기) | 정의표의 효과 타일 칸 → `CoreMapOverlay`(하이라이트와 같은 층) — 소비·회복 사건이 있으면 구독 |
| 2 | 궁극기 착지 예고 칸 | T16·T17 · `BattleBridge.UltimateLeap.cs:87·110 ShowLandingTelegraph` | 이탈 사건(`CoreEvent` 의 `areaTiles: TelegraphTileRange`)을 `CoreMapOverlay` 착탄 예고 표식(6c)으로 — 아치 비행(8a)은 그대로 |
| 3 | 마음 붕괴 연출 + 슬로모 | `BattleBridge.cs PlayCoreBurst`·`DrainGoalCollapsedEvents`·`coreBurstTimeScale` | `HeartCollapsed`(30) 구독 뷰 신설 + 슬로모 = **틱 발행률**(`BattleDriver` 의 정지 리스와 같은 축 — `Time.timeScale` 금지). 슬로모 배율·길이는 옛 저작 값을 SO 로 |
| 4 | 배치 드래그 중 적 흐리게 | `BattleBridge.cs SetEnemiesDimmed` | `DragPlacementInput` 드래그 시작/끝 → `CoreUnitViewPool` 적 전원 `SetDimmed`(추상 메서드 이미 있음 `CoreUnitView.cs:49`) |
| 5 | 적 체력 틴트 | `BattleBridge.cs EvaluateEnemyHealthTint` | 피해 사건 뒤 `SetHealthTint`(`CoreUnitView.cs:47`) — 산식은 옛 것 그대로(순수 함수로, 값은 SO) |
| 6 | 소환사 유지 애니메이션 | `BattleBridge.cs SyncSummonerAnimationState` | AI 상태 읽기 창 → `SetAiState`(`CoreUnitView.cs:59`) — 지속 루프/상실 원샷 이름은 저작 SO |
| 7 | 방어유닛 AI 전이 트레이스 | `BattleBridge.cs TraceDefenderAiTransition` | 새 트레이스(60종)에 AI 전이 사건이 **있으면** 장부만 정정(흡수). 없으면 코어 사건 하나(값 스냅샷: id·이전·이후·틱) + 트레이스 정거장 |
| 8 | 배치 사거리 **칸 채움**(링과 함께 「어느 칸이 사거리 안인가」를 칠한다 · 링 있으면 투명) | rule-holders T3·T13 · `SetPlacementRange`·`RangeFillAlpha`·`IsPlacementRangeCell` | `CoreMapOverlay.PaintRange` 옆에 칸 채움 층 + 자리 고스트가 사거리 칸을 비켜 가는 read seam. ⚠ T13 비고 「되돌리면 채움이 두 겹」 — 링과 채움을 한 곳이 그린다. **리드 기본값 = 이식**(옛 플레이어 가시 · 은퇴 결정 없음) · 사용자가 은퇴를 택하면 이 행만 「삭제」로 닫는다 |

## 구현

1. 행마다 옛 코드를 먼저 읽고 **값**(알파·틴트 커브·슬로모 배율·예고 길이)을 SO 로 옮긴다 — 코어·뷰에 리터럴 금지(제약 6). 옛 값 = 옛 씬 브리지 블록(bridge-fields 머리말과 같은 방식).
2. 뷰는 사건 값 스냅샷과 읽기 창만 쓴다. 이벤트로 상태를 되묻지 않는다(계약 4). 드라이버·풀에 규칙을 넣지 않는다.
3. 슬로모(3)는 8a 의 정지 리스와 같은 진입점을 쓴다 — 리스 둘이 겹치면 더 느린 쪽이 이긴다(옛 `TimeManager.Request` 결합 규칙 인용).
4. 행마다 PlayMode 코어 테스트 1(사건 → 뷰 호출 증언) · 순수 함수는 EditMode. 골든 무변(뷰만).
5. 끝나면 `rule-holders`·`bridge-methods` 의 해당 행 「실현 위치」를 심볼로 채우고 `--owners` exit 0.

## 이식 제외

없음 — 7행 전부 옛 기능이고 은퇴 결정이 없다(있으면 옛 spec 인용 필수, 함정 5).

## 완료 기준

- [ ] `check_ledgers.py --owners` exit 0(8c 가 보류한 줄이 닫힌다).
- [ ] 새 씬 Play: 효과 타일 칸이 보인다 · 궁극기 이탈 뒤 착지 칸 예고가 보인다 · 마음 붕괴 시 연출 + 슬로모 · 드래그 중 적이 흐려진다 · 적 체력에 따라 틴트 · 소환사 유지 루프.
- [ ] PlayMode 코어 +7(또는 +8) 초록 · EditMode 선행 2 외 빨강 0 · 골든 무변 · 헤드리스 3종 · Retire.Check 0.
- [ ] `core-reviewer` APPROVE → 8c 의 머지 게이트(플레이 4차)에 합류.
