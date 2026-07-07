# 4 · Handoff Summary — lobby-keyring-drag

## Commit

- `f731b36b` docs(spec): lobby-keyring-drag 스펙
- `f076a76b` 0 — LobbyKeyringSettings SO + 에셋
- `2643383b` 1 — 드래그 세션 + 스프링 스윙 리그 + suspend/resume 접점
- `a3366f50` 2 — 중력 낙하+바운스, 재잡기, 클릭/스와이프 구분(스와이프 중 idle)
- `249ae848` 3 — OutgameScene 와이어링 (Hello/World 부착 + SO 할당)

## Implemented

- 로비 캐릭터(hello/world) 스와이프 → 키링 모드: 고리(손가락)+줄+캐릭터 매달림,
  스프링+감쇠+속도상한 지연 추종, 머리 중심 기울임(maxAngle 클램프).
- 놓기 → 스윙 y 속도 승계 중력 낙하 → bounceMinSpeed/bounceDamping 반동 →
  바닥(초기 anchoredPosition.y) 정착 → 행동 재개(hello 새 위치 로밍, world idle).
- 낙하 중 재잡기(BeginDrag 재진입, 낙하 속도 승계).
- 단발 클릭 = 리액션(기존), 스와이프 = 키링. 픽업 시 리액션/걷기 강제 종료 +
  idle 즉시 Play, suspended 동안 TriggerReaction 차단.
- 리그 = 런타임 UI Image(줄=회전 사각, 고리=절차적 annulus 1회 생성 공유).
- FallStep 순수 함수 + EditMode 테스트 3건 통과.

## Key Files

- `Assets/_Project/Scripts/UI/Outgame/LobbyKeyringDrag.cs` — 상태머신/스윙/낙하/리그 전부
- `Assets/_Project/Scripts/UI/Outgame/ILobbyKeyringTarget.cs` — 캐릭터 접점 (hello/world 구현)
- `Assets/_Project/Scripts/Data/LobbyKeyringSettings.cs` + `Assets/_Project/Data/Config/LobbyKeyringSettings.asset`
- `Assets/_Project/Tests/EditMode/LobbyKeyringFallStepTests.cs`

## Verified

- compile 클린, 콘솔 에러/워닝 0. EditMode 3/3 통과.
- 사용자 Play 통과 확인 (2026-07-07): 스와이프 스윙·낙하 바운스·재개·클릭 리액션 공존.

## Notes (되돌리지 말 것)

- 스프링 워밍업(가속 램프) 금지 — 인게임 keyring-cord-preview 계약 승계.
- 기울임 회전 중심은 머리(피벗 위치 역산). 중심/발 피벗이면 반대로 흔들림.
- 클릭 가드는 `_keyringSuspended` 플래그(TriggerReaction 진입부) — 픽업~착지와
  정확히 일치해 `IsBusy` 참조 불필요. 드래그 직후 발화하는 클릭도 차단됨.
- 인게임 배치 코드(`DefenderDragPlacementController`/`DragSwaySettings`)는 무변경.
- OutgameScene 워크트리에 이 spec 과 무관한 미커밋 변경(배경 알파/비활성 오브젝트)
  존재 — 249ae848 에서 헝크 선별로 제외했고 정리하지 않았다.

## Follow-up

- feel 튜닝은 SO 라이브 편집으로 계속 가능(기본값으로 1차 통과).
- 후속 후보는 README 참조: 고리/줄 아트 스왑, 매달림 전용 애니, 착지 VFX,
  스쿼시&스트레치, 줄 sag 곡선.
