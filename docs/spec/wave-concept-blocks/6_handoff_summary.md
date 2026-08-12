# 6. Handoff — wave-concept-blocks

## Commit

| 해시 | 내용 |
|---|---|
| `e337154e` | spec 신설 (rev 1) |
| `97846a29` | spec rev 2 — 동행 기각, 생성 레이어로 범위 축소 |
| `c4319793` | spec unit 4 보정 — 「공습」을 Air 로스터 주도로 |
| `0bfd3163` | unit 0 — 컨셉 데이터 모델 |
| `79dacaa8` | units 1~4 — 순수 함수 · 생성기 통합 · 보스 재케이던스 · 5종 저작 |
| `ab9d283e` | unit 5 — 블록 전환 예고 |
| `67c0c7c6` | refactor — 편성 조립 단일화 + `map-wave-balancing.md` 갱신 |

⚠ **`BattleBridge.cs` 의 이 spec 변경은 다른 세션 커밋 `78e3931f`·`ef78f937`(elite-enemy-tier)에 흡수됐다.** 같은 워크트리를 공유하는 중에 그쪽이 파일 전체를 스테이징했다. 코드는 main 에 있고 동작하지만 계보가 그 커밋 안에 있다 — `git log -S GeneratorLaneCount` 로 찾을 수 있다.

## Implemented

- **컨셉 = 블록(3웨이브)의 속성.** 블록 경계에서 하나 뽑고 컨셉·lane 배정을 `conceptHoldWaves` 웨이브 동안 유지, 수량만 곡선을 따라 오른다.
- **lane 은 `laneGroup` 위상**으로 저작하고 실제 인덱스는 `waveSeed` 가 고른다 → 한 풀이 스폰 2~4개인 6맵에 그대로 쓰인다.
- **컨셉 5종**: 평소(1.0) · 벌떼(1.3/Runner) · 중장(0.4/Tanker 단일) · 원거리(0.7/Shooter 협공/게이트7) · 공습(0.3/Air).
- **`Enemy_Skimmer` 라이브 편입** — 6맵+Endless 풀에 중간 삽입(11→12종), `waveSeed` 20260821~27, `waveGeneratorVersion` 3.
- **보스 간격 5 → 9** (블록 마지막 웨이브 = 학습 2 → 시험 1), **덱별 보스 1종 저작**(6맵÷3종=각 2맵), 호위가 블록 컨셉의 성질·위상을 입는다.
- **블록 전환 예고** — 브리핑 스트립은 블록 첫 카드에만 라벨(안쪽 카드는 액센트 죽임), 도크는 전환 직전에만 `다음 N초 · 컨셉`.
- **레거시 폴백 보존** — 컨셉 풀이 빈 덱은 rng 소비 순서까지 현행과 byte-identical.

## Key Files

- `Assets/_Project/Scripts/Data/WaveConceptData.cs` — SO 3필드 슬롯(`laneGroup`/`classFilter`/`altitude`)
- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — 순수 함수 3개 + `ResolveBlockConcept` + `BuildConceptGroups` + `PickSlotUnitIndex` 완화 ladder
- `Assets/_Project/Data/WaveConcepts/Concept_*.asset` — **밸런스를 만지는 곳은 여기 5개뿐**
- `Assets/_Project/Scripts/Data/Decks/Deck_*.asset` — `waveConceptPool`·`conceptHoldWaves`·`bossPool`
- `docs/reference/map-wave-balancing.md` §웨이브 컨셉 블록 — 운영 가이드

## Verified

- `dotnet build Wassup.Runtime` / `Wassup.Tests.EditMode` 0 에러
- **EditMode 2307개 / 실패 0** (스킵 3은 기존 Ignore). 신규 5파일이 블록 유지 · lane 불변식 · 폴백 동일성 · RoundRobin 케이던스 · 고도/성질/게이트/상한 필터 · 보스 간격 9 · 라이브 덱 배선 · 6맵 결정론을 고정
- **PlayMode 12개 / 실패 0** — `TallyFlowTest`(판 시작→결과) · `MovementIntegritySmokeTest` · `GoalStabilityTest` · `BossShieldTest`(보스 호위) · `EndlessModeSmokeTest` · `StructureLivePlayTest` · `BossLullabyTest` · `DraftFlowSmokeTest`
- **리팩토링 동작 불변 실측** — 7덱 × laneCount{1,2,3,4} × 100웨이브 시그니처 덤프 2800줄이 리팩토링 전/후 **완전 동일**
- PlayMode 전체(107개) 1차 시도는 23개에서 정지했다 — 다른 세션과 에디터를 공유해 생긴 정지이며 그때 유일한 실패는 `AuthE2ETest`(Newtonsoft JObject 캐스트, 웨이브와 무관)였다. 좁힌 12개로 재확인했다.

## Notes (되돌리면 안 되는 의도)

1. **레거시 2종 분기를 지우지 말 것.** 컨셉 풀이 빈 덱의 무회귀 경로이고 rng 소비 순서까지 같아야 성립한다. 보스 후처리를 웨이브 루프 안으로 옮기는 것도 같은 이유로 금지.
2. **`SlotAltitude` 에 `Any` 를 추가하지 말 것.** 기본이 Ground 라서 「평소」가 웨이브 1~3 에 비행을 못 낸다. `Any` 가 생기면 대공 없는 첫 블록에 막을 수 없는 적이 나온다.
3. **`PickSlotUnitIndex` 의 완화 순서에서 altitude 는 마지막**이다. class 보다 먼저 버리면 지상 컨셉에 비행이 섞인다.
4. **`laneCount` 는 결정론 키다.** 브리핑(`BuildBriefingWavePlan`)과 런타임이 같은 `GeneratorLaneCount` 창구를 쓴다 — 갈리면 예고와 실스폰이 다른 편성을 보여준다.
5. **호위 예산에 `countMul` 을 곱하지 말 것**(이중 스케일).
6. **`bossUnit` 필드를 제거하지 말 것.** 키를 잃으면 생성기가 «보스 없음»을 no-op 처리해 에러도 경고도 없이 전 맵에서 보스가 사라진다.
7. **뭉침은 저작으로 만든다.** 스폰 시 `moveSpeed` 를 덮는 「동행」은 rev 2 에서 기각됐다(코어 로직 침범 + 제약 6 마찰). 재검토하려면 «저작으로 안 되는 컨셉»의 실제 사례를 먼저 제시할 것.

## Follow-up

- **Play 육안 확인 미완** — 5개 컨셉이 화면에서 서로 구분되는가(특히 「벌떼」 vs 「평소」), 「공습」이 무력감이 아니라 스킬 지불로 느껴지는가, 라벨을 보고 대응할 시간이 있는가. 자동 테스트가 답할 수 없는 항목이고 이 spec 의 검증 질문이다.
- **`countMul`·`weight` 실측 튜닝** — 1.0/1.3/0.4/0.7/0.3, weight 0.6~1.0 은 어림값이다.
- **「원거리」 게이트(웨이브 7) 판정** — 방어유닛이 깎이는 압력이 재미인지 짜증인지 실측 필요.
- **보스와 호위의 동행 불성립** — 조사 완료(sim 재현), **수정 미착수**. 호위가 1~4초 안에 보스 오라 반경을 벗어나 `Nightmare 채찍질`·`Mamemo 악몽의 가호`가 사실상 죽는다. 원인 3종(속도·**경로**·고도)이고 «보스 속도만 올리기»는 **기각**됐다 — 측정·기각 근거·수정 2안은 [README](README.md) 후속 후보 참조. 착수 전 그 항목을 먼저 읽을 것.
- 나머지 후속 후보는 [README](README.md) 하단 참조.
