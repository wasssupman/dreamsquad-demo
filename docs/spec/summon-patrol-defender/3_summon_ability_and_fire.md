# unit 3 — 소환 능력 에셋 + 소환 발화(구역 게이트)

## 목적

"어떤 순찰병을 · 어떤 거점 반경으로 유지하나"를 데이터로 선언하고, 소환사가 쿨다운마다 **적 유무 무관하게** 소환하게 한다. 이 unit 이 끝나면 소환수가 실제로 나온다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Data/Abilities/SummonPatrolAbility.cs`
- 신규 `Assets/_Project/Scripts/Battle/Combat/SummonerState.cs`
- 신규 `Assets/_Project/Scripts/Battle/Combat/PatrolSpawnRequest.cs`
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — 소환 발화 분기(구역 게이트)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — bake · 레지스트리 · 드레인 · `DestroyBattleEntities`

## 구현

### ① `SummonPatrolAbility : DefenderAbilityData`

5번째 구체 능력. base 는 구체 4종 실존으로 이미 제약 8 을 통과한 상태이고(`defender-ability-assets` 계약 7), 이 spec 은 그 확장 지점을 그대로 쓴다.

```csharp
public DefenderUnitData patrolUnit;   // 소환할 순찰병 (DefenderCatalog 미등록 에셋)
public int   leashTileRadius = 2;       // 거점 박스 반경 (Chebyshev)
```

**쿨다운 필드를 두지 않는다.** 소환 주기는 소환사 SO 의 `attackCooldown` 이다 — "소환 = 공격"을 심에서도 유지하면 공격 애니·공격 SFX·`UnitAttackVisualEvent` 가 전부 공짜로 붙는다. 폭탄맨이 `AttackState.cooldownRemaining` 을 그대로 쓰는 것과 같다.

### ② `SummonerState` (Combat 소유) + bake

```csharp
struct SummonerState : IComponentData
{
    public int    patrolDataIndex;  // Bridge 측 순찰병 SO 레지스트리 인덱스
    public int    leashTileRadius;
    public Entity current;            // 살아있는 순찰병. Entity.Null = 없음
}
```

`DefenderUnitData` 는 managed 라 컴포넌트에 못 담는다 — `RegisterZoneHazardSO` / `GetOrCreateProjectileDataIndex` 와 같은 **인덱스 등록** 관용구를 따라 `RegisterPatrolUnitSO` 를 Bridge 에 추가한다.

bake: `CreateDefenderEntity` 에서 `GetAbility<SummonPatrolAbility>()` 가 non-null 이고 `patrolUnit != null` 이면 `SummonerState` 부착(`current = Entity.Null`). 기존 능력 4종의 bake 분기와 같은 자리·같은 형태.

### ③ 소환 발화 분기 — 구역 게이트

폭탄맨과 같은 자리 — `AttackSystem` 공격자 루프에서 **타겟 선정보다 앞**이다. 타겟을 요구하는 RESOLVE 에 두면 적이 사거리에 들어오기 전까지 순찰병이 안 나와서 "거점을 지킨다"가 성립하지 않는다.

```
if (SummonerState 보유):
    쿨다운 진행 → 만료 아니면 continue
    current 가 유효(Exists && !DeadTag && HP>0) → 소환 스킵
    gateOpen = hasSummonedOnce
             || 소환사 셀 ± leashTileRadius 안에 PastGoal 아닌 적이 있음
    gateOpen 이면 → 요청 캐리어 stage + 쿨다운 리셋
    아니면        → 쿨다운을 **리셋하지 않고** 만료 상태로 대기(즉시 반응)
    continue                                    // 일반 타겟팅/공격 경로를 타지 않는다
```

anchor 스냅은 Bridge 의 `TryGetNearestWalkCell` 이 소유하므로 심에서는 소환사 셀만 실어 보내고 **스냅은 드레인 쪽에서 한다**.

### ④ 요청 캐리어

신규 NativeQueue 채널을 만들지 않는다. `ProjectileRequestCarrier` 와 같은 **전용 캐리어 엔티티** 관용구를 쓴다 — AttackSystem 에서 Bridge 스폰을 요청하는 관용구가 이미 그 자리에 있고, 싱글턴 배선도 CLAUDE.md 채널 목록(27개) 갱신도 불요하다.

```csharp
struct PatrolSpawnRequest : IComponentData
{
    public Entity owner;
    public int2   ownerCell;      // Bridge 가 walk 셀로 스냅한다
    public int    patrolDataIndex;
    public int    leashTileRadius;
}
struct PatrolRequestCarrier : IComponentData { }   // 드레인이 통째로 파괴
```

Bridge 드레인: 매 프레임 캐리어를 훑어 `TryGetNearestWalkCell` → `CreatePatrolEntity` → 성공 시 `SummonerState.current` 기록 → 캐리어 파괴. 스냅 실패 시 소환 취소(요청 폐기). `DestroyBattleEntities` 에 `DestroyEntitiesByType<PatrolRequestCarrier>()` 추가(드레인 사이에 전투가 멈춘 낙오분 회수 — 투사체 캐리어가 같은 이유로 등재돼 있다).

**한 프레임 지연이 생기지만 중복 소환은 불가능하다** — 요청을 stage 한 프레임에 쿨다운이 이미 리셋되므로 다음 발화까지 최소 1쿨이 있다.

## 완료 기준

- [ ] 컴파일 통과 · 기존 EditMode 스위트 전량 통과 (폭탄맨·머신거너·일반 공격 무회귀)
- [ ] `SummonPatrolAbility` 서브에셋을 인스펙터에서 생성·할당할 수 있다
- [ ] 능력 미부착 유닛에 `SummonerState` 가 붙지 않는다
- [ ] Play: 소환사를 배치하면 **적이 하나도 없어도** 쿨다운 후 순찰병이 나온다
- [ ] 순찰병이 살아 있는 동안 추가 소환이 없다 (1기 고정)
- [ ] 순찰병 스폰 셀이 walk 타일이다 (배치 타일 아님)
- [ ] 주변에 walk 셀이 없는 곳에 소환사를 배치하면 소환이 조용히 취소된다 (에러 없음)
- [ ] 소환 순간 공격 애니/SFX 가 재생된다 (`AttackState` 재사용의 귀결)
- [ ] 전투 종료 후 재진입 시 캐리어·순찰병 잔존 없음
- [ ] 콘솔 에러/경고 0

---

**완료 기준 확인**: 2026-08-03 · 커밋 `68d2f35c` + 게이트 보정 `83ab214b` · `PatrolSystemIntegrationTests` 8건(`Summoner_Stages_One_Request_When_No_Patrol_Alive` · `Summoner_Does_Not_Stage_While_Patrol_Alive` · `Summoner_Waits_While_Cooldown_Remains` · `First_Summon_*` 4건 · `Respawn_Ignores_The_Gate_Once_Consumed`)이 발화·1기 고정·초회 구역 게이트를 EditMode 로 고정한다.
⚠ **위 «적이 하나도 없어도 쿨다운 후 순찰병이 나온다» 는 무효다.** 초기 구현은 폭탄맨식 blind 발화였고, 실플레이 확인 중 `83ab214b` 이 **«첫 소환만 구역 게이트»** 로 뒤집었다(README 계약 8).
(체크박스 소급 기록.)
