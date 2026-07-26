# 맵 · 웨이브 밸런싱 레퍼런스

> 맵 로테이션 / 웨이브 난이도 / 몬스터 스탯을 조정하는 실무 가이드. **자주 바꾸는 값들**이라 여기 모아둔다.
> 점수 산식 상세는 `docs/reference/score-formula.md`, 맵 파이프라인은 `object-pipeline-map.md` 참조.

---

## 조정하고 싶은 것 → 어디로 가나

| 바꾸고 싶은 것 | 파일 / 도구 | 핵심 필드 |
|---|---|---|
| **어떤 맵이 등장하나** (맵 추가/제거) | `Assets/_Project/Data/Maps/MapDocumentPool.asset` | `entries` (맵+덱 쌍) |
| **맵 지형** (경로·스폰·골·배치칸) | `Window/Wassup/Map Painter` 또는 execute_code | MapDocument (tiles/spawns/goals) |
| **웨이브 난이도** (몬스터 수·종류·보스) | 맵별 `Deck_{맵}.asset` (AttackDeck) | 아래 §웨이브 knob |
| **개별 몬스터 강함** (HP·속도·공격) | `Enemy_*.asset` (AttackUnitData) | health/moveSpeed/attackRange/attackCooldown… |
| **맵 랜덤 on/off** | `BattleBridge.fixedMapSeed` (BattleScene) | `0`=매판 랜덤, 비0=한 맵 고정 |

---

## 맵 ↔ 덱 페어링

풀의 각 엔트리 = `(MapDocument, AttackDeck)`. **맵마다 자기 전용 덱**을 가진다(2026-07-23~):

| 맵 asset | 덱 | waveSeed |
|---|---|---|
| MapDocument_Serpent | Deck_Serpent | 20260801 |
| MapDocument_Coil | Deck_Coil | 20260802 |
| MapDocument_Twin | Deck_Twin | 20260803 |
| MapDocument_Spiral | Deck_Spiral | 20260804 |
| MapDocument_Zig | Deck_Zig | 20260805 |

- 맵과 덱은 **같은 인덱스로 함께 선택**된다(`MapPoolSelect.SelectIndex(seed, count)`), 그래서 "맵마다 고정된 적 패턴".
- 맵 추가 = 풀 `entries` 에 (새 MapDocument, 새 Deck) 한 쌍 추가. **코드 변경 불필요**(GUID 참조).
- `WaveA.asset`/`WaveB.asset` 은 레거시 원본(테스트 참조) — 풀은 안 씀, 삭제 금지.

---

## 웨이브 난이도 knob (AttackDeck)

`WavePatternGenerator.Generate(deck, seed)` 가 이 값들로 웨이브를 짠다:

| 원하는 것 | 필드 | 현재 기본 |
|---|---|---|
| 웨이브당 몬스터 ↑↓ | `minUnitsPerWave` / `maxUnitsPerWave` | 6 / 10 |
| **웨이브마다 수량 평탄** | `minUnitsPerWave == maxUnitsPerWave` | (ramp 폭 0) |
| 웨이브 개수 | `minWaveCount` / `maxWaveCount` (+`waveCountJitter`) | 10 / 15 |
| 등장 몬스터 종류 | `attackUnitPool` (AttackUnitData[]) | 9종 |
| 보스 | `bossUnit` · `bossWaveInterval` · `bossEscortMin`/`Max` | Nightmare · 5마다 · 3~4 |
| 스폰 템포 | `intraWaveSpacingSec` | 1s |
| **웨이브 시작 → 첫 적 유예** | `waveSpawnLeadInSec` | 2s |
| 누수 한계(스트레스 축) | `defeatGoalReachedCount` | 10 |
| 제한 시간 | `timerDurationSec` | 180 |

**수량 결정 방식**: `minUnitsPerWave~maxUnitsPerWave` 를 **초반→후반 ramp**(초반 웨이브 ≈ min, 후반 ≈ max, jitter). 일반 웨이브 = **2종류**(countA+countB 분할). 보스 웨이브 = 보스1 + 호위(escortMin~Max) 치환.

**리드인**(`waveSpawnLeadInSec`, wave-pattern unit 11): 웨이브 트리거와 첫 적 등장 사이의 유예.
트리거 그리드(`i × interval`)·강제 호출 리스케줄·플랜 시각·브리핑 표기는 **불변**이고 스폰만 밀린다.
올릴 때는 **마지막 스폰이 `timerDurationSec` 안에 남는지** 확인할 것(`WaveKillBudgetPinTests` 가 가드).
작성 플랜(`WavePlanAsset`)에는 적용되지 않는다 — 그룹 상대 시각으로 직접 표현한다.

**완전 수제 웨이브**: `useGeneratedWaves=false` + `spawns` 리스트에 (시각, 유닛, 수) 직접 authoring → 생성기 안 씀.

---

## ⚠️ 결정론 규칙 (절대 지킬 것)

**같은 맵 = 매번 같은 웨이브** 는 `waveSeed` 로 보장된다:

- `BattleBridge`: `waveSeed = deck.waveSeed != 0 ? deck.waveSeed : DeriveWaveSeed(matchSeed)`.
- 덱 `waveSeed` **비0 고정** → `matchSeed`(매판 랜덤) **무시** → 시드 고정 → 웨이브(수·종류·순서·수량) **매판 동일**.
- **`waveSeed` 를 0 으로 만들면 매판 달라진다 — 절대 금지.** (실증: 각 덱 3회 생성 시 유닛·수량까지 완전 일치.)
- 매판 랜덤인 건 "**어느 맵이 나오냐**"(`fixedMapSeed=0`)뿐. 특정 맵이 나오면 그 맵 웨이브는 항상 같음.
- 새 맵/덱 추가 시에도 **덱 waveSeed 를 비0 유니크 값**으로.

---

## 점수 예산과의 관계

점수 3원천(`docs/reference/score-formula.md`): **시간**(`timerDurationSec`) · **스트레스**(`defeatGoalReachedCount`) · **킬**(볼륨 ≈ waveCount×unitsPerWave). 그래서:

- **몬스터 종류만 바꾸기 = 예산 불변**(킬값 종류 무관 잡몹 일괄).
- **수·웨이브·시간·누수 바꾸기 = 예산 변동.**
- **맵 간 점수 소폭 차등은 허용**(2026-07-23 사용자 결정) — 예산을 맵마다 똑같이 맞출 필요 없음. **유일 불변식은 "같은 맵=같은 웨이브"**. 맵별 난이도는 그 `Deck_*` 만 자유롭게 조정.

---

## 맵 지형 규칙 (Map Painter / 신규 맵)

- **골 1~2개**(목표지점). 스폰 **2~4개**(1스폰 금지 — 런타임 `MapConnectivity` 가 `<2` 거부).
- **복도는 골 셀에서만 만난다**: 분리 맵=각 스폰 자기 골(완전 분리), 수렴 맵=여러 스폰이 골에서 합류(non-goal 병합 금지).
- 이동로(Walk) 스폰→골 **≥20**, Walk 1링=Place(배치칸), 나머지 Deco. **2×2 walk 블록 금지**. 그리드 **≤20×12**.
- 수동 맵 관례: `authoringSeed=-1`, `generatorVersion=0`. 덮어쓰기는 **GUID 유지**(풀/덱 배선 불변).
- 골 여러 개면 flow field 가 **최근접 골** 라우팅(`FlowFieldBuilder.BuildFromSources`). 복도 분리면 각 스폰이 자기 골로.

---

## 검증

- **회귀 가드**: `Tests/EditMode/MultiGoalPoolSeparationTests` — 풀 맵 골 ≤2·각 스폰 도달·복도 non-goal 병합 금지. `MapConnectivityTests`·`FlowFieldSingletonTests`.
- **런타임 검증**: `MapConnectivity.AllSpawnsReachGoal`(각 스폰 아무 골이든 도달) — adapter/브리지 가드.
- **덱 결정론 확인**: execute_code 로 `WavePatternGenerator.Generate(deck, deck.waveSeed)` 를 N회 생성해 signature(유닛 id+count) 비교.
- **시트 검증**: 값을 curl 로 읽어 SO 대조(읽기 전용). 상세 `docs/reference/lessons/` + 메모리.

---

## 편집 경로 요약

- **인스펙터 직접**: Deck_*.asset / Enemy_*.asset / MapDocumentPool.asset.
- **Map Painter**: `Window/Wassup/Map Painter` (지형 그리기·검증·Bake).
- **execute_code**: 프로그래매틱 대량 편집(맵 bake·덱 생성). CodeDom C#6 — `in` 파라미터는 `ref`, delegate 파라미터명 외부 지역변수와 충돌 금지.
- **Google Sheet 동기화**: 덱/유닛 값 시트 편집→import (프로젝트에 sheet-sync). import 전엔 디스크 SO 가 옛값.
