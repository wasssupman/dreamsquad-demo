# 3. Unit Assets And Spine

## 목적

신규 hazard caster defender 4종을 실제 asset 으로 만들고, 현재 사용 가능한 Spine skeleton/skin/animation 을 연결한다.

## 변경 대상

- Add: `Assets/_Project/Data/Defenders/Defender_FireCaster.asset`
- Add: `Assets/_Project/Data/Defenders/Defender_IceCaster.asset`
- Add: `Assets/_Project/Data/Defenders/Defender_PoisonCaster.asset`
- Add: `Assets/_Project/Data/Defenders/Defender_BlockingCaster.asset`
- Modify: draft/defender pool asset or scene reference that feeds `BattleBridge.defenderPool`

## 구현

Spine 은 기존 `SpineUnitPool` / `SpineUnitView` 경로만 사용한다. 신규 view class 를 만들지 않는다.

Skin 임시 배정:

| Unit | Skin |
|---|---|
| Fire caster defender | `Lamb` |
| Ice caster defender | `Owl` |
| Poison caster defender | `Goat` |
| Blocking caster defender | `Owl` |

animation 은 SkeletonDataAsset 에 존재하는 이름만 사용한다. 후보는 다음 순서로 찾는다.

- idle: `idle`, `walk`
- cast/attack: `attack`, `attack-1`, `attack-2`
- death: `die`, `death`, empty fallback

밸런스 초깃값:

- health: 기존 ranged defender 와 비슷한 35~50
- **attackRange: 0** — `AttackSystem` 이 target 을 찾지 않도록 0으로 설정한다. `CreateDefenderEntity` 는 항상 `AttackState` 를 부착하므로, 0이 아닌 값을 두면 AttackSystem 이 매 cooldown 마다 target 을 찾고 spurious `UnitAttackVisualEvent` 를 enqueue 해 Spine attack animation 이 오발된다.
- attackDamage: 0
- outputs: empty
- hazardCastRange: 4~5 tiles
- hazardCastCooldown: 3~5 sec

## 완료 기준

- 4종 defender asset 이 생성되고 `DefenderUnitData` 로 로드된다.
- `attackRange = 0` 이다. PlayMode 에서 spurious Spine attack animation 이 발생하지 않는다.
- Spine skin/animation missing warning 이 없다.
- draft/defender pool 에서 4종을 선택/배치할 수 있다.
- 일반 projectile/AttackOutput 공격 없이 hazard cast action 만 수행한다.
