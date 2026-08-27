# 6 — 소환 (`SummonPatrolAbility`)

## 목적

전용 상태 + 전용 시스템 3개로 자란 **다섯 번째 구성원**을 접는다.
`on-place-skill-rework` 계약 2 가 「캐스트 4종」이라 적은 것은 소환사가 그 뒤에 생겨서다.

## 변경 대상

- `Assets/_Project/Data/Abilities/Ability_SummonPatrol_Summoner.asset`
- `Assets/_Project/Scripts/Data/Abilities/SummonPatrolAbility.cs`
- `Assets/_Project/Scripts/Battle/Combat/SummonerState.cs` · `PatrolSpawnRequest.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — bake(`:3939`·`:7940`) · 스폰 드레인
- (잔존) `Battle/Units/PatrolLifecycleSystem.cs` · `Battle/Effects/PatrolFieldSystem.cs`

## 구현

1. **스킬은 「소환 개시」까지다.** 「1기 유지」·순찰·수명은 진행형 상태이므로
   `SummonerState` 와 `PatrolLifecycleSystem`·`PatrolFieldSystem` 에 남는다(토대 계약 5).
2. **`SpawnUnitIntent` 가 필요하다.** 소환물 스폰은 managed 쪽(브리지 드레인)이 하므로
   디스패처가 managed 인 것이 오히려 유리하다. 새 intent 를 열 때 unit 0 의 의도 표를 갱신한다.
3. **`PatrolSpawnRequest` 캐리어에 `SimEntityId` 가 없다** — 토대 unit 2a 의 「요청 캐리어
   미부착」 목록에 있다. 스킬이 캐리어를 참조하지 않는 형태로 설계하면 부착이 불필요하다.
4. **다중 순찰병은 하지 않는다** — `SummonerState.current` 를 버퍼로 바꾸는 것은 콘텐츠 결정
   (2026-08-03 사용자 결정으로 1기 고정)이고 이 spec 범위 밖이다.

## 완료 기준

- [ ] 소환 개시가 concrete + 저작 SO 로 존재한다
- [ ] `SummonerState`·`PatrolLifecycleSystem`·`PatrolFieldSystem` 이 **남아 있다**
- [ ] 1기 고정 규칙이 그대로다 (다중 소환 안 됨)
- [ ] 순찰병이 소환되고 이동·교전한다 (PlayMode 단언 — 「멈추는데 못 때림」 회귀 방지)
- [ ] 그물 초록


---

## 재평가 (2026-08-26) — **소환 개시는 기본공격이다**

unit 5 에서 사용자가 그은 경계선을 적용한 결과다:

> 「발사 명세를 트리거한다」만으로는 스킬이 아니다. 그 위에 **조건**이 얹힐 때 스킬이 된다.

소환 arm 의 발동 조건은 **쿨다운이 차면**이고(`attack.cooldownRemaining <= 0`), 유닛의
공격 사양(`attack.range`·`targetMask`·`targetTraversalLayers`)을 그대로 쓰며 평타 분기를
대체한다. 코드 주석이 이미 두 번 그렇게 적고 있다 —
「소환사는 «사거리에 적이 들면 공격(=소환)»하는 **평범한 유닛**」·「소환 = 이 유닛의 **공격 사건**」.

「1기 유지」는 조건이 아니라 **자원 제약**이고, 초회 게이트(「담당 구역에 적이 있나」)는
모든 공격이 갖는 **대상 조건**이다.

⚠ 그리고 이 가족엔 옮겨서 없어지는 arm 사본이 **0** 이다. `patrolDataIndex` 로 일반화돼
있어서 새 소환사를 저작해도 switch 를 안 건드린다 — 이 spec 이 없애려는 병이 여기 없다.

### 이 가족의 **진짜 스킬**은 분열이다

`OnDeath × SplitOnDeath`(슬라임) — 「죽을 때 갈라진다」는 조건이 얹힌 유닛 생성이고,
경계선의 이쪽이다. 오늘은 **의도적 무슬롯**이라(bake 가 슬롯을 안 굽고 브리지 킬 드레인이
SO 를 직독) 라우팅 키를 걸 자리가 없다.

**착수 조건**: `SpawnUnitIntent` 가 필요하고, 그 어휘의 **첫 실사용자**가 분열 하나다.
제약 8(「나중을 위한」 추상 레이어 금지)을 생각하면 하나를 위해 새 sink 를 여는 것이
맞는지가 먼저다 — 그리고 그것도 arm 사본을 없애지 않는다(킬 드레인 한 곳뿐).

**우선순위 판단(2026-08-26)**: unit 7 이 먼저다. 그쪽은 **세 번째 어휘**(`SkillEffectType`)와
**6-arm switch** 를 통째로 죽인다 — 이 spec 이 없애려는 병의 마지막 서식지다.
