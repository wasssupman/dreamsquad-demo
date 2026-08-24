# 2 — 처치가 스트레스를 덜어준다

## 목적

**악몽을 잡으면 마음이 회복된다**(명제 6). 이게 이 spec 의 「저울」 절반이다 — 스트레스가 오르기만
하면 3분은 버티기 게임이고, 내려갈 수 있어야 «잡을수록 숨통이 트인다»가 성립한다.

**새 저작 필드를 만들지 않는다**(명제 7). `awakeningReward` 가 이미 「이 적을 잡으면 얼마를 주나」를
저작하고 있고 값 분포(잡몹 2 / 엘리트·중간 3 / 보스 5 / 분열체 0)가 그대로 회복 서열로 읽힌다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Units/GoalTowerTag.cs` — **계약 주석 갱신**(+ stale 주석 정리)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SpawnStructureEntities`(버퍼 추가) · `DrainEnemyKilledEvents`(힐) · `EnqueueGoalTowerHeal`(신설)
- `Assets/_Project/Scripts/Data/AttackDeck.cs` — 회복 배율 저작 필드 1개
- `Assets/_Project/Scripts/Data/Decks/*.asset` — 배율 기입
- **테스트 개정 1건**: `Tests/EditMode/GoalTowerArchetypeTests.cs` + `StructureFixtures.MakeGoalTower`
  (브리지 타워와 픽스처의 컴포넌트 집합 대칭차를 단언한다 — 같은 커밋에서 동기화)

## 구현

**1. 골 아키타입에 `IncomingHeal` 버퍼를 붙인다.** 지금 마음은
`GoalTowerTag`/`StructureTag`/`SimEntityId`/`Health`/`IncomingDamage`/`FactionTag`/`LocalTransform` 이다.
버퍼 하나만 추가하면 `DamageApplicationSystem` 이 **이미 있는 줄**로 처리한다:
`newHp = min(maxHp, value − totalDamage + totalHeal)`.

**2. ⚠ `GoalTowerTag` 의 계약을 뒤집는다.** 그 파일 주석이 *"이 엔티티에 ModifierStats /
StatModifierSlot / ShieldSlot / **IncomingHeal** 을 붙이지 않는다"* 라고 못박고 있다. 원 명분은
`MaxHealthScaleSystem` 이 `Health.max` 를 재계산하면 미러가 깨진다는 것인데 그건 **`ModifierStats`
전용**이다 — `IncomingHeal` 단독은 `max` 를 안 건드린다. **주석에서 `IncomingHeal` 만 빼고 나머지
셋은 금지로 유지**하며, 왜 하나만 열었는지 근거를 같은 자리에 남긴다.
곁들여 같은 주석의 stale 한 줄(*"정본은 `GoalTowerHealth` 싱글턴"* — 그 타입은 코드에 없다.
rev 2 에서 per-entity `Health` 정본으로 바뀌었다)도 정리한다.

**3. 힐 넣는 자리는 `EnqueueGoalTowerDamage` 의 형제.** `DrainEnemyKilledEvents` 안에서
`EnqueueGoalTowerHeal(killedType.awakeningReward × 배율)`. 그 루프는 이미 `killedType`(SO 원본)을
들고 있어 새 조회가 없다.
⚠ **단 대상 선택은 형제와 다르다.** `EnqueueGoalTowerDamage` 는 「위치 최근접 1기」인데, 힐은
**살아있는 마음 전체**에 넣는다(unit 0 계약). 피해는 「어느 마음이 맞았나」가 사건의 일부지만
회복은 그렇지 않고, 최근접으로 두면 2골 사고 시 만피 마음이 흡수해 clamp 로 소멸시킨다.
마음 1개에서는 두 규칙이 동치라 **테스트로 구분되지 않으므로** 계약으로 고정한다.

**4. ⚠ SO 원값을 쓴다.** `evt.awakeningReward` 는 「살찌운 제물」 카드의 배율이 **이미 곱해진**
baked 값이다. 그걸 쓰면 카드 하나가 각성 충전과 스트레스 회복 **두 축**을 겸한다.
`killedType.awakeningReward`(원값)를 쓴다. 등록부 miss 시 **회복 0 + 경고 1회**
(`DrainGoalEvents` 의 `_leakTypeMissLogged` 선례) — 조용히 폴백값을 주면 회복이 공짜가 된다.

**5. 배율은 `AttackDeck`.** 판 규칙이 사는 곳이다(`goalStabilityMax`·`timerDurationSec` 이웃).
시트 임포터는 `AttackDeck` 을 다루지 않으므로(확인함) 「로그인 임포트가 되돌린다」 함정이 없다.

**6. 힐 VFX 를 어떻게 할지 결정한다.** `IncomingHeal` 을 쓰면 `DamageApplicationSystem` 이 pulse
마다 `HealAppliedEvent` 를 enqueue 하고 `DrainHealAppliedEvents` 가 **마음 위치에 힐 이펙트를 자동
스폰**한다. 분당 수십 킬이면 마음에서 이펙트가 연발한다. **기본은 억제**(마음 위치 힐 VFX 스킵) —
회복 피드백은 unit 1 의 바 하강과 unit 3 의 림 완화가 이미 담당한다. 억제하지 않기로 하면
그 결정을 이 문서에 적는다.

## 구현에서 드러난 것 (2026-08-23)

**⚠ 「크래시-as-가드」 하나를 잃었다.** 힐러가 마음을 대상으로 고르지 않는 실제 기제는
**진영 마스크**(`DefenderTargetDefaults.AllyMask` = `DefenderUnit` 단독)이고 그건 무변경이다.
그런데 `GoalTargetingPriorityTests` 는 그 계약을 **버퍼 부재**로 재고 있었다 — 마스크를
`AnyDefender` 로 넓히는 실수가 나면 「IncomingHeal 버퍼가 없는 거점이 후보에 들어 ECB playback
이 던진다」로 **즉시 크래시**했기 때문이다.

마음이 버퍼를 갖게 되면서 그 그물이 마음에 한해 사라진다. 같은 실수가 이제 크래시가 아니라
**「힐러가 마음을 조용히 회복시킨다」** 는 밸런스 결함으로 나타난다. 대응 둘:

- 테스트 단언을 프록시(`HasBuffer == false`)에서 **실물**(`GetBuffer(tower).Length == 0`)로 교체
- `DefenderTargetDefaults` 의 근거 주석에 «넓히면 던진다» 가 **약해졌음**을 기록 (본능·적 마음은
  여전히 버퍼가 없어 던진다 — 잃은 것은 마음 한 종류에 대한 그물이다)

**힐 VFX 는 억제로 확정했고 자리는 sim 쪽이다.** `DamageApplicationSystem` 의 힐 펄스 enqueue에
`GoalTowerTag` 게이트를 더했다 — 바로 위 데미지 폰트가 `AttackUnitTag` 로 「적 전용」을 거르는
것과 **같은 형태**라 새 관용구가 아니다. 회복 피드백은 unit 1 의 바 하강과 unit 3 의 림 완화가
담당한다(둘 다 «지속» 어휘라 분당 수십 킬에 묻히지 않는다).

## 완료 기준

- [ ] 컴파일 0 에러 · 콘솔 에러 0
- [ ] `GoalTowerArchetypeTests` + `StructureFixtures` 동기화 (소비처 3파일 포함) — 초록
- [ ] EditMode 전체 완주, 신규 실패 0건
- [ ] EditMode: 킬 1회 → 마음 `Health` 가 `awakeningReward × 배율` 만큼 오른다
- [ ] EditMode: 만피에서 킬 → `Health.max` 를 넘지 않는다(clamp 확인)
- [ ] EditMode: 「살찌운 제물」 표식 적을 잡아도 **회복량은 원값 기준**이다
- [ ] Play: 잡을수록 바가 내려간다. 마음에서 힐 이펙트가 연발하지 않는다
