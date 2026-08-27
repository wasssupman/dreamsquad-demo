# 4 — 드림캐쳐 카드 (즉발 · hand-op)

## 목적

카드 32행 중 **실행 클래스가 다른 두 종류**를 처리한다.
`3_cards_slot_arm.md` 와 나눈 이유는 이 둘이 슬롯을 안 타기 때문이다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — 즉발 5행 경로
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs` — hand-op 실행자
- `Assets/_Project/Scripts/Skills/Concrete/`

## 구현

1. **즉발 5행**(`trigger = None`) — 슬롯 없이 **부착 즉시** 실행한다.
   예: 호접몽(`DreamCocoon`) · `BountyMark`. 감지자를 안 거치므로 디스패처 3지점이 아니라
   **부착 지점에서 직접 `Execute`** 한다. 「호출자 = 소유자」의 가장 단순한 형태다.
   ⚠ 부착 경로는 **요청-응답**이다 — `ApplyDreamcatcherCardToUnit` 이 부착 코드(-1 = 무차감
   거절)를 반환해 **코스트 환불**을 결정한다(`BattleBridge.Dreamcatcher.cs:310~441`).
   `Execute` 는 void 이므로 **가부 판정은 스킬 밖**(기존 `DcApplicability` preflight — 이미 순수)에
   유지하고 `Execute` 는 발동만 담당한다. 토대 unit 0 의 판정을 따른다.
2. **`RecallAttachedToFront`(hand-op)** — 실행자가 sim 도 브리지도 아니라
   `DreamcatcherHandController`(Mono)다. `DcMechanic.cs` kind 25 주석이 그것을 명시한다.
   **판정 필요**: Mono 계열 intent 로 표현할지, 구조적 예외로 명문화할지.
   예외로 두면 §「범위 밖」에 근거와 함께 적는다 — 「아직 안 옮김」은 예외가 아니다.
3. **`RegisterPlacementAura` 의 revoke 핸들** — host 사망 시 `RevokeDreamcatcherEffects(handle)`
   가 회수한다. fire-and-forget `Emit` 으로 표현 불가. 별도 포트 메서드 또는 예외로 판정
   (토대 unit 0).

## 완료 기준

- [ ] 즉발 5행이 concrete 로 존재하고 부착 지점에서 직접 `Execute` 된다
- [ ] 부착 가부/코스트 환불이 **스킬 밖**에 남아 있고 동작이 바뀌지 않았다
- [ ] hand-op 이 이전됐거나, 예외로 **근거와 함께** 명문화됐다
- [ ] PlacementAura 회수가 동작한다 (host 사망 → 효과 소멸 PlayMode 단언)
- [ ] 그물 초록


---

## 진행 (2026-08-26)

| 조각 | 내용 | 상태 |
|---|---|---|
| **4a** | **부착 seam 개통** + `SelfBuffLethal`(2행 — 마지막 불꽃·재앙의 심장) | 완료 |
| **4b** | `DreamCocoon`(호접몽) | 완료 |
| **4c** | `BountyMark`(살찌운 제물) | 완료 |
| **4d** | `PlacementAura` + hand-op — **둘 다 예외로 확정** | 완료 |

## 4a~4c 에서 나온 것

**여섯 번째 seam 이 필요했고, 그 이유가 다른 다섯과 다르다.** 앞의 다섯은 전부 시뮬
프레임 안의 사건이라 「다음 틱에 드레인」이 성립한다. 부착은 아니다 —
`ApplyDreamcatcherCardToUnit` 이 **동기 트랜잭션**이라 preflight 로 가부를 정하고 쓰기를
하고 핸들(또는 −1, 무차감 거절)을 그 호출에서 돌려준다. 큐에 넣고 프레임을 기다리면
그 결정 뒤에 쓰기가 도착한다.

그래서 `SkillSeam.Immediate` 는 **자기 순서를 갖지 않는다.** 그룹에 있는 것은 안전망이고,
실제 실행은 브리지가 `Update()` 를 직접 불러 자기 콜스택 안에서 끝낸다.
⚠ 이 seam 을 시뮬 사건에 쓰면 안 된다 — 프레임 중간에 임의로 도는 드레인이라 시스템 간
순서 계약(emitter 같은 프레임 · 파괴 전 등)을 하나도 보장하지 않는다.

**그물의 단언이 「`yield` 가 없다」는 것이다.** 세 그물 모두 부착 호출 **직후** 상태를 묻는다.
초록이면 실행이 콜스택 안에서 끝났다는 뜻이고, 빨개지면 seam 이 비동기로 샌 것이다.
이 단언은 다른 어떤 방법으로도 대체되지 않는다.

**호접몽은 의도가 하나여야 했다.** 처음엔 「잠 + 감시」 두 의도로 냈는데, 잠을 `ApplyCc` 로
보내면 그건 **큐 경유라 한 프레임 늦게** 도착한다 — 그 사이에 맞으면 깨울 잠이 없어 파탄이
안 나고 감시만 남는다(공짜 버프). 「개시」를 쪼개지 않는 것이 `BeginUltimateLeap` 과 같은
판단이고, 어댑터가 잠과 감시를 나란히 즉시 쓴다.

**새 어휘 셋.**
- `StartLethalTimer` — 「이 시간 뒤에 죽는다」. 버프의 만료와 죽음은 **다른 사건**이라
  (모디파이어 시계 ↔ `LethalTimer`) 한 의도로 합치지 않는다. 저작이 둘을 같은 초로 적을 뿐이다.
- `BeginDreamCocoon` — 진행형 상태 개시(위 원자성).
- `ScaleKillReward` — 다른 의도가 「값을 준다」인 것과 달리 **가진 값을 배로 만든다**.
  그래서 `Amount` 가 양이 아니라 배율이다. ⚠ 즉시 쓰기여야 한다 — 처치 이벤트가 enqueue
  시점에 보상값을 복사하므로, 재생을 기다리면 같은 프레임에 죽는 적이 배율 없는 값을 싣는다.

**저작 인코딩은 감지자가 푼다.** `% → 배율`, `CardBuffKind → StatKind`, 스택 슬롯 발급이
전부 브리지에 남는다(슬롯 bake 가 같은 자리에서 같은 변환을 하는 것과 같은 규율).

⚠ **테스트 더미에 `SimEntityId` 가 없으면 표식이 조용히 사라진다.** 라이브 스포너는 발급
하는데 `BountyMarkTest` 의 더미가 안 받고 있었다 — 「감지·드레인은 초록인데 효과만 없다」의
그 신호다(unit 3g 에서 두 번, 여기서 세 번째).


## 4d — 범위 밖 (구조적 예외 2건)

「아직 안 옮김」이 아니다. 둘 다 **이 레이어의 형태로는 표현할 수 없는** 이유가 있고,
그 이유가 사라지는 조건도 같이 적는다.

### ① `PlacementAura`(느린 각성) — 효과가 아니라 **구독**이다

지금 무엇도 바꾸지 않는다. 「앞으로 배치되는 같은 axis 유닛에게 이렇게 하라」를 배치
파이프라인에 **등록**하고, 그 등록의 결과가 **회수 토큰**이다.

세 가지가 동시에 막는다:

1. **토큰이 부착 트랜잭션의 반환값이다.** `ApplyDreamcatcherCardToUnit` 이 그 핸들을
   돌려주고 손패 컨트롤러가 host 사망 시 `RevokeDreamcatcherEffects(handle)` 로 회수한다.
   `Execute` 는 void 라 토큰을 돌려줄 수 없다. 토큰 없이 등록하면 회수가 끊겨 **영구 누수**가
   된다 — 그 누수는 이미 한 번 겪어서 「카드당 오라 1개」 가드가 서 있다.
2. **엔티티도 컴포넌트도 큐도 없다.** 브리지의 관리 리스트 둘(`_activeDcEffects`,
   `_activePlacementSleeps`)이 전부이고, 소비자는 시뮬이 아니라 **배치 파이프라인**이다.
3. **payload 가 아니라 카드의 속성을 읽는다** — `card.axis`. 유닛에 붙는 효과가 아니라
   카드가 거는 규칙이라는 증거다.

**옮기는 조건**: 포트가 요청-응답을 갖거나(그러면 fire-and-forget `Emit` 이라는 이 레이어의
형태가 바뀐다), 회수 모델이 **불투명 핸들 → host 키**로 바뀌거나. 후자는 카드 회수 전반을
건드리는 재설계라 unit 4 보다 크다.

⚠ 회수 동작은 그물이 이미 지킨다 — `PlacementAuraTest.Aura_RevokedWhenHostDies_ViaController`.

### ② `RecallAttachedToFront`(손패 회수) — 판이 아니라 **손패**에서 일어난다

3e 에서 미룬 이유(같은 콜스택 순서)는 **부착 seam 이 생기면서 사라졌다.** 그런데도 옮기지
않는 이유는 남는다:

- **효과가 보드에 닿지 않는다.** 손패 카드 순서를 바꾸는 일이고 `ISkillContext` 는 시뮬
  포트다. 옮기면 Mono → ECS 큐 → 드레인 → 메타 싱크 → Mono 의 왕복인데 그 사이에 보드를
  한 번도 안 건드린다.
- **이 레이어가 없애려는 병이 여기엔 없다.** 조건(OnRetire + 이 카드가 선언했나)과 실행
  (손패 재정렬)이 컨트롤러의 **한 메서드 안에** 이미 같이 있다. arm 사본도, seam 도,
  조용한 죽음의 여지도 없다 — 옮겨서 없어지는 것이 0이다.
- 슬롯을 안 굽는 것도 그 결과다(bake 가 의도적으로 건너뛴다). 라우팅 키를 걸 자리가 없다.

**옮기는 조건**: 손패가 두 번째 표면으로 정식화될 때(손패 포트). 그때는 「스킬이 손패에
하는 일」이 여럿이 되어 한 곳에 모을 값이 생긴다. 오늘은 하나다.

## 4d 에서 걷은 것

`EnqueueAttackSpeedMul` — 유일한 호출처가 마지막 불꽃 bake 였고 그게 concrete 로 갔다.

## 4d 에서 잡은 회귀 — **결합 버킷은 파생 축이다**(unit 2b 소급)

`DreamcatcherEffectTest.CrackedGrail_RevokeNeutralizesBothAdditiveEffects` 가 2.0 을
기대하는데 **2.21** 이 나왔다. 2.21 = 1.3 × 1.7 — 가산이어야 할 것이 **곱셈 버킷**에 있었다.

unit 2b 가 오라 셋을 `StatAuraSkill` 하나로 합치면서 보스 채찍의 `Multiplicative` 를
셋 다에 강요했다. 그런데 레거시는 갈렸다:

| 오라 | 레거시 | 버킷 |
|---|---|---|
| 보스 채찍(`AllyMoveSpeedAura` arm) | `op = CombineOp.Multiplicative` **명시** | 곱셈 |
| 가디언 `BoostNearbyDefenders` | `EnqueueStatModifier` → `FromMultiplier` | **가산** |
| 궁수 `BindNearby` | `EnqueueMoveSpeedMul` → 같은 함수 | **가산** |

버킷이 다르면 **다른 버프와 쌓이는 방식**이 달라진다 — 가디언 오라(+30%) 위에 카드
+70% 를 얹으면 가산은 2.0, 곱셈은 2.21 이다. 그래서 결합 버킷을 다섯 번째 파생 축으로
올리고 기본을 `FromAuthoredMultiplier`(= 레거시와 **같은 번역 함수**)로 두었다.
같은 함수를 쓰는 한 감소 배율(궁수의 0.6)까지 자동으로 같은 버킷에 간다.

⚠ **일반화는 「같은 것」을 합칠 때만 안전하다.** 축 넷이 같아 보여 합쳤는데 다섯 번째가
숨어 있었고, 그물이 이미 있었는데도 그 그물을 안 돌려서 두 unit 을 지나쳤다.
