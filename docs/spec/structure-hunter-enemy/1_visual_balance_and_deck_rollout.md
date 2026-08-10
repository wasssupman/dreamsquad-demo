# unit 1 — 실루엣 구분 · 동시 등장 상한 · 덱 편입

## 목적

unit 0 이 만든 적을 **실제 판에 올린다.** 세 가지가 한 커밋이어야 하는 이유는 셋이 서로의 전제이기 때문이다 — 덱에 넣으면 동시 등장 상한이 없이는 판이 끝나고, 상한을 걸어도 잡몹과 구분이 안 되면 플레이어는 무엇이 자기를 죽였는지 모른다.

## 변경 대상

- `Assets/_Project/Scripts/Data/AttackUnitData.cs` — `maxPerWave` 저작 축 신설
- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — 순수 클램프 + 호출 2곳(일반 웨이브·보스 호위)
- `Assets/_Project/Tests/EditMode/` — 클램프 순수 함수 테스트
- `Assets/_Project/Data/Enemies/Enemy_Heartseeker.asset` — 실루엣 저작 + `maxPerWave`
- `Assets/_Project/Scripts/Data/Decks/Deck_*.asset` — 라이브 **7종**(6 + `Endless`) 편입 + `waveSeed` 갱신

## 구현

### 1. `maxPerWave` — 왜 새 축이 필요한가

지금 생성기에는 **종류별 수량 상한이 없다.** 일반 웨이브는 `countA = rng.NextInt(1, total)` 로 최대 24기가 한 종류로 나올 수 있고, 보스 웨이브 호위는 `rng.NextInt(escortMin, escortMax+1)` = 3~4기다. 기존 적은 전부 **몸으로 막을 수 있어서** 이 상한 없음이 문제가 되지 않았다. 마음사냥꾼은 아니다 — 3기면 1000HP 타워가 약 13초에 무너진다.

```csharp
[Tooltip("한 웨이브에 이 유닛이 나올 수 있는 최대 수. 0 = 무제한. seed 생성 웨이브에만 적용.")]
[Min(0)] public int maxPerWave = 0;
```

**기본값 0 = 무제한이라 기존 적 12종은 값이 바뀌지 않는다.**

### 2. 클램프는 순수 함수다 (제약 10)

```
ClampGroupCounts(maxA, maxB, ref countA, ref countB)
```

상한을 넘은 몫을 잘라내고, **남은 몫을 여유가 있는 쪽으로 넘겨 웨이브 총량을 보존**한다(둘 다 상한이면 총량이 준다 — 그게 상한의 목적). **rng 를 소비하지 않으므로** 기존 덱의 웨이브 스트림이 흔들리지 않는다. 비자명(재분배 분기)하고 밸런스 직결이라 EditMode 테스트 대상.

호출 2곳: 일반 웨이브의 `countA/countB` 직후, 보스 호위의 `escortCount` 직후.

### 3. 실루엣 — 색이 아니라 형태로 가른다

현재 `partSkins` 가 `Enemy_Basic` 과 **완전히 동일**해서 색만 다른 리컬러다. 작은 화면에서 잡몹 무리에 섞이면 못 읽는다.

**`back`(망토)을 쓰는 적이 13종 중 하나도 없다** — 이것이 가장 강한 구분 축이다. 여기에 `helmet`(민머리 대신)을 더해 실루엣을 바꾼다(`Kindler` 가 helmet+top 으로 같은 전략을 쓰지만 망토가 없다).

### 4. 덱 편입 + 웨이브 재기준

**라이브 덱은 7종이다** — `Serpent·Coil·Twin·Spiral·Zig·Hook` + **`Endless`**. `WaveKillBudgetPinTests` 의 열거가 정본이다.

**풀에 1종을 더하면 `waveSeed` 가 고정이어도 웨이브 1부터 전 구성이 재추첨된다**(`rng.NextInt(0, pool.Count)`). 피할 수 없다. 따라서 **`waveSeed` 를 새 값으로 갱신해 그것이 새 baseline 임을 diff 에 드러낸다** — 시드를 그대로 두면 「같은 시드인데 내용이 다르다」는 거짓 동일성이 남아 과거 기록과의 비교가 조용히 깨진다.

**삽입 위치는 풀 맨 뒤가 아니다.** `ResolveWaveEligibleIndex` 는 게이트에 걸리면 풀 순서로 **앞으로 순환**하므로, 맨 뒤(index N-1)에 두면 웨이브 1~7 의 그 몫이 전부 index 0(`Enemy_Basic`)으로 떨어진다. 중간에 넣어 편향을 한 유닛에 쌓지 않는다.

## 완료 기준

- [x] `maxPerWave` 기본값 0 — **기존 적 12종 저작값 무변경**(에셋 미편집)
- [x] `ClampGroupCounts` EditMode 테스트 6개 — 절단 · 재분배 · 둘 다 상한 시 총량 감소 · 상한 0 무변경 · 부분 여유 채움 · **천장이지 목표가 아님**
- [x] 생성기가 **rng 를 추가 소비하지 않는다** — 클램프는 순수 함수이고 호출 전후로 rng 콜 수가 같다
- [x] **상한이 실전 플랜에서 걸린다** — 라이브 7덱 × 100웨이브 생성 후 직접 카운트: 마음사냥꾼 등장 **109웨이브 전부 최대 2기**, `OVER_CAP=0`, `BEFORE_GATE=0`(웨이브 8 이전 등장 0)
- [x] **실루엣** — 색을 완전히 제거한 오프스크린 렌더에서 Basic·Kindler·마음사냥꾼이 윤곽만으로 구분된다(헬멧 + **등짐** + 넓은 상체 + 큰 체구). `back` 슬롯을 쓰는 적은 13종 중 이것뿐 (`Assets/Screenshots/heartseeker_silhouette.png`, 비추적 스크래치)
- [x] 라이브 덱 **7종 전부** 편입 — 6덱 pool 10→11, `Endless` 9→10
- [x] EditMode **2117 / 실패 0 / 의도적 스킵 3** (신규 6개 포함, 기존 웨이브 테스트 전량 그린) · 콘솔 신규 에러 0
- [ ] **라이브 Play: 웨이브 8 이후 실제 등장 + 골 압박 체감 (사용자 확인 대기)**

### 저작 확정값

| | 값 | 근거 |
|---|---|---|
| `maxPerWave` | **2** | 2기 × 25 피해/1초 = 50 dps → 1000HP 타워를 20초에 무너뜨린다. 그 20초가 플레이어가 800HP 를 녹여야 하는 창이다 |
| `minWaveNumber` | 8 | 대응 축 하나를 지우는 적이라 학습 뒤에 등장 |
| HP / 이동속도 / 근접 피해 | 400 / 2.0 / 25 | unit 0 계측값 유지. 사용자 체감 확인 후 조정 |
| `waveSeed` | 20260801~07 → **20260811~17** | 풀에 1종을 더하면 재추첨이 불가피하다. 시드를 그대로 두면 「같은 시드인데 내용이 다르다」는 거짓 동일성이 남는다 |
