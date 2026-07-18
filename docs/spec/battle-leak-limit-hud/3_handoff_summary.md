# 3 — Handoff Summary

## Commit

- `f4cc4371` — Add live leak limit HUD below score
- `d4cd9f0f` — Rename leak HUD label to stress
- 사용자 마감 확인: 2026-07-18

## Implemented

- 점수 UI 바로 아래에 같은 네이비 플레이트·골드 테두리의 보조 배지를 추가했다.
- 플레이어 노출 라벨은 `스트레스`, 값은 `{current} / {limit}` 형식이다.
- 현재값은 BattleBridge의 `_goalReachedCount`를 사용한다.
- 최대값은 실제 패배 기준인 `defeatGoalReachedCount - _leakAllowancePenalty`를 사용한다.
- 기본 WaveA에서 `0 / 10`으로 시작하고 숫자 10은 UI에 하드코딩하지 않는다.
- 몽마의 계약으로 허용치를 지불하면 분모가 즉시 감소한다.
- 잔여 3 이하는 주황, 잔여 1 이하는 적색으로 경고한다.
- 현재값 증가 또는 최대값 감소 시 숫자에 짧은 flash/punch를 재생한다.
- Placement/Result에서는 점수 패널과 함께 숨고 Battle에서만 표시한다.

## Key Files

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scripts/UI/ScoreHudView.cs`
- `docs/spec/battle-leak-limit-hud/README.md`
- `docs/spec/battle-leak-limit-hud/0_leak_state_bridge.md`
- `docs/spec/battle-leak-limit-hud/1_score_companion_badge.md`
- `docs/spec/battle-leak-limit-hud/2_play_validation.md`

## Verified

- Unity 6000.4.3f1, 1920×1080 Battle에서 점수 하단 정렬과 safe area를 확인했다.
- `0 / 10`, `7 / 10` 주황, `9 / 10` 적색 상태를 확인했다.
- `TryPayLeakAllowance(1)`에서 remaining `10 → 9`, HUD `0 / 9`를 확인했다.
- Placement hidden, Battle visible 페이즈 계약을 확인했다.
- `스트레스` 4글자 라벨이 gold pill 안에서 잘리지 않음을 확인했다.
- Unity 컴파일 및 Console error 0을 확인했다.
- ECS 리뷰에서 경계·수명주기 지적 0이었다.

## Notes

- `스트레스`는 플레이어 노출 용어다. 코드의 leak/누수 명칭은 기술 용어로 유지한다.
- GoalReached 이벤트 구조와 NativeQueue 수명주기는 변경하지 않았다.
- HUD는 ECS를 직접 읽지 않고 유일한 경계인 BattleBridge에서 스냅샷을 받는다.
- AttackDeck SO와 패배 조건의 의미는 변경하지 않았다.
- BattleScene 저장 없이 코드 기본 직렬화값으로 구현해 다른 씬 WIP를 보존했다.
- 플레이 오브젝트 파이프라인 변경이 없어 object-pipeline-map 갱신 대상이 아니다.

## Follow-up

- 적 누수 위치의 월드 VFX·SFX·카메라 피드백은 별도 spec 후보로 남긴다.
- 상세 후속 후보는 본 spec README의 `비목표 / 후속 후보`를 따른다.
