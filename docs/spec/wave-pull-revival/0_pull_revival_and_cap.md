# 0 — 당김 복귀 + 겹침 상한

## 목적

플레이어에게 당김 버튼을 돌려주되, **연타로 남은 웨이브를 통째로 큐잉하는 것**을 구조적으로 막는다. 상한 없이 버튼만 되살리면 «다 눌러놓고 버티기»가 항상 최선이 되어 당김이 판단이 아니라 조작이 된다.

**한 커밋이다.** 버튼 복원과 상한을 나누면 «막 눌러도 잘 돈다»가 정상으로 보이는 중간 상태가 생긴다 — `ForceNextWave` 를 판 진행 동력으로 쓰는 PlayMode 스모크 3개가 그 상태에서도 초록이라 회귀를 못 잡는다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 겹침 카운터·리셋 지점·읽기 전용 창구
- `Assets/_Project/Scripts/Data/AttackDeck.cs` — `maxPullsPerClear` 신설
- `Assets/_Project/Scripts/Data/Decks/Deck_*.asset` — 7덱 저작값
- `Assets/_Project/Scripts/UI/NextWaveDock.cs` — 정보 표시 → 버튼 + 정보
- 테스트: `Tests/PlayMode/WavePullCapTest.cs`(신규)

## 구현

**1. 겹침 카운터** — 브리지가 `_pullsSinceClear` 하나를 들고, 세 지점에서만 움직인다:

| 지점 | 동작 | 근거 |
|---|---|---|
| `ForceNextWave()` 성공 시 | `+1` | 얹은 것을 센다 |
| `QueueDueWaves` 의 `cleared` 분기 | `= 0` | 필드를 비웠으면 겹침이 사라졌다 |
| 시계가 0 이 되는 지점 전부 | `= 0` | `_killScoreTotal`·`_waveTimeShift` 와 같은 규칙(계약 9 승계). `BattleBridge.cs:1311·1367·1678` 인근 |

`capReached`(타임아웃 진행)에서는 **리셋하지 않는다.** 20초가 지나 다음 웨이브가 겹쳐 들어온 것은 «정리했다»가 아니라 «못 정리한 채로 하나 더 받았다»이다. 여기서 리셋하면 가만히 있는 것이 당김 예산을 벌어준다.

**2. 기제와 규칙을 분리한다** — 상한을 `ForceNextWave()` 안에 넣으면 **기존 스모크 3종이 죽는다**:

호출처는 **PlayMode 7곳 + EditMode 3파일**이고 전부 «한 프레임 안의 연타 루프»다. 같은 프레임엔 필드가 빌 수 없어 리셋이 안 걸리므로, 상한 3이면 40연타가 3이 된다:

| 테스트 | 연타 | 상한을 메서드에 걸면 |
|---|---|---|
| `EndlessModeSmokeTest:73` | 40 | 안정도 감소가 느려져 30000프레임 안에 못 볼 수 있다 |
| `TallyFlowTest:89` | 20 | 90초 타임아웃(`ResultTimeoutSec`)에 걸려 실패 |
| `GoalStabilityTest:48` | 20 | 같은 종류의 진행 정체 |
| `StructureLivePlayTest:130·188` | 20·5 | 〃 |
| `MapCrowdClearanceTest:71` | 6 | ⚠ **가장 위험**. 6→3 이 돼도 `Assert.Greater(peakEnemies, 5)` 를 통과한다 — 군집 통과 교착의 유일한 회귀 가드가 **밀도 반토막인 채 초록으로 남는다** |
| `InstinctNearestTargetMeasureTest:113` | WaveRush | 측정 전제가 조용히 바뀐다 |
| `MovementIntegritySmokeTest:41` | 1 | 영향 없음 |
| EditMode 3파일 | `WaveForceRescheduleTests` 등 | 리스케줄 계약 검증이 막힌다 |

이 테스트들을 고쳐 상한을 우회시키는 것은 **테스트를 구현에 맞춰 비트는 것**이다. 대신 두 층으로 나눈다:

```csharp
public void ForceNextWave()      // 기제: 상한을 보지 않는다. 카운터는 올린다.
public bool TryPullNextWave()    // 규칙: 상한을 검사하고 통과하면 위임. **플레이어 경로는 이것뿐.**
```

도크는 `TryPullNextWave()` 만 부른다. 스모크 3종은 `ForceNextWave()` 를 그대로 쓰므로 **한 줄도 안 고친다**. 상한은 게임 규칙(플레이어 입력에 대한 제약)이지 스케줄러의 물리 법칙이 아니므로 이 분리가 의미와도 맞는다.

**3. 상한 판정** — `_pullsSinceClear < ActiveDeck.maxPullsPerClear`. 덱값이 0 이하면 **당김 금지가 아니라** 폴백 상수를 쓴다 — 0 을 «금지»로 읽으면 저작 누락이 조용히 기능을 끄는 침묵이 된다. 폴백은 경고 1회와 함께 3.

**작성 플랜에는 상한을 걸지 않는다.** 예산이 회복되는 사건은 «필드를 비웠다» 하나인데, 작성 플랜(`_usingAuthoredPlan`)은 저작된 시각 타임라인이 정본이라 `QueueDueWaves` 의 전멸 분기를 **구조적으로 지나지 않는다**(`wave-authoring-test-mode` 계약). 그대로 걸면 저작 모드에서 3회 뒤 버튼이 **영구 잠긴다**. 특수 케이스를 더하는 것이 아니라 규칙을 그대로 읽은 것이다 — **회복 사건이 없는 모드에는 상한도 없다.**

산식이 비교 한 줄이라 별도 순수 함수로 빼지 않는다(제약 10 의 과잉 추출 단서). 대신 아래 PlayMode 테스트가 카운터의 **리셋 규칙**을 고정한다 — 버그가 나올 자리는 산식이 아니라 리셋 지점이다.

**4. 읽기 전용 창구 3개** — 도크가 브리지 내부를 모르게 한다:

```csharp
public bool  PullAvailable      // 버튼을 보일 조건 (기존 NextWaveAvailable && NextWaveHasNext)
public bool  PullAllowed        // 지금 누를 수 있나
public int   PullsRemaining     // 남은 횟수 (잠금 사유 표시용)
```

**5. 도크 UI** — `three-minute-survival` 이전의 버튼 chrome 을 되살리되 지금 표시를 **잃지 않는다.** 아래 행은 버튼이 되고, 버튼 안에 `웨이브 N / M` 을 유지하며, 잔여 초·컨셉 라벨(`wave-concept-blocks` unit 5)은 그대로 아래 줄에 남는다.

**잠길 때 버튼을 숨기지 않는다**(계약 3). 회색으로 잠그고 라벨을 «정리하면 다시»로 바꾼다 — 사라지면 «왜 없지», 무반응이면 «고장났나»가 된다.

**6. 당김이 판단거리인지 실측 (PRD §9 V1 대용)** — 같은 시드로 두 판을 돌려 최종 점수를 비교한다:

| | 플레이 |
|---|---|
| ⓐ | 한 번도 당기지 않는다 |
| ⓑ | 열릴 때마다 당긴다 |

**ⓑ − ⓐ 가 +면 당김이 점수를 만든다**(판단거리가 성립). **ⓑ ≤ ⓐ 면 당김이 손해**이므로 페이스·공급량 재저작이 선행이다.

원래 두려던 기준(「상한까지 당겨도 3분 안에 전량 소화되지 않는다」)은 **버린다 — 구조적으로 항상 참이라 정보량이 0이다.** 라이브 덱은 전부 `minWaveCount = maxWaveCount = 100` 이고 3분 실도달은 10~16 이라, 상한 3으로 최대한 당겨도 30을 못 넘는다. 통과해도 아무것도 배우지 못하는 기준은 기준이 아니다. 위 A/B 는 **실패할 수 있는** 측정이다.

⚠ 이 실측은 **unit 2 뒤에** 한다. unit 2 가 편성을 바꾸므로(`waveGeneratorVersion` 4) 앞에서 잰 값은 그때 버려진다.

## 완료 기준

- [ ] 컴파일 통과(테스트 어셈블리 포함), 콘솔 에러/경고 0
- [ ] PlayMode `WavePullCapTest`: `TryPullNextWave()` 를 상한까지 부르면 그 다음 호출이 **false 를 내고** `_nextWaveIndex` 가 안 오른다
- [ ] PlayMode: 같은 상태에서 `ForceNextWave()`(기제)는 **여전히 통과**한다 — 두 층이 분리돼 있다는 증거
- [ ] PlayMode: 필드를 비우면 당김이 다시 열린다 (리셋 규칙)
- [ ] PlayMode: 타임아웃 진행으로는 **열리지 않는다** (리셋 아님)
- [ ] PlayMode: 판을 다시 시작하면 카운터가 0 이다 (계약 9)
- [ ] **`ForceNextWave` 호출처 7곳 전부 무회귀** — 위 표의 PlayMode 7 + EditMode 3파일. 특히 `MapCrowdClearanceTest` 의 `peakEnemies` 가 이전과 같은 대역인지(밀도 반토막인 채 초록이 되는 것이 이 spec 의 최대 회귀 위험)
- [ ] Play: 버튼을 누르면 다음 웨이브가 리드인 뒤 즉시 오고, 상한에 닿으면 잠기며, 정리하면 풀린다
- [ ] **Play 실측(unit 2 뒤에)**: 같은 시드 ⓐ무당김 / ⓑ열릴 때마다 당김 두 판의 최종 점수. **ⓑ > ⓐ** 여야 당김이 판단거리다
