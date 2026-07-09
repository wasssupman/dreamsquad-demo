# 1 — 각성 획득 런타임 (적 reward 베이크 + 이벤트 확장 + bridge 노출)

## 목적

사망 이벤트에 각성 보상량을 실어 bridge 가 Mono 세계로 C# 이벤트로 노출한다. 게이지 상태 자체는 unit 3(컨트롤러)의 것 — 여기서는 **전달 배관만**.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Units/AwakeningReward.cs` (신규 IComponentData)
- `Assets/_Project/Scripts/Battle/Units/EnemyKilledEvent.cs` (필드 append)
- `Assets/_Project/Scripts/Battle/Units/DamageApplicationSystem.cs` (enqueue 지점 1곳)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (적 스폰 베이크 + 드레인 2곳 + C# 이벤트 2개)

## 구현

1. **`AwakeningReward : IComponentData { public int value; }`** — **Units 소유**(사망 이벤트 큐와 동일 맥락). 쓰기 = bridge 스폰 베이크(유일 창구 선례), 읽기 = `DamageApplicationSystem`.
2. **적 스폰 베이크**: bridge 의 기존 적 스폰 경로에서 `AttackUnitData.awakeningReward` 를 `AwakeningReward` 로 부착. 모든 적에 무조건 부착(zero-value 도 부착 — 룩업 분기 단순화).
3. **`EnemyKilledEvent.awakeningReward`** (int, append). `DamageApplicationSystem` 의 기존 enqueue 지점(적 사망 판정)에서 `_awakeningRewardLookup`(RO) 조회해 기입, 컴포넌트 없으면 0.
4. **bridge 드레인 → C# 이벤트** (`WaveMilestoneReached` 선례, 구독자 없어도 무해):
   - `DrainEnemyKilledEvents`: `event Action<int> EnemyKilledAwakening` 발화(evt.awakeningReward).
   - `DrainDefenderDeathEvents`: `event Action<Entity, DefenderUnitData> DefenderDied` 발화 — **binding 제거 전에** `_defenderByTile` 의 `binding.data` 를 캡처(ECS 무변경). reward 는 구독자가 `data.awakeningReward` 로 읽는다. entity 는 unit 3 의 부착 카드 회수 키.

## 경계 확인

- Component 쓰기: `AwakeningReward` 는 bridge 부착(스폰타임 선례) + Units 시스템 읽기 — 맥락 위반 없음.
- 이벤트 struct append-only — 기존 소비자(`scoreHud`) 무영향.

## 완료 기준

- [ ] 컴파일 클린 + 기존 PlayMode 무회귀(적 처치 점수 HUD 정상).
- [ ] Play 중 적 처치 시 `EnemyKilledAwakening` 발화 값이 해당 악몽 SO 의 `awakeningReward` 와 일치 (임시 로그 or unit 3 에서 확인).
- [ ] defender 사망 시 `DefenderDied` 가 entity + data 로 발화.
- [ ] ecs-review 대상 (Battle/ 변경).
