# AttackDeck.SpawnEntry.spawnIndex Migration

**작업 구분**: Phase 10B

## 목적

Q-C 결정 반영: `AttackDeck.SpawnEntry` 의 `string pathId` 필드를 `int spawnIndex` 로 교체. 기존 deck asset (`WaveA.asset`) 도 같이 수정. Multi-spawn 상황에서 deck 이 어느 spawn 에서 적을 뱉을지 명시.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Data/AttackDeck.cs`
- Modify: `Assets/_Project/Scripts/Data/Decks/WaveA.asset`
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (적 스폰 경로 — Phase 9 P9-05B 에서 `SpawnCells[0]` 하드코딩을 `SpawnEntry.spawnIndex` 로 전환)

## 구현

### AttackDeck.SpawnEntry 필드

기존 (실제 `AttackDeck.cs:17-22` 확인됨 — C-7 fix):
```csharp
[Serializable]
public class SpawnEntry
{
    public float triggerTimeSec;
    public AttackUnitData unitType;   // attacker unit — 실제 타입 AttackUnitData (DefenderUnitData 아님)
    public string pathId;              // ex: "A" / "B" — MapData.Paths.id 와 매칭
}
```

신규:
```csharp
[Serializable]
public class SpawnEntry
{
    public float triggerTimeSec;
    public AttackUnitData unitType;   // ← 타입 유지 (원 초안의 DefenderUnitData 는 오류)
    [Tooltip("GeneratedMap.spawns 배열의 인덱스. 범위 벗어나면 index 0 fallback + 경고.")]
    public int spawnIndex;
}
```

### BattleBridge 적 스폰 로직

Phase 9 P9-05B 에서:
```csharp
var spawnCell = map.SpawnCells[0];
var spawnWorldPos = GridToWorldCenter(spawnCell);
```

Phase 10B 로 교체:
```csharp
int idx = entry.spawnIndex;
if (idx < 0 || idx >= _generatedMap.spawns.Length)
{
    Debug.LogWarning($"[BattleBridge] SpawnEntry.spawnIndex={idx} out of range (spawns={_generatedMap.spawns.Length}). Fallback to 0.");
    idx = 0;
}
var spawnCell = new Vector2Int(_generatedMap.spawns[idx].x, _generatedMap.spawns[idx].y);
var spawnWorldPos = GridToWorldCenter(spawnCell);
```

### Asset migration — WaveA.asset

YAML 에서 `pathId: A` 또는 `pathId: B` 를 `spawnIndex: 0` 또는 `spawnIndex: 1` 로 교체:

Before:
```yaml
entries:
- triggerTimeSec: 2.0
  unitType: {fileID: ..., guid: ...}
  pathId: A
- triggerTimeSec: 5.0
  unitType: {fileID: ..., guid: ...}
  pathId: B
```

After:
```yaml
entries:
- triggerTimeSec: 2.0
  unitType: {fileID: ..., guid: ...}
  spawnIndex: 0
- triggerTimeSec: 5.0
  unitType: {fileID: ..., guid: ...}
  spawnIndex: 1
```

### 매핑 규약

Phase 9 PrototypeMap 의 Path A / Path B 는 Phase 10 에서 어떤 spawn index 인가? 현재 `PrototypeMap.spawnCells[0]` 하나만 존재. 맵 procedural 전환 후 spawns 배열이 2~3개 (task 11 에서 `spawnCount = rng.NextInt(2, 4)`). Deck 작성자는 실제 맵의 spawn 개수를 알고 index 를 작성한다.

v1 현실: WaveA.asset 은 현재 2개 entry. procedural 맵이 2+ spawn 생성하므로 idx 0/1 유효.

## 하위호환 없음

Phase 10 개시와 함께 `pathId` 필드 완전 제거. Unity YAML deserialize 는 missing field 를 default(0) 로 처리 — 구 asset 이 migration 없이 로드되면 모든 entry 가 spawnIndex=0 이 됨. 이는 동작은 하지만 **모든 적이 spawn 0 에서 나옴**. 육안으로 바로 확인 가능한 이상 동작이므로 경고만.

## 완료 기준

- `AttackDeck.cs` 컴파일 (unitType 타입은 `AttackUnitData` 유지).
- `WaveA.asset` YAML 수정 완료. Inspector 에서 entry 별 `spawnIndex` 정수 필드 노출.
- EditMode 테스트: `SpawnEntry.spawnIndex=0` 로 설정된 deck 으로 spawn → `GeneratedMap.spawns[0]` 위치에서 적 entity 생성.
- 범위 초과 index → LogWarning + idx 0 fallback.
- PlayMode smoke: procedural 맵 2-spawn 환경에서 WaveA.asset 의 2 entry 가 각각 spawn 0/1 에서 스폰.

## Subtask 분할 (OVERRUN 대응, 35분 예상)

- **15A** — `AttackDeck.SpawnEntry.pathId` → `spawnIndex` 필드 교체 (타입 `AttackUnitData` 유지)
- **15B** — `WaveA.asset` YAML 수정 (entries[].pathId → entries[].spawnIndex)
- **15C** — `BattleBridge` 적 스폰 경로 `entry.spawnIndex` 기반으로 교체 + 범위 체크
