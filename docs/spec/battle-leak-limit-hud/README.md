# battle-leak-limit-hud

> 상태: **완료 2026-07-18** — 구현 `f4cc4371`, 용어 개정 `d4cd9f0f`, 사용자 마감 확인 완료.
> 인계: `3_handoff_summary.md`
> 선행: `score-hud`, `subconscious-curse-expansion` 완료

## 목표

적이 목표 지점으로 누수되면 패배한다는 조건을 상시 노출한다. 우상단 점수 배지 바로 아래에 같은
골드/네이비 스타일의 작은 배지를 추가하고 `스트레스 현재값 / 실제 최대 제한`을 표시한다.

검증 질문: *"플레이어가 전투 중 점수와 함께 현재 스트레스와 패배 한계를 즉시 읽고, 다음 적
누수가 얼마나 위험한지 이해할 수 있는가?"*

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_leak_state_bridge.md` | 상태 계약 | BattleBridge의 실제 패배 판정값을 ScoreHudView에 전달 |
| 1 | `1_score_companion_badge.md` | HUD 구현 | 점수 아래 `스트레스 current / limit` 배지와 위험 상태 표현 |
| 2 | `2_play_validation.md` | 통합 검증 | 초기화·누수 증가·한계 변경·패배 직전·페이즈 노출 확인 |
| 3 | `3_handoff_summary.md` | 인계 | 종료 시 구현·검증 결과 기록 |

## Feature-wide 계약

- **표시값은 패배 판정의 source of truth와 동일하다.** 현재값은 `_goalReachedCount`, 최대값은
  `deck.defeatGoalReachedCount - _leakAllowancePenalty`다.
- 기본 `WaveA`에서는 `0 / 10`으로 시작하지만 숫자 10을 UI 코드에 하드코딩하지 않는다.
- `몽마의 계약`으로 허용치를 지불하면 분모가 즉시 감소한다. SO `AttackDeck`은 변경하지 않는다.
- 플레이어 노출 용어는 `스트레스`다. 내부 코드와 패배 규칙의 leak/누수 명칭은 기술 용어로 유지한다.
- 정보 구조는 한 줄 `스트레스 {current} / {limit}`이며, 점수 배지 바로 아래 우측 정렬로 붙인다.
- 점수 HUD의 네이비 플레이트·골드 테두리·Kanit 폰트를 재사용하되 점수보다 작은 보조 위계로 둔다.
- 정상 상태는 골드/밝은 회색, 잔여 허용치가 적을 때는 주황→적색으로 경고한다. 임계값과 팔레트는
  `ScoreHudView` 직렬화 필드로 둔다.
- 누수 증가 시 짧은 punch/color flash만 사용하고 점수 처치 연출은 공유하지 않는다.
- Battle에서만 점수와 함께 표시하고 Placement/Result에서는 기존 ScoreHudView 수명주기를 따라 숨긴다.
- GoalReached 이벤트, 패배 조건, 결과 화면, 점수 계산은 변경하지 않는다.
- 새 씬 참조 없이 기존 `BattleBridge.scoreHud`로 상태를 전달한다.

## 변경 예상 범위

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scripts/UI/ScoreHudView.cs`
- `Assets/_Project/Scenes/BattleScene.unity` — 필요 시 스타일값 hunk만 선별

## 파이프라인 커버리지

N/A — 기존 GoalReached 드레인과 UGUI만 확장한다. 새 ECS 컴포넌트·이벤트 채널·플레이
오브젝트·생성→렌더 정거장이 없어 `docs/reference/object-pipeline-map.md` 갱신 대상이 아니다.

## 비목표 / 후속 후보

- 누수 최대치 및 웨이브 밸런스 변경.
- 누수 발생 위치의 월드 VFX·카메라·SFX.
- 별도 게이지나 하트 아이콘 확장.
- 결과 화면의 누수 통계 변경.
