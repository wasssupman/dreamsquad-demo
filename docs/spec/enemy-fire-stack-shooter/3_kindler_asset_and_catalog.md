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

- [ ] `EnemyCatalog.units` 에 등장, 라이브 덱 6종 `attackUnitPool` 에 등장
- [ ] **첫 웨이브 미등장**: 6맵 각각의 `waveSeed` 로 `WavePatternGenerator.Generate` 를 돌려
      wave 1 편성에 `kindler` 가 없는지 확인(EditMode 또는 `execute_code` — Play 불요)
- [ ] 시트 importer 를 태워도(또는 로그인 후) SO 값이 유지되는지 확인 — `kindler` unmatched 로그

### Play 라이브 검증 (스크립트 e2e 또는 육안)

레인저를 1기 배치하고 킨들러가 사거리에 들어오는 상황을 만든 뒤:

- [ ] **타겟팅**: 가디언/파이터를 앞에 세워도 킨들러가 **레인저만** 조준한다. 레인저를 치우면
      아무도 안 쏘고 통과한다(계약 9)
- [ ] **어그로 예외**: 가디언이 킨들러를 어그로하면 조준이 **가디언으로 넘어간다**
      (`AttackSystem` sticky override)
- [ ] **누적**: 레인저의 `StackModifierSlot(kind=Fire)` 이 **슬롯 1개**로 유지되며
      `stackCount` 가 1→…→5 로 오른다 (`execute_code` reflection 프로브)
- [ ] **발화**: 5스택 도달 프레임 근처에서 레인저에게 `DotEffect(origin=Stack, element=Fire)`
      가 붙고, `stackCount` 가 0으로 리셋된다
- [ ] **틱 수·총량**: 화상 1회분이 **6틱 · 총 60** (틱당 10). 데미지 숫자가 0.5초 간격 정수
      "10" 으로 뜨고 초당 수십 개 스팸이 없다
- [ ] **오라**: 레인저에게 `StatusFxKind.Fire` 오라가 점등되고 화상 종료와 함께 꺼진다.
      **Stack origin 으로 이 오라가 켜지는 첫 사례** — `dot-effect-extraction` unit 1 검증 겸함
- [ ] **2축 분리**: 킨들러 화상이 도는 레인저가 있을 때 `DotEffect` 버퍼에 `(Stack, Fire)`
      슬롯만 있고 다른 origin 의 화염과 섞이지 않는다
- [ ] **펄스 리듬**: 화상이 2.85초 뒤 꺼지고, 다음 발화(≈6초 주기)까지 공백이 있다(계약 2)
- [ ] 콘솔 에러 0

### 회귀

- [ ] EditMode 전량 green
- [ ] PlayMode = HEAD 베이스라인과 동일(사전 실패 건수 대조)

## 확인

<!-- 확인 일자 + 커밋 해시 -->
