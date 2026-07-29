# 3 — 킨들러 유닛 SO + 카탈로그·덱 등록 + Play 검증

## 목적

적 유닛 `킨들러` 를 만들고 실제 매치에 등장시킨 뒤, **누적 → 5스택 → 화상** 의 전 사슬을
라이브에서 확인한다. 이 단위가 spec 의 검증 질문에 답한다.

## 변경 대상

- `Assets/_Project/Data/Enemies/Enemy_Kindler.asset` (신규)
- `Assets/_Project/Data/EnemyCatalog.asset` — `units` 배열에 추가
- `Assets/_Project/Scripts/Data/Decks/Deck_{Serpent,Coil,Twin,Spiral,Zig,Hook}.asset` —
  `attackUnitPool` 에 추가 (`MapDocumentPool` 이 짝지은 라이브 덱 6종)

## 구현

`Enemy_Sniper` 를 형제로 삼아 저작한다(같은 Shooter · `FocusUntilDead` · `Halt`).

| 필드 | 값 | enum 값 |
|---|---|---|
| `id` / `displayName` | `kindler` / `Kindler` | |
| `enemyClass` | `Shooter` | `4` |
| **`minWaveNumber`** | **`2`** | **첫 웨이브 등장 금지.** `Enemy_Runner`(현재 유일한 사용처)와 같은 값 |
| `attackMethod` | `Projectile` | `2` |
| `targetMode` | `FocusUntilDead` | `2` |
| `engageMovement` | `Halt` | `0` |
| `targetPriorityClass` | `None` | `0` |
| **`targetClassMask`** | **`Ranger` 단독** | `2` (= `1 << 1`) |
| `health` / `moveSpeed` | 45 / 2.0 | |
| `attackRange` / `attackCooldown` | 4 / 1.2 | |
| `attackTargetCount` / `hitDelaySec` | 1 / 0.3 | |
| `projectile` | `Projectile_Enemy_Fireball` | unit 2 |
| `outputs[0]` | `Damage` 5 | |
| `outputs[1]` | `ApplyStack` · `stackKind Fire(1)` · `magnitude 1` · `duration 3.0` · `stackMaxStack 5` | 계약 4 — SO 와 양쪽 명시 |
| Spine | 기존 적 스켈레톤 + 파츠/슬롯 색 placeholder | 화염 계열 색(주황~적) |
| `awakeningReward` / `killScore` | 2 / 100 | |

**등장 게이트(`minWaveNumber`)** — `WavePatternGenerator.ResolveWaveEligibleIndex` 가 소비한다.
뽑힌 인덱스의 유닛이 그 웨이브에 등장 불가면 **rng 를 건드리지 않고** 풀 순서로 다음 허용
유닛까지 순환한다(결정론 보존). 풀 전원이 금지면 `startIndex` 를 그대로 돌려주는 fail-open —
빈 웨이브를 만드느니 게이트를 연다. 적용 범위는 **seed 생성 경로뿐**이고 작성 플랜
(`WavePlanAsset`)은 디자이너 배치를 그대로 존중한다. 라이브 덱 6종은 `useGeneratedWaves: 1`
이라 게이트가 적용된다.

⚠ **덱 풀에 유닛을 추가하면 같은 `waveSeed` 라도 웨이브 구성이 바뀐다.** 생성기가 풀에서
뽑으므로 기존 6맵의 웨이브 편성이 전부 이동한다 — 회귀가 아니라 의도된 결과지만, 이 커밋
이후 "웨이브가 달라졌다"는 관찰은 여기서 설명된다.

⚠ **시트 권위.** 적 스탯은 시트 import 대상이다(`EnemyStatDto`: `health`·`moveSpeed`·`atk`·
`attackRange`·`attackCooldown`·`hitDelaySec`·`targetClassMask`·`targetMode`·`engageMovement`
·`awakeningReward`). 지금은 `kindler` 행이 없어 importer 가 이 SO 를 건드리지 않지만, 행이
생기는 순간 **시트가 권위**가 된다 — 이후 튜닝은 SO 만 고치면 로그인 시 되돌아간다.
`ApplyStack` 의 `stackKind`/`duration`/`stackMaxStack` 과 `minWaveNumber` 는 DTO 에 없어
SO 전용이다.

## 완료 기준

### 배선

- [x] `EnemyCatalog.units` 에 등장, 라이브 덱 6종 `attackUnitPool` 에 등장
- [x] **첫 웨이브 미등장**: 6맵 각각의 `waveSeed` 로 `WavePatternGenerator.Generate` 를 돌려
      wave 1 편성에 `kindler` 가 없음 + **6/6 덱에서 이후 웨이브에는 등장**(등록이 inert 가
      아님을 같이 확인 — 게이트만 보면 "안 나오는 것"과 구분이 안 된다)
- [ ] 시트 importer 를 태워도 SO 값이 유지되는지 — `kindler` 행이 없으므로 구조상 unmatched.
      로그인 경유 실측은 사용자 Play 대기

### 배치 e2e — `KindlerFireStackE2ETest` (PlayMode, 포커스 무관)

배치 근거: 킨들러를 **아처(Ranger) 위**에 두고 가디언을 체비셰프 **4칸** 떨어뜨린다.
4 ≤ 킨들러 사거리(4) 라 가디언도 후보에는 들어오지만, 가디언 사거리는 1 이라 킨들러를
때리지 못해 **어그로가 걸리지 않는다**. 어그로 sticky override 는 클래스 필터를 덮는 것이
사양이므로 그 경로를 배제해야 base 필터를 본다.

- [x] **타겟팅**: 사거리 안의 가디언이 화염 스택을 **한 번도** 받지 않는다
      (= `targetClassMask = Ranger` 하드 필터의 직접 증거)
- [x] **누적**: 아처의 `StackModifierSlot(kind=Fire)` 이 **슬롯 1개**로 유지되며 `stackCount` 누적
- [x] **발화**: 아처에게 `DotEffect(origin=Stack, element=Fire)` 가 붙는다
- [x] **선행 가드**: `GetStackThresholds(Fire).Length > 0` (unit 1 씬 배선)

### 사용자 Play 대기 (프레젠테이션 — 배치로는 판정 불가)

- [ ] **어그로 예외**: 가디언이 킨들러를 때려 어그로하면 조준이 가디언으로 넘어간다
- [ ] **레인저 부재**: 레인저를 치우면 아무도 안 쏘고 통과한다(계약 9)
- [ ] **틱 수·총량**: 데미지 숫자가 0.5초 간격 정수 "10" × 6회. 초당 수십 개 스팸 없음
- [ ] **오라**: 아처에게 `StatusFxKind.Fire` 오라 점등 → 화상 종료와 함께 소등.
      **Stack origin 으로 이 오라가 켜지는 첫 사례**
- [ ] **펄스 리듬**: 화상이 2.85초 뒤 꺼지고 다음 발화(≈6초)까지 공백(계약 2)
- [ ] 콘솔 에러 0

### 회귀

- [x] EditMode 전량 green (1584 중 1582 pass / 0 fail, skip 2 = 기존 Ignored)
- [ ] PlayMode = HEAD 베이스라인 대조

## 확인

- **2026-07-30** · `KindlerFireStackE2ETest` **Passed** (testrig 배치).
  스폰 → 레인저 조준 → 파이어볼 히트 → 화염 누적 → 5스택 임계 → `(Stack, Fire)` 도트까지
  배송 에셋 그대로 통과.
- ⚠ 테스트가 킨들러를 찾을 때 **`EnemyCatalog` 를 쓰면 안 된다** — 그 에셋은 OutgameScene 의
  `UnitStatRuntimeRefresher` 에서만 참조돼 BattleScene 에는 로드되지 않는다(초판이 NRE 로
  죽었다). BattleScene 은 `MapDocumentPool` 을 참조하고 그 풀이 라이브 덱 6종을 물고 있어서,
  덱 `attackUnitPool` 에 등록된 유닛은 씬 로드와 함께 올라온다.
- ⚠ 적 스폰은 `BattleBridge.SpawnUnit`(private) 을 리플렉션으로 탄다. 엔티티를 직접 조립하면
  outputs·`EnemyTargetFilter` bake 를 테스트가 복제하게 되어 **정작 검증하려는 배선을 우회**한다.
