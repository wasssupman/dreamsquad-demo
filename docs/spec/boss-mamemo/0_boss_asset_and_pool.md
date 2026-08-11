# 0 — 보스 에셋 + 로테이션 투입

## 목적

마메모를 **능력 없는 껍데기 상태로** 판에 올린다. 이 단위의 유일한 검증 대상은
**기존 8개 덱의 잡몹 편성이 무회귀인가**이고, 덤으로 스폰·이동·외형을 눈으로 확인한다.

능력(자장가·실드)은 unit 1·3 에서 붙인다. 여기서 `nightmareMechanics` 를 비워두는 이유는
짱쎈놈 선례와 같다 — **위험한 것(라이브 덱 8개의 rng 스트림)을 먼저 증명**하고, 그게 통과한
뒤에 능력을 얹는다.

> **이 단위가 증명하지 못하는 것**: `BakeNightmareMechanics` 는 mechanics 가 비면 early
> return 이라(`BattleBridge.cs:7519-7521`) `BossTag`·`ThreatEntry`·꿈결 위기 배너·방어유닛
> 사냥 이동이 **하나도 안 붙는다.** unit 0 의 마메모는 "덩치 큰 잡몹"처럼 골로 걸어간다.
> 그게 정상이며, 보스로 굴러가는 것은 첫 mechanic 이 들어오는 unit 1 부터다.

## 변경 대상

| 파일 | 내용 |
|---|---|
| `Assets/_Project/Data/Enemies/Enemy_Boss_Mamemo.asset` | **신규** — `AttackUnitData`, `nightmareMechanics` 빈 배열 |
| `Assets/_Project/Data/EnemyCatalog.asset` | `units` 배열 끝에 추가 (`runtime-stat-refresh` 의 id 해석 대상) |
| `Assets/_Project/Scripts/Data/Decks/Deck_{Coil,Endless,Hook,Serpent,SiegeTest,Spiral,Twin,Zig}.asset` | `bossPool` 3번째 항목 (8개 덱) |
| `Assets/_Project/Tests/EditMode/WavePatternGeneratorBossTests.cs` | 무회귀 테스트 추가 |
| `docs/reference/score-formula.md` | 보스 티어 행에 `Boss_Mamemo` 추가 |

`Deck_WaypointLab` 은 `bossPool` 이 비어 있다(→ `bossUnit` 단일 폴백). **건드리지 않는다** —
그 덱은 웨이포인트 실험용이고, 손대면 폴백 경로의 무회귀 증명이 사라진다.

## 구현

### 스탯 초안 (제약 6 — 전부 SO 값, 실플레이 튜닝 대상)

| 항목 | 값 | 근거 |
|---|---|---|
| `id` / `displayName` | `boss_mamemo` / 마메모 | id 는 카탈로그·시트 키. **에셋 생성 후 rename 금지** |
| `enemyClass` | `Tanker`(1) | 나이트메어와 같은 티어. 클래스 하드 타게팅 적(킨들러)의 대상 마스크에 영향 |
| `health` | 1100 | 나이트메어 1000 · 짱쎈놈 950. 실드가 붙으면 실효 HP 최고가 된다 |
| `moveSpeed` | 1.4 | 나이트메어 1.0 · 짱쎈놈 2.2 의 중간. 느리면 방어유닛에 닿기 전에 죽는다 |
| `attackMethod` / `attackRange` | Melee(1) / 2 | 나이트메어와 동일 |
| `attackCooldown` / `hitDelaySec` | 1.5 / 0.3 | 셋 중 가장 느린 평타 |
| `attackTargetCount` | 1 | 단일. cleave 는 짱쎈놈의 것 |
| `outputs[0]` | Damage 40 | **셋 중 최약** — 나이트메어 100 · 짱쎈놈 30×3. "안 아픈데 성가시다"가 정체 |
| `killScore` | 10 | 보스 티어(기본값 1 — **명시 저작 필수**) |
| `stabilityDamage` | 5 | 보스 티어(기본값 1) |
| `awakeningReward` | 5 | 보스 티어(기본값 1) |
| `spineVisualScale` | 2.9 | 나이트메어 3.2 · 짱쎈놈 2.6 사이 |
| 애니메이션 | Idle / Walk / `Attack1` / Die | 기존 두 보스가 `Attack3` 이라 **구분되는 모션**을 쓴다 |
| `nightmareMechanics` | **빈 배열** | unit 1·3 에서 채운다 |

`skeletonDataAsset` · `visualMaterial` 은 기존 두 보스와 같은 것을 쓴다.

### 외형 (확정 — 오프스크린 컨택트 시트로 육안 선정)

| 파츠 | 값 | 이유 |
|---|---|---|
| helmet | `helmet_c_10` | 늘어진 꼬리 + 폼폼 **나이트캡**. 나이트메어(납작한 두건)·짱쎈놈(캡+헤드폰)과 **실루엣이 겹치지 않는다** — 작은 화면에서 실루엣이 색보다 먼저 읽힌다 |
| eyes | `eyes_c_3` | 반쯤 감긴 졸린 눈. 20종 중 유일 |
| top | `top_c_44` | 흰 니트 베스트 = 잠옷풍 |
| gear_right | `gear_right_c_6` | **베개 방망이**. 짱쎈놈의 가시 곤봉과 안 겹치고, 능력(재우기)과 소품이 같은 말을 한다 |
| `attackAnimation` | `Attack1` | 옆으로 후려치는 모션 — 베개와 맞는다. 기존 두 보스는 `Attack3` |
| `weaponTrailPrefab` | **null** | 베개에 검기 궤적은 어색하다. 기존 두 보스는 있다 — **의도적 부재** |

**나이트캡 색 = `slotColors` 틴트다.** `helmet_c_10` 의 원본은 빨강이라 그대로 두면 **산타로 읽힌다**.
`helmet_` 슬롯에 `(0.62, 0.62, 0.90)` 을 걸어 자주빛으로 낮춘다.

> **틴트의 두 함정 (둘 다 확인함)**
> 1. **슬롯 이름은 `helmet` 이 아니라 `helmet_` 이다.** `helmet` 에 걸면 조용히 아무 일도 안 일어난다.
> 2. **틴트는 곱연산이라 빨강을 파랑으로 못 만든다.** 자주빛까지가 한계이고, 파스텔이 필요하면
>    밝은 바탕 모자(`helmet_c_3` 크림)를 골라야 한다. 이 스켈레톤의 **어떤 애니메이션도 `helmet_`
>    슬롯 색을 키잉하지 않는다**(10종 전수 확인) — `SpineCombinedSkinCache` 주석이 경고한
>    "애니가 틴트를 덮는" 사고는 이 슬롯엔 없다.

### 무회귀 테스트

`WavePatternGeneratorBossTests` 에 케이스 1개 추가. 현재 이 파일이 덮는 것은
Count==1 legacy 동치 · 결정론 · 로테이션 · null 필터 · pool 누출이고, **«Count 2 vs 3»
케이스가 없다.**

단언: 같은 seed·같은 덱에서 `bossPool` 이 2종일 때와 3종일 때, **보스 웨이브를 뺀 모든
웨이브의 그룹 구성이 완전히 같다.** 보스 웨이브도 `escortCount`/`escortType` 이 같고
보스 종류만 다를 수 있다.

근거: `Unity.Mathematics.Random.NextInt(min,max)` 는 range 와 무관하게 `NextState()` **1회**만
소비한다(rejection 루프 없음). 보스 치환은 일반 웨이브 루프가 **끝난 뒤**에 돈다
(`WavePatternGenerator.cs:157`). 따라서 pool 크기는 rng 스트림의 **위치**를 바꾸지 않는다.

`waveGeneratorVersion` 은 **올리지 않는다** — 순수 로그 라벨이고, 잡몹 편성이 안 바뀌므로
올릴 이유가 없다.

## 완료 기준

- [x] EditMode 보스 테스트 14/14 · 신규 2건(«Count 2 vs 3 잡몹 편성 동일» · «3종 로테이션») 통과
- [x] 전체 EditMode 2142건 중 실패 2건 — **둘 다 이 작업과 무관**. `SpawnAlertForecastTests` 는
      병행 세션이 `TryGetSpawnAlertForecast` 를 재작성 중이고(`BattleBridge` +198줄 미커밋),
      `MovementSystemTests.GoalRoute_UsesUnitsTraversalLayerSlot` 은 같은 세션의 통행 층 작업
      (`MovementSystem` +60줄 미커밋)이다. **이 unit 의 런타임 C# 변경은 0줄이다.**
- [x] `EnemyCatalog` 13 → 14 · `bossPool` 8개 덱 2 → 3
- [x] **외형 육안** — 오프스크린 렌더로 Idle/Walk/Attack1 × 보스 3종 대조. 마메모가 나이트캡·졸린 눈·
      베개로 한눈에 구분된다
- [x] `score-formula.md` 보스 행에 `Boss_Mamemo`
- [ ] **Play 육안(사용자)**: 5웨이브에 마메모가 스폰되고 골 방향으로 걸어간다 + 호위 3~4기
- [ ] **Play 육안(사용자)**: 다른 웨이브 구성이 이전과 달라 보이지 않는다
- [ ] 콘솔 경고 0 — 특히 `BakeNightmareMechanics` 가 빈 mechanics 를 **조용히** 지나가는지
      (경고를 뱉으면 그게 회귀다)

> **검증 절차 주의** (README 계약 11): 능력 검증용으로 `bossPool` 을 마메모 단독 고정하면
> `Count==1` **rng 미소비 경로**로 갈라져 웨이브 편성이 라이브와 달라진다.
> **무회귀 확인과 외형 확인을 같은 판에서 하지 않는다.** 3종 균등이면 판당 보스 2~3회라
> 마메모를 한 번도 못 볼 수 있다 — 외형 확인은 단독 고정 판에서 한다.
