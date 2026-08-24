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
