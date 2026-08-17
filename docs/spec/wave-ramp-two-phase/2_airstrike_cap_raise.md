# 2. 공습 상한 상향 — Dragon 1→2 · Skimmer 2→4

## 목적

Air `maxPerWave` 합 3 → 6. 클라이맥스(지수 구간)에서 공습 블록이 24→3 으로 추락하는
구멍(wave-concept-blocks unit 8 «알려진 구멍»)을 로스터 추가 없이 해소한다. 사용자 결정
(2026-08-17, «maxPerWave 상향»).

## 변경 대상

- `Assets/_Project/Data/Enemies/Enemy_Dragon.asset` — maxPerWave 1→2
- `Assets/_Project/Data/Enemies/Enemy_Skimmer.asset` — maxPerWave 2→4
- 컨셉 덱 11개 — `waveGeneratorVersion` 6→7 (라이브 출력이 실제로 바뀌는 커밋에서 일괄 bump,
  README 계약 7. 시드는 유지 — rng 불변, 수량만 변한다. spec 8 의 v6 bump 와 같은 판단)
- `WaveConceptAuthoringTests.GeneratorVersion_IsBumped` — 7 + 이력 주석
- `docs/spec/wave-concept-blocks/8_airstrike_gate.md` — «알려진 구멍» 해소 주석

## 구현 (enemy-wave-integration 스킬 case (a) 절차)

- 파급 전수: Dragon/Skimmer 는 컨셉 덱 11개에만 존재(레거시 dev 덱 무관). `ClampGroupCounts`
  는 rng 무소비라 **재추첨 없음** — 공습 웨이브(+공습 블록 보스 호위)의 수량만 는다.
- 컨셉 귀속·통행층·속도 폭·튜토리얼 로스터 불변. 킬 예산 pin 은 구조적(리터럴 아님)이라 유지.

## 완료 기준

- EditMode 전량 초록 (엘리트 붕괴 가드·종류합 단언·결정론 3회 포함)
- w4~7 공습 = Dragon 1~2 + Skimmer 1~2, 클라이맥스 공습 총량이 3 에 고정되지 않음
