# 6. 고정 웨이브 시드 — 테스트 버전용 매판 동일 공격 패턴

> rev 2026-07-20. wave-pattern 1차 구현(0~5) 이후 추가 작업 단위.

## 목적

테스트 배포 기간 동안 매판 같은 공격 패턴이 나오도록 웨이브 시드를 고정한다.
현재 라이브 경로는 `MatchSeed.DeriveWaveSeed(matchSeed)` 로 매판 다른 시드를 쓴다
(`BattleBridge.TryInitializeGeneratedWaves`). 덱 SO 의 기존 `waveSeed` 필드
(match-seed-unification 때 "레거시, 테스트 전용"으로 강등됨)를 **라이브 고정
오버라이드**로 재활성한다.

**대안 비교** (기각 사유 포함):

- `GameManager.debugFixedMatchSeed`: 코드 0줄이지만 맵·기믹·픽업·비주얼 지터까지
  전부 고정된다. 요구는 "공격 패턴만".
- `BattleBridge` 에 `fixedWaveSeed` 필드 신설: `fixedMapSeed` 미러지만, 비0 코드
  기본값이 씬 저장 시 베이크되는 함정이 이미 백로그에 지적돼 있고(spec/README.md
  "시드 권한 일원화"), 아웃게임 브리핑(`WavePatternStripView`)이 못 본다.
- **채택 — `AttackDeck.waveSeed`**: SO 값이라 씬 베이크 함정 없음(제약 6 부합).
  브리핑 스트립은 이미 `Generate(deck)`→`ResolveWaveSeed()`→`waveSeed` 를 쓰므로
  **프리뷰=런타임 동일 플랜** 계약이 자동 복원된다.

## 변경 대상

- `Assets/_Project/Scripts/Data/AttackDeck.cs` — `waveSeed` 주석 갱신 (deprecated → 비0 = 라이브 고정 오버라이드, 0 = matchSeed 파생)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `TryInitializeGeneratedWaves` 시드 resolve 분기 + provenance 로그
- `Assets/_Project/Scripts/Data/Decks/WaveA.asset` — `waveSeed` 를 비0 값(예: `20260720`)으로 설정

## 구현

`TryInitializeGeneratedWaves` 의 시드 결정을 다음으로 교체:

```csharp
int waveSeed = deck.waveSeed != 0
    ? deck.waveSeed                                            // 덱 고정(테스트 버전)
    : Wassup.Core.MatchSeed.DeriveWaveSeed(_matchSeed != 0 ? _matchSeed : 1); // 매판 파생
```

기존 시작 로그에 시드 출처 1단어 추가: `seed={N} (source=deck-fixed|derived)`.
`ResolveWaveSeed()` 는 0→1 폴백이 있어 분기 조건으로 직접 쓰지 않는다(0 판별 불가).
작성 플랜(`_authoredPlan`) 우선순위·legacy fallback 등 기존 흐름은 무변경.

## 완료 기준

- 에디터 Play 2회 재진입: 시작 로그의 `seed=` 값과 웨이브 요약(`FormatSummary`)이 두 판 동일.
- 아웃게임 웨이브 브리핑 스트립의 웨이브 목록이 인게임 실제 웨이브와 일치.
- `waveSeed` 를 0으로 되돌리면 매판 랜덤(derived) 복귀 — 로그 source 표기로 확인.
- 기존 EditMode 테스트(WavePatternGeneratorTests·BossTests) 그린 유지.
