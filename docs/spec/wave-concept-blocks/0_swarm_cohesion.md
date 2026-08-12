# 0. 동행 — 스웜 속도 정렬 (단독 선행)

## 목적

**이 spec 전체의 전제를 컨셉 기계 없이 검증한다.** 전제는 «스웜이 덩어리로 오면 부딪히는 순간이 생기고 광역 스킬이 답이 된다»다. 틀리면 unit 1~6 이 통째로 흔들리므로, 스폰 시 값 하나로 가장 싸게 먼저 확인한다.

동시에 그 자체로 버그 수리다. 생성기는 보스를 선봉으로 스폰하지만(`WavePatternGenerator.cs:177` `// 선봉: RoundRobin round 0 = 보스 먼저`) Boss_Nightmare 는 **1.0 u/s 로 전 로스터 최저속**이고 호위 후보는 1.6~5.6 u/s 다. 경로 20셀·tileSize 1 이면 호위가 ~3.6~12.5초, 보스가 ~20초 — **호위가 통째로 죽고 10초 뒤에 보스가 혼자 걸어온다.** 스폰 순서는 접촉을 견디지 못한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/GeneratedWavePlan.cs` — `WaveSpawnGroup` 에 `cohesionGroup`
- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — 그룹 조립 시 `cohesionGroup` 부여
- `Assets/_Project/Scripts/Data/AttackDeck.cs` — `swarmCohesion` (bool, 기본 true)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `PendingSpawnEntry.speedOverride` · `QueueWave` 에서 그룹별 최저 속도 계산 · `SpawnUnit` 의 `speed` 대입(`BattleBridge.cs:8136`)

## 구현

**`cohesionGroup`**: `int`, `-1` = 동행 없음. 같은 값을 가진 그룹들의 유닛 전원이 한 덩어리다. **필드 위치는 `WaveSpawnGroup` 맨 뒤**(직렬화·positional 인자 호출자 보호 — `bossPool`·`unitGrowthPerWave` 를 맨 뒤에 붙인 것과 같은 이유).

**unit 0 의 부여 규칙** (컨셉이 없으므로 단순하다):

- `swarmCohesion = false` → 전 그룹 `-1`. 현행 동작과 byte-identical.
- `swarmCohesion = true` → **웨이브 하나가 곧 하나의 동행 그룹**. 일반 웨이브의 두 그룹, 보스 웨이브의 보스+호위 그룹 모두 `0`.

unit 3 에서 컨셉이 이 부여의 소유권을 가져가 `laneGroup` 별로 세분한다(계약 3). 그때 「평소」 컨셉은 동행 off 로 저작되므로 **unit 0 의 «전 웨이브 동행»은 최종 상태가 아니다** — 전제 검증용 중간 상태다. 되돌리는 스위치가 `swarmCohesion` 이다.

**속도 정렬은 QueueWave 에서 미리 계산해 pending 에 싣는다.** 스폰 시점에 형제 엔트리를 되짚지 않는다:

```
QueueWave: cohesionGroup 별 min(group.unit.moveSpeed) 를 구해
           그 그룹에서 나온 엔트리 전부의 speedOverride 에 기입 (-1 그룹은 0)
SpawnUnit: speed = pending.speedOverride > 0f ? pending.speedOverride : unitType.moveSpeed
```

min 한 줄이고 호출처가 하나이므로 별도 static 으로 빼지 않는다(제약 10 의 «자명한 산술 + 단일 호출처» 판정).

**스탯 모디파이어와 충돌하지 않는다.** 정렬은 스폰 시 기저값을 다르게 쓰는 것이고, 슬로우·헤이스트는 그 위에 `MoveSpeedMul` 로 곱해진다(`EnqueueMoveSpeedMul`, stackId 0). 즉 동행한 떼에 슬로우 필드를 걸면 떼 전체가 같은 비율로 느려져 **진형이 유지된 채** 느려진다.

## 완료 기준

- **EditMode** — `swarmCohesion=true` 인 덱에서 보스 웨이브의 보스 그룹과 호위 그룹의 `cohesionGroup` 이 같다. `false` 면 전 그룹 `-1` 이고 생성 결과가 현행과 동일(signature 비교).
- **PlayMode** — 보스 웨이브 스폰 후 N프레임 뒤 **보스와 호위의 골까지 거리 차가 임계 이내**. 대조군으로 `swarmCohesion=false` 에서는 차이가 크게 벌어진다. *순수 함수 그린은 증거가 아니다* — 얼어붙은 유닛도 컴포넌트 단언을 통과한다(`traversal-layers` unit 5 실패 사례).
- **Play 육안 (사용자 확인)** — ① 웨이브가 덩어리로 도착하는가 ② 방어선에 부딪히는 순간이 생기는가 ③ 광역 스킬을 «지금 쓴다»는 판단이 생기는가 ④ 보스가 호위와 함께 오는가.
- **콘솔 0 에러**.

③이 부정이면 계약 4 이후를 재설계한다 — 컨셉을 만들기 전에 멈춘다.
