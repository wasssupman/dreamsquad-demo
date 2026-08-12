# unit 4 — owner 연쇄 소멸 + 재소환 순환

## 목적

요구사항 4·5 를 닫는다 — 순찰병이 죽으면 최대 1쿨 안에 다시 나오고, 소환사가 죽으면 순찰병도 동시에 사라진다. 이 unit 이 끝나면 계층 B 가 완성된다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Battle/Units/PatrolLifecycleSystem.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.Relocation.cs` — anchor 재스냅

## 구현

### ① `SummonedBy` (Units 소유) — **구조체는 unit 2 에서 이미 만들었다**

`CreatePatrolEntity` 가 `owner != Entity.Null` 일 때 부착하므로 구조체 정의가 unit 2 로 앞당겨졌다. 이 unit 은 **소비 시스템만** 추가한다.

수명 링크다. Units 가 죽음(`DeadTag`·`HealthDeathSystem`)을 소유하므로 여기 둔다. `PatrolAnchor`(Movement, 이동 제약)와 **맥락이 다르다** — 이것이 두 컴포넌트를 나누는 오늘의 근거다(미래 확장이 아니다).

```csharp
struct SummonedBy : IComponentData { public Entity owner; }
```

### ② `PatrolLifecycleSystem` (Units)

`SummonedBy` 보유 엔티티마다 owner 생존을 판정하고, 죽었으면 `DeadTag` 를 붙인다. 파괴는 기존 `UnitLifecycleSystem` 의 general dead loop 가 처리한다(`DeadTag` + `WithNone<DefenderTile, BlockingHazard>` — 순찰병은 `DefenderTile` 이 없어서 여기로 떨어진다).

### ③ 생존 술어를 양방향 대칭으로 (계약 8)

```
유효(e) = Exists(e) && !DeadTag(e) && Health(e).value > 0
```

- `SummonedBy.owner` 에 적용 → 소환사 사망 시 순찰병 소멸
- `SummonerState.current` 에 적용 → 순찰병 사망 시 재소환 (unit 3 의 스킵 조건)

**한쪽만 검사하면 실패한다.** `current != Entity.Null` 만 보면 파괴된 순찰병의 stale 핸들이 남아 소환사가 영구 대기한다. `Entity` 는 version 을 포함하므로 `Exists` 가 재활용 id 를 막는다 — `shield-guardian-defender` 계약 4 가 같은 논거를 쓴다. `AggroStateSystem` 의 링크 가디언 사망 3중 판정이 선례다(ECB 파괴분 + death-프레임 `DeadTag` + `HP<=0`).

### ④ 소환사 재배치 시 anchor 재스냅

`BattleBridge.Relocation` 이 `DefenderTile` 을 from→to 로 스왑하는 확정 프레임에서, 그 소환사의 `SummonerState.current` 가 유효하면 순찰병의 `PatrolAnchor.cell` 을 **to 셀의 최근접 walk 셀**로 다시 스냅한다. 재스냅 실패 시 anchor 를 유지한다(순찰병을 죽이지 않는다).

## 완료 기준

- [ ] 컴파일 통과 · 기존 EditMode 스위트 전량 통과
- [ ] Play: 순찰병을 강제 사망시키면 **≤1쿨 안에 재소환된다** (반복 3회 이상 안정)
- [ ] Play: 소환사를 강제 사망시키면 순찰병이 **같은 프레임 대에 사라진다** (뷰도 회수)
- [ ] 소환사 사망 후 유령 순찰병이 남지 않는다 (Entity Debugger 확인)
- [ ] 소환사 재배치 후 순찰병의 거점이 새 위치로 따라간다
- [ ] 재배치 목적지 주변에 walk 셀이 없으면 순찰병이 죽지 않고 기존 거점을 유지한다
- [ ] 소환사 2기를 동시 배치하면 각자 순찰병 1기씩 유지한다 (링크 혼선 없음)
- [ ] 콘솔 에러/경고 0

---

**완료 기준 확인**: 2026-08-03 · 커밋 `68d2f35c` · `PatrolSystemIntegrationTests` 가 양방향 생존 술어(계약 9)를 4건으로 고정하고(`Patrol_Survives_While_Owner_Is_Alive` · `Patrol_Dies_When_Owner_Gets_DeadTag` · `..._Owner_Entity_Is_Destroyed` · `..._Owner_Health_Hits_Zero`), 재소환 순환을 2건으로 고정한다(`Summoner_Restages_When_Current_Handle_Is_Stale` · `..._Current_Is_Dead_But_Not_Destroyed`).
**재배치 anchor 재스냅은 육안에서 PlayMode 로 승격됐다** — unit 9(`023b4d4e`)가 `TryBeginDefenderRelocation` 실경로로 「중심 = 새 소환사 셀 · 집이 새 구역 안」을 고정한다.
(체크박스 소급 기록.)
