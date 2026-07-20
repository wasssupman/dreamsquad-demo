# 4 — Handoff Summary

boss-wave-cadence: 라이브 웨이브에 매 5번째 보스 편성 + 보스 스폰 시 "꿈결 위기!!" 워닝. 2026-07-18 완료(사용자 Play 확인).

## Commit
- `eb3e61b4` docs — 스펙
- `8e734cf5` unit 0 — 생성기 보스 편성 주입 + EditMode
- `add0c3ec` unit 1 — BossWarningView
- `8604015d` unit 2 — 스폰 워닝 훅
- `a6e95c67` unit 3(데이터) — WaveA 덱 보스 값
- `1ece58e0` fix — 10웨이브 미노출(스티키 가드 제거)
- `f1e7877d` BattleScene 배선(BossWarningView GO + `_bossWarning`)
- `010acd18` 보스 F2 외형(별도 스코프)

## Implemented
- `WavePatternGenerator.Generate`(seed 경로)가 매 `bossWaveInterval`(=5)번째 웨이브를 **보스×1(선봉) + 잡몹×[3,4]**로 후처리 치환. optional 파라미터라 기존 호출부 무변경.
- 생성 pool 에서 `bossUnit` 방어 제외(있으면 경고) → 비-보스 웨이브/escort 보스 누출 불가.
- 비-보스 웨이브는 같은 seed 로 version 1 과 byte-identical(후처리라 rng 불변).
- `BossWarningView` — 런타임 절차 UI(UiCanvasSetup+UiRoundedSprite+PrimeTween), Kanit SDF, 크림슨 배너 + 붉은 비네트, 슬램인→홀드→페이드.
- 워닝 트리거 = `BakeNightmareMechanics` 의 보스 확정(BossTag 부착) **단일 지점**에서 `_bossWarning?.Show()`. 웨이브/생성기 무관, seed·authored 양경로 자동.
- `Show()` 는 **재시작 방식**(스티키 가드 없음). 페이드 콜백은 패널 비활성만(자기-Stop 금지).

## Key Files
- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs`, `AttackDeck.cs`
- `Assets/_Project/Scripts/UI/BossWarningView.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (`_bossWarning` 필드 + `BakeNightmareMechanics` 훅)
- `Assets/_Project/Scripts/Data/Decks/WaveA.asset` (bossUnit/interval=5/escort 3~4/ver=2)
- `Assets/_Project/Scenes/BattleScene.unity` (BossWarningView GO fileID 322840704 + BattleBridge._bossWarning)
- 테스트: `Assets/_Project/Tests/EditMode/WavePatternGeneratorBossTests.cs`

## Verified
- EditMode **14/14**(보스 5 + 생성기 회귀 9). 컴파일 클린, 콘솔 에러 0.
- 사용자 Play 확인: **5·10웨이브 둘 다** "꿈결 위기!!" 배너 정상(2026-07-18, 스티키 가드 픽스 후).

## Notes (되돌리면 안 됨)
- **BattleScene 배선 커밋됨(f1e7877d)**: BossWarningView GO + `_bossWarning` 만 스테이징(commitPop hunk 역적용으로 제외). 사용자 드래그-프리뷰 WIP(`commitPop*`/`liquid*`)는 여전히 **unstaged 보존**(사용자 소유). BattleScene 저장의 sparkColorBoost 재유입은 이미 해결됨(코드 기본값).
- `Show()` **재시작 > 코얼레스**: 코얼레스+스티키 가드가 10웨이브 미노출의 원인이었다. 되돌리지 말 것.
- 워닝 판별은 `nightmareMechanics` 단일 진실 — SpawnUnit 재판정 금지.
- 보스는 wave 그룹 **선봉(group[0])** — RoundRobin round 0 스폰.
- `waveGeneratorVersion` 2 는 **로그 라벨**(런타임 enforce 없음).

## Follow-up
- **보스 비주얼 F2 완료**(별도 스코프, 010acd18): scale 3.2 + partSkins(해골반다나/보라눈/코트) + 백팩제거. 최종 체감은 사용자 Play.
- 한글 글리프 렌더는 Kanit fallback 의존(스코어 "점수"와 동일). 이상 시 Jua SDF 교체.
- 후속 후보: 보스 로테이션(bossPool), 엄밀한 2초 프리-텔레그래프(스폰 지연).
