# 보스 웨이브 케이던스 + 꿈결 위기 워닝 — 설계

> 얇은 브레인스토밍 결과물. 구현 상세는 `docs/spec/boss-wave-cadence/` 참조.

## 목표

라이브 생성 웨이브에서 **매 5번째 웨이브 = 보스 1기 + 잡몹 3~4마리**로 편성하고,
보스가 스폰되는 순간 **"꿈결 위기!!"** 크림슨 워닝 배너로 등장을 알린다.

명시적 비목표: 파워/예산 밸런싱 시스템(사용자 보류). authored 테스트 플랜 변경. 새 보스 콘텐츠.

## 아키텍처 요약

두 관심사를 **완전히 분리**한다:

1. **편성(생성기)** — `WavePatternGenerator.Generate`(seed 경로)가 기존 랜덤 웨이브를 만든 뒤,
   매 N번째 웨이브를 보스+잡몹으로 **치환**한다. 보스 출처는 `AttackDeck.bossUnit`(풀과 분리).
2. **워닝(스폰 구동)** — `BattleBridge.SpawnUnit`이 스폰 시 이미 `nightmareMechanics`로 보스를 판별한다.
   바로 그 지점에서 `BossWarningView.Show()`를 호출한다. 워닝은 웨이브/생성기를 전혀 모른다 →
   seed·authored 어느 경로로 보스가 나와도 자동으로 뜬다. 단일 진실 = `nightmareMechanics`.

핵심 결정: 워닝을 **웨이브 시간 lookahead 가 아니라 보스 스폰 이벤트**에 연결(사용자 지시).
트레이드오프 = 배너가 보스 스폰 "직전"이 아닌 "순간"에 뜬다(맵 가장자리 워크인이 자연 리드).

UI 스타일 = 기존 `ScoreHudView` 언어 차용(런타임 절차 UI, Kanit Bold Italic SDF), 팔레트만 크림슨 위기색.

## spec 포인터

- 계약·작업 단위: `docs/spec/boss-wave-cadence/README.md`
- 0 생성기 주입 / 1 워닝 뷰 / 2 스폰 훅 / 3 씬 배선·검증
