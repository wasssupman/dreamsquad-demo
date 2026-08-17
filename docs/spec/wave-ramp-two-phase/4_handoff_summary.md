# 4. Handoff — wave-ramp-two-phase

## Commit

- `4533cf47` feat: unit 0 — 두 단계 수량 곡선 (옵트인, rng 중립)
- `16270c20` feat: unit 1 — 클라이맥스 변주 격상 (상시 변주 + 신규 레인 개방)
- `1b1db9ba` balance: unit 2 — 공습 상한 상향 (Dragon 1→2 · Skimmer 2→4)
- `59af1a41` balance: unit 3 — 공성 3덱 시드 재선정 + 본편 다양성 술어

## Implemented

- `waveRampBreakWave`/`waveRampBreakUnits` (덱, 맨 뒤 append) — 0 = 기존 지수(라이브 무회귀).
  공성 3덱 (15, 12). knob 하나가 곡선 전환·변주 상시·변주 신규 레인 전부의 게이트.
- 본편(w1~15) 평탄 5→12, break 부터 breakUnits 기점 지수. 곡선은 rng 무소비
  (`RampCurve_DoesNotDisturbConceptSequenceOrPicks` pin).
- 클라이맥스 변주 상시(협공 빈도 1/3→3/3) + 미지 laneGroup 변주의 미사용 레인 개방
  (swarm/heavy variantSlots laneGroup 1 저작 — off 덱은 접힘이라 무변경, 테스트 pin).
- Air cap 합 3→6 — 클라이맥스 공습 3기 고정 해소. 컨셉 덱 11개 generator v7, 시드 유지.
- 공성 시드: Duel 20260868 · Ford 20260880 · Isle 20260890 — w1~15 에 5종 전부, 시퀀스 상이.

## Key Files

- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — 곡선·변주 게이트·InheritLanes
- `Assets/_Project/Scripts/Data/AttackDeck.cs` — break 필드
- `Assets/_Project/Tests/EditModeAssets/WaveConceptAuthoringTests.cs` — 다양성 술어 + 시드 스캐너

## Verified

- EditMode 전량 2471 / 실패 0 (skip 3 = 기존 Ignored). 라이브 6덱 변주 pin ①~③ 무회귀.
- **PlayMode·사용자 Play 체감 미확인** — 공성 판이 «다양한 본편 → 클라이맥스»로 읽히는지,
  평탄화 후 무당기기 도달 웨이브 실측(README 후속 후보)이 남아 있다.

## Notes

- **오프라인 시뮬은 시드 선정에 쓰지 마라**: PickConcept float32 누적 경계에서 실 생성기와
  시드별로 갈린다(실측 — 포트 후보 2/3 이 빨강). 재선정은 `Scan_SiegeSeedCandidates`
  (Explicit)로. MCP 러너는 Explicit 를 이름으로도 못 고르니 잠시 속성을 떼고 돌려라.
- 변주 계약 ①~③(wave-pull-revival)은 **게이트 off 덱의 계약**으로 스코프 명시됨.
- 버전 7 bump 는 컨셉 덱 11개만 — SiegeTest/WaypointLab(v2)은 Dragon/Skimmer 가 없어 무영향.

## Follow-up

- 라이브 6덱 확대(break 저작 + 시드 재선정) — 공성 검증 후 사용자 결정
- 무당기기 도달 웨이브 실측 → break 경계 재조정 여부
- 변주 격상 3단계 세분화 / Air 적 신규 — README 후속 후보 참조
