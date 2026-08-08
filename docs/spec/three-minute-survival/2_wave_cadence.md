# 2 — 웨이브 케이던스: 전멸 즉시 진행 · 20초 상한 · 당기기 비활성

## 목적

웨이브를 **시각 그리드**에서 **이벤트 구동**으로 바꾼다. 전멸하면 바로 다음, 안 되면 20초 뒤
자동. 플레이어의 당기기 경로는 없앤다. 상한 100웨이브, 수량은 완만한 지수로 오른다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 스케줄러, 읽기 API, `_waveTimeShift` 은퇴
- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — 수량 곡선, 명목 트리거 시각
- `Assets/_Project/Scripts/Data/AttackDeck.cs` — `maxWaveIntervalSec`·`unitGrowthPerWave` 추가,
  `fixedWaveIntervalSec` 은퇴
- `Assets/_Project/Scripts/Data/Decks/Deck_*.asset` (**7개, `Deck_Endless` 포함**) — 아래 저작값
- `Assets/_Project/Scripts/UI/NextWaveDock.cs` — 버튼 제거 → 정보 표시
- `Assets/_Project/Scripts/UI/Draft/WavePatternStripView.cs` — 카드 수 상한
- `Assets/_Project/Scripts/UI/Tutorial/FirstSessionTutorialController.BattleHud.cs` — 당기기 안내 제거
- `docs/reference/map-wave-balancing.md` — §웨이브 knob·수량 결정 방식·리드인 절 갱신
- 테스트: `WaveCountRampTests`(6+) · `WaveFixedIntervalTests` · `WaveSpawnLeadInTests` ·
  `WaveSpawnForecastTests` · `WavePatternGeneratorTests` · `WaveKillBudgetPinTests` ·
  `WaveForceRescheduleTests`(5) · `NextWaveClearReadyTests`(10) ·
  `Tests/PlayMode/NextWaveClearAttentionSmokeTest` · `Tests/PlayMode/FirstSessionTutorialSmokeTest`

## 구현

**1. 스케줄러** — `QueueDueWaves` 의 `elapsedSec >= ScheduledWaveTime(i)` 판정을 버린다:

```
다음 웨이브 = _nextWaveIndex > 0 && (NoQueuedAttackersRemain() || _battleClock − _waveStartSec >= maxWaveIntervalSec)
```

- 시계는 **`_battleClock`**(Battle 도메인)이다. 실시간을 쓰면 정지·슬로우모에서 갈라지고
  (`BattleBridge.cs:1855-1856` 의 같은 경고), 첫 판 튜토리얼은 전투 시작 직후 이 도메인을 0으로
  정지시킨다(`BattleHud.cs:152`) — 실시간이면 튜토리얼 중에 웨이브가 쏟아진다.
- 필드 판정은 기존 `NoQueuedAttackersRemain()`(`_pending` 0 + `AttackUnitTag` 0)을 재사용한다.
- 웨이브 1 은 지금처럼 판 시작에 큐잉하고, `_nextWaveIndex > 0` 가드로 시작 직전의 "필드가
  비었다"가 즉시 트리거되는 것을 막는다.
- 20초는 **트리거 기준**이다. 트리거 시 `_waveStartSec = _battleClock`.
- 리드인·스폰 예고는 불변 — 자동/전멸 진행 모두 기존 `QueueWave` 한 경로를 지난다.
  `_waveTimeShift`(당기기 오프셋)는 은퇴한다.
- `GeneratedWave.triggerTimeSec` 은 **명목값**(`i × maxWaveIntervalSec`, 최악 케이스 시각)이 되고
  런타임은 읽지 않는다.
- **작성 플랜(`_usingAuthoredPlan`)은 기존 시각 스케줄을 유지한다.** 저작된 `durationSec`
  타임라인이 그 모드의 정본이므로(`wave-authoring-test-mode` 계약) 이벤트 구동을 적용하지 않는다.

**2. 스폰 창 불변식** — `waveSpawnLeadInSec + (N−1) × intraWaveSpacingSec < maxWaveIntervalSec`.
위반하면 `_pending.Count > 0` 이 영구히 참이 되어 **"필드에 적 0기" 분기가 절대 성립하지 않고**
20초 고정 케이던스만 남는다. 라이브 값 `spacing=1`·`leadIn=2` 에서는 N≥19 에서 깨진다 — 아래
저작값이 `spacing` 을 함께 내리는 이유다. 생성기에 이 불변식 위반 시 경고 1회를 넣는다.

**3. 수량 곡선** — `RampedWaveTotal`(선형)을 지수로 교체한다. 순수 static + EditMode 테스트:

```
total_i = clamp(round(base × growth^i) + jitter_i, base, cap)
base = minUnitsPerWave · growth = unitGrowthPerWave · cap = maxUnitsPerWave
jitter_i = waveSeed 파생 rng, 폭 = waveCountJitter
```

`minUnitsPerWave` 를 base 로 재사용해 YAML orphan 을 피한다. **jitter 는 반드시 `waveSeed`
파생** — "같은 맵 = 같은 웨이브" 불변식이 여기 걸려 있다.

**4. 저작값** — 100웨이브는 **명목**이다. `timerDurationSec 180` + 20초 상한이면 실제 도달은
**10~16웨이브**(못 잡으면 `floor(180/20)+1 = 10`, 즉시 밀면 14~16). 곡선은 그 구간에서 성장이
보여야 의미가 있다:

| 필드 | 값 | 근거 |
|---|---|---|
| `minWaveCount`/`maxWaveCount` | 100 / 100 | 요구 상한. 코드에 100 을 박지 않는다(제약 6) |
| `minUnitsPerWave`(base) | 5 | |
| `unitGrowthPerWave` | 1.12 | i=10 → 16기, i=15 → cap. 도달 구간 전체가 상승 구간 |
| `maxUnitsPerWave`(cap) | 24 | |
| `intraWaveSpacingSec` | 0.5 | cap 24 에서 창 = 2 + 23×0.5 = 13.5초 < 20초 ✓ |
| `maxWaveIntervalSec` | 20 (**Endless 도 20**) | Endless 의 구 10초는 스폰 창 13.5초와 충돌해 불변식을 깬다 — 10초를 유지하면 그 모드에서 전멸 진행이 영구히 죽는다. 무한 모드는 케이던스를 그대로 상속한다 |

`fixedWaveIntervalSec` 제거는 7개 asset 에 YAML orphan 키를 남긴다 — Unity 6.4 에서 정리
불가하고 무해하다(메모리 `forcereserialize-keeps-orphan-keys`).

**5. 당기기 비활성** — `ForceNextWave()` 는 **기능을 유지**하고 플레이어 경로만 없앤다:
`NextWaveDock` 의 버튼을 제거하고 `웨이브 N / 100` + 다음 웨이브까지 남은 초만 표시한다.
no-op 으로 만들면 `TallyFlowTest:104`·`EndlessModeSmokeTest:68`·`MovementIntegritySmokeTest:40`
이 이 메서드를 **판 진행 동력으로** 쓰고 있어 타임아웃으로 죽는다. 단
`WaveForceRescheduleTests`(그리드 리스케줄 계약)는 그리드가 사라지므로 "즉시 다음 웨이브 +
`_waveStartSec` 리셋"으로 재작성한다.

`nextwave-clear-attention` 의 클리어 어필과 `NextWaveClearReady` 는 **은퇴**한다(자동 진행이라
누를 것이 없다) — 관련 테스트 2파일 삭제.

**6. 브리핑 스트립 상한** — `WavePatternStripView.cs:84` 는 `plan.waves.Count` 전량을 카드로
만든다(상한 없음). 100웨이브면 카드 100장, 인트로 = `0.20 + 99×0.06 + 0.30` = **6.44초**,
콘텐츠 폭 27,600px. 카드 수 상한(직렬화 값, 초기 12)을 걸고 초과분은 `…` 한 장으로 접는다.
드래프트 화면이 6.4초 멈추는 것은 후속으로 미룰 수 있는 상태가 아니다.

## 완료 기준

- [ ] 컴파일 통과(테스트 어셈블리 포함), 콘솔 에러/경고 0
- [ ] Play: 웨이브 적을 전부 잡으면 리드인 뒤 **바로** 다음 웨이브가 나온다
- [ ] Play: 적을 안 잡고 두면 트리거 후 20초에 다음 웨이브가 겹쳐 들어온다
- [ ] Play: 도크에 다음 웨이브 버튼이 없고 웨이브 번호·잔여 초가 갱신된다
- [ ] Play: 브리핑 스트립 인트로가 1초 이내에 끝난다(카드 상한 확인)
- [ ] EditMode: 같은 덱·같은 `waveSeed` 로 3회 생성 시 100웨이브 수량 시퀀스가 완전 일치
- [ ] EditMode: `jitter=0` 일 때 곡선이 **단조 비감소**이고 cap 에서 포화하며 base 미만이 없다
      (jitter≠0 이면 국소 감소가 정상이므로 단조성을 요구하지 않는다)
- [ ] EditMode: 저작값이 스폰 창 불변식을 만족하는지 7개 덱 전부 검사하는 pin 테스트
      (`WaveKillBudgetPinTests` 를 이 불변식으로 재정의)
- [ ] 3분 실측: 도달 웨이브 수가 **10~16 범위**이고 마지막 도달 웨이브 수량이 base 의 2배 이상
