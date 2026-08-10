# unit 3 — 거점 저작 (StructureData + MapDocument.structures)

## 목적

거점(마음·본능)을 **맵 저작물**로 만든다. 페인터에서 칸을 찍고 SO 를 물리면 문서에 남고, 빌드가 그것을 판으로 옮긴다. 스폰·전투는 unit 4·5 이고, 이 유닛은 **저작 → 직렬화 → 왕복 → 검증**까지다.

**행동 변화 0** — 저작된 거점이 없으면 모든 경로가 빈 배열이다.

## 변경 대상

- `Assets/_Project/Scripts/Data/StructureData.cs` — 신설 SO
- `Assets/_Project/Scripts/Data/StructurePlacement.cs` — 신설(런타임 unmanaged 표현 + 진영 파생)
- `Assets/_Project/Scripts/Data/MapGrid/MapDocument.cs` — `structures[]` 직렬화 + 검증
- `Assets/_Project/Scripts/Data/MapGrid/MapDocumentBuilder.cs` — 왕복(`ToGeneratedMap` / `WriteToDocument`)
- `Assets/_Project/Scripts/Data/GeneratedMap.cs` — `structures` 배열
- `Assets/_Project/Scripts/Data/MapConnectivity.cs` — `spawns.Length` 하한 2 → 1
- `Assets/_Project/Editor/MapPainterWindow.cs` — 거점 브러시 + 검증 + 모드 배지
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 고른 문서를 필드로 보관(`_resolvedDeck` 대칭)

## 구현

### 1. `StructureData` SO — 종류와 스탯

```csharp
public enum StructureKind : byte { Core, Instinct }

// StructureData
public StructureKind kind = StructureKind.Core;
public float health = 1000f;
public GameObject viewPrefab;          // KayKit 프랍. unit 4 가 소비
// 본능 공격(unit 5 가 소비). 마음은 공격하지 않으므로 무시된다.
public Faction targetFactions = Faction.DefenderUnit;
public float attackRange, attackCooldown, attackDamage;
public ProjectileData projectile;
```

**진영은 SO 에 두지 않는다.** 방어 본능과 적 본능이 같은 스탯일 수 있어 SO 를 두 벌 만들게 되고, «진영이 다른 같은 거점» 이 데이터 중복이 된다. 진영은 **배치가** 정한다(아래).

### 2. 배치 = 칸 × 편 × SO, 진영은 파생

```csharp
// MapDocument 직렬화 엔트리
[Serializable] public struct StructureEntry
{
    public Vector2Int cell;
    public StructureSide side;   // Defender · Enemy · Neutral
    public StructureData data;
}
```

`Faction` 은 **(편 × 종류)에서 파생**한다 — 순수 함수:

```csharp
StructurePlacement.DeriveFaction(StructureSide side, StructureKind kind)
// (Defender, Core)     → Faction.DefenderCore
// (Enemy,   Instinct)  → Faction.EnemyInstinct  ...
```

저작에 `Faction` 을 직접 쓰지 않는 이유: `DefenderUnit` 처럼 **거점이 아닌 비트**를 찍을 수 있게 되고, 그건 표현되면 안 되는 상태다. 편·종류 두 축만 저작하면 잘못된 조합이 애초에 만들어지지 않는다(모드 enum 을 기각한 것과 같은 판단 — README §모드 판정).

### 3. `GeneratedMap.structures` — 셀 + 진영 두 값이면 충분하다

```csharp
public struct StructurePlacement { public int2 cell; public Faction faction; }
public NativeArray<StructurePlacement> structures;   // GeneratedMap
```

**종류를 따로 싣지 않는다** — 교차 비트가 이미 종류를 인코딩한다(`DefenderCore` vs `DefenderInstinct`). footprint 도 거기서 파생한다(v1: 마음 1×1 · 본능 3×3). 1축 교차 비트 결정이 여기서 값을 돌려받는 자리다.

스탯(체력·프랍·공격)은 SO 에 남고 **브리지가 문서에서 읽는다**(unit 4). `GeneratedMap` 은 unmanaged 라 SO 참조를 실을 수 없고, 실을 필요도 없다 — 마스크 파생·연결성·모드 판정은 셀과 진영만 본다.

왜 unit 3 에서 `GeneratedMap` 을 건드리나: 페인터의 Bake 가 `GeneratedMap` 을 조립해 `WriteToDocument` 로 내보내는 경로라, 이 배열이 없으면 **저작한 거점이 문서에 안 남는다.** 「나중을 위한」 확장이 아니라 이 유닛의 왕복에 필요하다.

### 4. 브리지가 고른 문서를 보관한다

`BuildMapForBattle` 의 `activeDoc` 은 지역 변수다 — 빌드가 끝나면 사라져 unit 4 가 SO 스탯을 읽을 방법이 없다. `_resolvedDeck`(풀에서 고른 덱을 필드로 보관)과 **같은 형태**로 `_resolvedMapDoc` 을 둔다. 소비는 unit 4.

### 5. 연결성 하한 완화

`MapConnectivity.AllSpawnsReachGoal` 이 `spawns.Length < 2` 면 무조건 false 다. 공성 맵은 적 마음 1개 = 스폰 1개라 통과할 수 없다. **하한을 1 로** 내린다. 침략 맵은 실측상 전부 2개 이상이라 영향 없다.

### 6. 페인터 — 브러시 + 검증 + 파생 배지

- **브러시**: `Tool.Structure` 추가. 편 토글(방어/적) + `StructureData` 오브젝트 필드. 클릭 = 배치/제거(스폰·골 브러시가 click-only 인 것과 같은 이유 — 드래그 재토글 깜빡임).
- **모드 배지**: 적 마음 개수에서 **파생**한다. 0 = 침략 · 1 = 공성 · 2+ = 에러. 드롭다운이 아니다(README §모드 판정).
- **검증**(README 표):

| 저작 상태 | 판정 | 에러 조건 |
|---|---|---|
| 적 마음 0 | 침략 | `spawns[]` 1개 이상 |
| 적 마음 1 | 공성 | 방어 마음도 정확히 1 (멀티골 금지) · `spawns[]` 저작은 에러(파생이 채운다) |
| 적 마음 2+ | 에러 | 공성 맵의 마음은 진영당 1개 |

- 3×3 본능이 격자 밖으로 나가거나 서로 겹치면 에러.

## 완료 기준

- [x] 컴파일 에러 0 · 콘솔 신규 에러 0
- [x] EditMode 전량 그린 — **2026개 / 실패 0 / 의도적 스킵 3**(기준선 2013 + 신규 13)
- [x] **행동 변화 0** — `NoStructures_ProjectsEmpty_CurrentMapsUnchanged`. 미저작도 배열은 생성되고 길이 0
- [x] 진영 파생 — 6조합 정확 + `DeriveFaction_NeverProducesNonStructureBits`(유닛·방벽 비트가 섞일 수 없음)
- [x] 왕복 보존 — `WriteToDocument_PassesStructureEntriesThrough`(SO 참조 보존) + `WithoutStructures_LeavesAuthoringUntouched`(기존 호출자 무회귀)
- [x] `AllSpawnsReachGoal` 이 스폰 1개 맵을 통과 — `AcceptsSingleSpawn`. 기존 다중 스폰 테스트 무회귀
- [x] 모드 판정 3케이스(적 마음 0/1/2+) + 공성 규칙(멀티골 금지·spawns 저작 금지)이 표대로 — 순수 함수 직접 호출
- [~] 페인터 실제 저작 — 창은 메뉴로 열려 **예외 없음**을 확인했으나, 에디터 비포커스라 `OnGUI` 리페인트(배지·툴바 레이아웃)는 미검증. 스펙 종료 시점 검증으로 유보
- [x] 리뷰: 투트랙(code-reviewer + ecs-reviewer) 완료 2026-08-09. 반영:
  - **M-a(양측)**: 거점 규칙 3개((Defender,Core) 금지·경계·겹침)를 페인터에서 `StructureAuthoringRules.ValidateStructures` 로 이관 — 페인터와 `MapDocument.OnValidate` 가 같은 함수를 부른다. 인스펙터 우회 저작이 이제 import 에서 잡힌다. 규칙 테스트 5개 추가
  - **M-b(양측)**: `OnValidate` 의 무조건 `spawns<1` 에러를 `ValidateMode`(모드별 규칙)로 교체 — 공성 문서(스폰 0)가 import 에러를 뱉던 자기모순 해소. ⚠ **런타임은 여전히 공성 문서를 못 돌린다** — `MapConnectivity` 가 spawns 0 에 false 인 것은 의도이고, unit 6 의 파생(적 마음 → `spawns[]`)이 서야 풀린다. 이것이 unit 6 의 전제다
  - M-c: stale 주석 정정(`Faction.cs` 그룹 상수 소비처 · 테스트 단정 메시지)

---

**확인 2026-08-09** — 구현 커밋: (아래 커밋 해시)

**unit 4 로 이관된 리뷰 지적** (M-d·M-e — 코드 무변경, 착수 시 처리):
- M-d: 아키타입 고정(`GoalTowerArchetypeTests`)이 속성 나열식이라 완전성 단정이 없고, 합성 픽스처 2개(`GoalProjectileTests.MakeGoal`·`GoalTargetingPriorityTests.CreateGoal`)는 브리지와 구조적 연결이 없다 — unit 4 가 타워 아키타입을 바꿀 때 **공용 픽스처 빌더**로 접을 것
- M-e: `_goalGaugeList` 는 writer 0(게이지 폴링 도달 불가, 라이브 바는 `SyncGoalStabilityBars` 가 그림) · `GoalCollapsedEventsSingleton` 페이로드(`cell`·`goalIndex`)는 골 인덱스 기준 — unit 4 의 거점 게이지·붕괴 채널 재설계에서 재용도/삭제를 명시 결정
- (기각 1건: «`AnyUnit` 게이트가 과대» — 도발이 `TauntAttackGrantSystem` 에서 `DefenderUnit` 비트를 OR 하므로 «게이트 통과 후 가디언 타격 불가» 시나리오는 불성립. «`EnemyUnit` 만 노리는 적이 도발*되어야 하는가*» 는 설계 질문으로 후속 후보)

**설계 결정 1건 (README 보다 좁힘)**: **방어 마음은 `goals[]` 로 계속 저작한다.** `structures[]` 는 본능 + 적 마음만 받고, `(Defender, Core)` 조합은 페인터가 에러로 막는다. 현행 9장이 전부 `goals[]` 를 쓰고 라이브 타워가 이미 `DefenderCore` 라, 옮기면 «콘텐츠 이관 0» 이 깨지고 **골이 또 두 벌**이 된다 — 이 스펙이 상대해 온 바로 그 병이다. unit 4 의 스폰은 두 소스를 각각 읽되 같은 아키타입을 만든다.

## 주의

- 마음은 **통행을 막지 않는다**(계약 12). 본능 3×3 본체만 막고, 그 차단·배치 배제 파생은 unit 4 다.
- `StructureData.targetFactions` 는 unit 1 의 `AttackUnitData.targetFactions` 와 같은 축·같은 의미다. 본능이 «유닛과 같은 파이프라인» 을 타는 근거(계약 10)이고 unit 5 가 소비한다.
- 거점 체력의 정본은 SO 다. 라이브 마음(현행 골 타워)이 덱(`AttackDeck.goalStabilityMax`)에서 받는 것을 SO 로 옮기는 이관은 **unit 4** 사안이다(리뷰 F5).
