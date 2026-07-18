# 10 — Handoff Summary (수명 정정 · Guardian 공용 배선)

## Commit

| 해시 | 내용 |
|---|---|
| `e29300eb` | 플립북 최종 프레임 고정 · 0.5초 자동 퇴장 · 배치 성공 강제 초기화 |
| `e3632167` | Guardian 컷씬을 Sniper 49프레임·뎁스·튜닝값으로 배선 |

## Implemented

- 플립북 Phase A 종료 직후 마지막 non-null 컬러 프레임을 명시 적용한다.
- 대응하는 뎁스 프레임도 기존 clamp 규칙으로 lockstep 적용한다.
- 최종 포즈를 `Time.unscaledDeltaTime` 기준 0.5초 유지한다.
- 이후 기존 0.18초 왼쪽 slide-out을 수행하고 루트 Canvas를 숨긴다.
- `TryBeginDefenderDeployment` 성공은 절대 우선하여 어느 Phase에서든 즉시 종료한다.
- 강제 종료는 코루틴·Canvas·틸트 target/current/velocity를 모두 초기화한다.
- 실패·취소는 컷씬을 자르지 않고 일반 자동 종료를 유지한다.
- 새 배치 세션은 직전 컷씬을 먼저 초기화해 프레임 없는 유닛에도 잔상이 넘어가지 않는다.
- Guardian은 전투 데이터는 유지하고 컷씬 4필드만 Sniper와 공유한다.

## Key Files

- `Assets/_Project/Scripts/UI/DeployCutscenePlayer.cs`
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`
- `Assets/_Project/Data/Defenders/Defender_Guardian.asset`
- `docs/spec/defender-deploy-cutscene/8_hold_last_frame.md`
- `docs/spec/defender-deploy-cutscene/9_guardian_uses_sniper_cutscene.md`

## Verified

- 다른 세션의 최근 커밋 `b487ac42`, `5fb92185`와 변경 경로 비중첩 확인.
- Guardian/Sniper 컷씬 프레임 49개·뎁스·scale 2.6·offset (0,0) 동일성 확인.
- 관련 diff `git diff --check` 통과.
- 열린 Unity 6000.4.3f1 Editor 강제 script refresh/recompile 완료.
- Unity Console error 0.

## Notes

- 배치 성공과 컴포넌트 비활성화는 즉시 강제 종료한다.
- 배치 실패·취소 자체는 즉시 종료 조건이 아니다.
- Guardian 자체 컷씬 PNG는 삭제하지 않았고 현재 데이터 참조만 Sniper로 교체했다.
- 컷씬은 순수 MonoBehaviour 프레젠테이션이며 ECS/BattleBridge를 건드리지 않는다.

## Follow-up

- 실제 BattleScene에서 애니 완주→최종 포즈 0.5초→좌측 퇴장 체감 확인.
- 애니 도중 배치 성공 시 같은 프레임에 컷씬이 사라지는지 확인.
- Guardian 드래그 시 Sniper 컷씬의 크기·위치가 의도대로 보이는지 확인.
