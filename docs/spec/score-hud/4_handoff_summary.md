# 4 — Handoff Summary

## Commit

- `1434911` `feat(combat-feedback): 데미지 숫자 팝업 + 라이브 점수 HUD`
  (damage-number-popup spec 과 공유 파일을 함께 건드려 단일 커밋)

## Implemented

- 전투 화면 상단 여백(게임영역 바깥)에 라이브 점수 HUD.
- 적 처치마다 +가점(기본 10), 카운트업 롤 + 펀치 스케일 + 골드 플래시로 강하게 증가.
- 적 사망(`AttackUnitTag`, HP≤0)만 집계. 골 도달·디펜더 사망 제외.
- `Battle` 진입 시 0 리셋 + 표시, 그 외 phase 숨김. 드캐 일시정지(timeScale=0) 중에도 `unscaledDeltaTime` 으로 롤 진행.
- 폰트는 데미지(Bangers)와 구분해 **Anton SDF**(OFL) 사용, 1.3배(값 83/캡션 29).
- 표시 전용 — ResultScreen/리더보드 공식 불변.

## Key Files

- `Assets/_Project/Scripts/Battle/Units/EnemyKilledEvent.cs` / `EnemyKilledEventsSingleton.cs` — 채널 #16.
- `Assets/_Project/Scripts/Battle/Units/DamageApplicationSystem.cs` — 사망 분기에서 적이면 enqueue(`_attackTagLookup` 재사용).
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 채널 생성/해제 + `DrainEnemyKilledEvents` → `scoreHud.OnEnemyKilled()`.
- `Assets/_Project/Scripts/UI/ScoreHudView.cs` — UGUI 빌드 + 롤/펀치/플래시 + phase 표시/리셋.
- `Assets/_Project/Fonts/Anton SDF.asset` + `Score Outline Mat.mat` + `Anton-Regular.ttf`(+OFL).
- BattleScene: `ScoreHud` GameObject + `ScoreHudView`, `BattleBridge.scoreHud` 연결.

## Verified

- compile: CS/Burst 에러 0, 경고 0(`textWrappingMode` 신 API).
- Play(Squad, 사용자 2026-06-05): 점수 표시·증가 연출, Anton 폰트·상단 배치·1.3배 확인.

## Notes

- 점수 로직(누적 + pointsPerKill)은 `ScoreHudView` 소유(표시 전용). BattleBridge 는 킬당 통지만.
- **씬 직렬화값 우선**: 폰트/크기/위치 변경 시 BattleScene 의 `ScoreHudView` 값 + 코드 기본값 둘 다 갱신.
- `GameManager.Instance.PhaseChanged` 지연 구독(Instance 준비 후, Update 폴링) — Awake 순서 의존 제거.
- 무관: OutgameScene 로드 시 `missing script (Unknown)` 2건은 기존 이슈(이번 작업과 무관, 미수정).

## Follow-up

- 적별 점수 차등(적 SO bounty 필드), 라이브 점수를 최종 결과 점수로 승격(점수 모델 변경).
- 콤보/연속 처치 배수, 킬 위치 "+10" 플로팅(EnemyKilledEvent.position 활용).
