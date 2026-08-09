# unit 1 — 저작 타겟 마스크 (EnemyTargetFilter.factionMask)

## 목적

적이 «무엇을 노리는 놈인가» 를 **SO 저작**으로 만든다. 지금은 브리지가 `AttackState.targetMask` 에 리터럴을 굽고 있어서 모든 적이 같은 것을 노린다 — 「어떤 적은 거점만 때린다」를 표현할 수단이 없다.

계약 2(저작 의도 / 런타임 마스크 2분)의 저작 쪽을 세우고, unit 0 에서 폐기된 계약 4가 넘긴 «거점 우선이냐» 도 이 축이 받는다.

**행동 변화 0.** 기존 적 12종은 전부 현행과 동치로 굽힌다.

## 변경 대상

- `Assets/_Project/Scripts/Data/AttackUnitData.cs` — 저작 필드 신설
- `Assets/_Project/Scripts/Battle/Combat/EnemyTargetFilter.cs` — `factionMask` 추가
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 베이크(`EnemyTargetFilter`) + `AttackState.targetMask` 를 리터럴에서 저작값으로 전환
- `Assets/_Project/Tests/EditMode/` — 폴백·베이크·거점전담 테스트

시트는 건드리지 않는다 — 부류는 스탯이 아니라 정체성이다(README 결정 5). `UnitStatImportDto` 무변경.

## 구현

### 1. 저작 필드 — `Faction` 을 직접 쓴다

```csharp
// AttackUnitData
[Tooltip("이 적이 노리는 대상(진영 × 종류). 비우면 현행 기본값으로 폴백.")]
public Faction targetFactions = Faction.DefenderUnit | Faction.BlockingHazard | Faction.DefenderCore;
```

**미러 enum 을 만들지 않는다.** `Wassup.Runtime.asmdef` 하나가 Data·Battle 을 같이 덮으므로 참조가 가능하고, 미러는 이 스펙이 계속 상대해 온 drift 의 원천이다(테스트↔브리지 아키타입, 골 두 벌, 기계 4쌍이 전부 같은 병이다). 진영 비트의 정의는 한 곳에만 있어야 한다.

### 2. `EnemyTargetFilter.factionMask`

```csharp
public struct EnemyTargetFilter : IComponentData
{
    public int classMask;      // DefenderClass 비트 (기존)
    public int priorityClass;  // (기존)
    public int factionMask;    // 신설 — 저작 의도. 전투 중 불변.
}
```

여기에 두는 이유: 이 컴포넌트는 **무기 없는 적에게도 무조건 부착**된다(`BattleBridge:7484`, `wantsAttack` 게이트 밖). 러너·스위프트처럼 `AttackState` 가 아예 없는 적도 저작 의도를 갖는다 — 계약 2가 «런타임 마스크를 게이트로 쓰면 무기 없는 적이 도발 불가가 된다» 고 한 함정을 구조로 막는 자리다. unit 2의 도발 게이트가 이 필드를 읽는다.

### 3. 미저작 폴백 — 마이그레이션이 아니라 «빈 값» 방어선이다

초판은 여기에 «Unity 가 새 enum 필드를 기존 에셋에 0 으로 로드하므로 폴백이 없으면 적 12종이 무장 해제된다» 고 적었다. **실측으로 틀린 것이 확인됐다**(2026-08-09):

- `targetFactions` 키는 기존 12 에셋 YAML 에 **없다**(재직렬화 전).
- 그런데도 런타임 값은 `None` 이 **아니다** — 미저작 분기가 한 번도 타지 않았다(`AllEnemyAssets_ResolveToNonZeroMask` 의 진단 로그 무발생).
- 즉 ScriptableObject 는 관리 객체 생성 시 **필드 이니셜라이저가 돌고**, YAML 에 존재하는 키만 그 위에 덮인다. 없는 키는 이니셜라이저 값을 유지한다.

→ **행동 변화 0 은 폴백이 아니라 필드 이니셜라이저가 보장한다.** 마이그레이션은 필요 없다.

폴백은 그래도 남긴다. 이유가 다르다: `Faction.None`(0)은 **인스펙터에서 표현 가능한 값**이고, 저작자가 마스크를 비우면 그 적은 아무것도 못 때리는 유령이 된다. `Resolve` 는 그 상태를 «미저작 = 현행» 으로 읽어 조용한 무장 해제를 막는다.

⚠ 그래서 «무엇도 노리지 않는 적» 은 지금 **표현할 수 없다**. 필요해지면 0 을 그 의미로 되돌리는 게 아니라 별도 신호(예: `attackMethod == None` 과의 조합)를 쓴다 — 0 은 이미 «미저작» 으로 예약됐다.

### 4. `AttackState.targetMask` 를 저작값에서 굽는다

`BattleBridge:7454` 의 리터럴 `(int)(Faction.DefenderUnit | Faction.BlockingHazard | Faction.DefenderCore)` 를 폴백 적용된 저작값으로 교체한다. 저작이 현행 기본값이면 비트가 같아 **행동 변화 0**.

**«순수 derive 함수» 를 만들지 않는다.** README 작업 단위 표의 «순수 derive» 표현을 문자 그대로 이행하면 지금은 **항등 함수**가 된다(저작 의도 → 런타임 초기 마스크가 그대로). 호출처 하나뿐인 항등을 함수로 빼는 것은 제약 8·10 이 금지하는 과잉 추상화다. 실제로 마스크를 변형하는 로직은 이미 `TauntAttackGrantSystem`(도발 시 `DefenderUnit` OR / `previousTargetMask` 원복)에 있고, 그것이 unit 2 의 영역이다. 폴백 판정(`0 → 기본값`)만 한 줄로 둔다.

### 5. 거점 전담 적이 표현 가능해지는가 (이 unit 의 검증 질문)

저작만으로 «마스크 = `DefenderCore` 단독» 인 적이 만들어져야 한다. 그 적은:
- 방어유닛을 후보로 잡지 않는다(마스크 필터, `AttackSystem:516`)
- 유인으로 막을 수 없다 — 도발이 유닛 비트를 OR 해줘야 하고, 그 게이트는 unit 2 가 저작 의도로 판정한다
- 최후순위 규칙에 걸리지 않는다 — 그 규칙은 unit 0 에서 폐기됐다. 후보가 거점뿐이면 거리순이 곧 거점이다

## 완료 기준

- [x] 컴파일 에러 0 · 콘솔 신규 에러 0
- [x] EditMode 전량 그린 — **2010개 / 실패 0 / 의도적 스킵 3**(기준선 2005 + 신규 5)
- [x] **행동 변화 0 실증** — `AllEnemyAssets_ResolveToNonZeroMask` 가 12 에셋 전부의 해석 결과가 0 이 아님을 단정. 실측으로 무회귀의 근거가 폴백이 아니라 **필드 이니셜라이저**임이 확인됐고(§3 정정), 테스트는 두 경로 어느 쪽이든 무장 해제를 잡는다
- [x] 저작값이 그대로 흐른다 — `Resolve_Authored_IsRespectedVerbatim`. 베이크 지점은 `AttackState.targetMask` 와 `EnemyTargetFilter.factionMask` 가 **같은 지역 변수**를 쓰므로 구조적으로 갈릴 수 없다
- [x] 거점 전담 마스크 적이 사거리 내(더 가까운) 방어유닛을 때리지 않는다 — `StructureOnlyMask_Enemy_IgnoresDefenderUnitInRange`
- [~] 무기 없는 적도 `factionMask` 를 갖는다 — 부착이 `wantsAttack` 게이트 **밖**임을 코드로 확인(`BattleBridge` `EnemyTargetFilter` 부착 지점). 브리지 베이크 경로 테스트는 `SpawnUnit` 리플렉션(PlayMode)이 필요해 미작성 — unit 2 의 도발 게이트 테스트가 이 전제를 실사용으로 덮는다
- [ ] 리뷰: 일반 리뷰(Data/Bridge 변경 — ECS 시뮬 로직 변경 없음)

---

**확인 2026-08-09** — 구현 커밋: (아래 커밋 해시)

**초판에서 정정된 것**: §3 의 «Unity 가 신규 enum 필드를 0 으로 로드한다» 전제가 **실측으로 틀렸다.** 무회귀는 폴백이 아니라 필드 이니셜라이저가 보장하며, 폴백의 역할은 «인스펙터에서 비운 마스크» 방어선으로 재정의됐다. `Faction.None` 은 이제 «미저작» 으로 예약됐으므로 «무엇도 노리지 않는 적» 은 별도 신호가 필요하다.

## 주의

- `classMask`(DefenderClass) 와 `factionMask`(진영×종류)는 **다른 축**이다. 전자는 «어느 직업의 방어유닛», 후자는 «어느 진영의 무슨 종류». 둘을 합치지 않는다 — 거점에는 `DefenderClassTag` 가 없어 `classMask` 가 애초에 적용되지 않는다(`EnemyTargetFilter` 주석의 «태그 없는 후보는 필터되지 않는다» 규약).
- `ProjectileTargetFaction` 은 여전히 별 축이다(계약 11). 본능의 탄이 무엇을 맞히는지는 unit 5 에서 이 저작 마스크와 대조한다.
