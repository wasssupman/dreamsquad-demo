# MapTileType Enum

**작업 구분**: Phase 10A

## 목적

맵 타일 4종 타입을 정의한다. 사용자 bullet 6 "이동/방어배치/환경/배경 오브젝트" 를 enum 으로 매핑.

## 변경 대상

- 새 파일: `Assets/_Project/Scripts/Data/MapTileType.cs`
- 기존 `TileType` (Buildable/Path/Obstacle) 은 Phase 10A 동안 병존, Phase 10A 종료 시점 제거 판단

## 구현

```csharp
namespace Wassup.Data
{
    // Phase 10: mutually exclusive 4종. 한 타일 = 한 역할.
    public enum MapTileType : byte
    {
        Walk  = 0,   // 적 이동 가능 (flow field walkable)
        Place = 1,   // defender 배치 가능
        Env   = 2,   // 환경 (Phase 10 = 시각 구분만, Phase 11 에서 효과)
        Deco  = 3,   // 배경 오브젝트 (시각 장식)
    }
}
```

## 의미 계약

- `Walk`: flow field BFS 가 walkable mask 로 사용. 적이 이 타일로만 이동
- `Place`: `PlacementInput` 이 defender 배치 허용 판정에 사용
- `Env`: Phase 10 에선 `MapView` 가 시각 구분만 (색/prefab). 효과 동작은 Phase 11 에서 별도 NativeArray layer
- `Deco`: 배경 오브젝트 타일. flow field 차단, 배치 불가. 시각만 담당

## 기존 TileType 과 분리 이유

기존 `TileType { Buildable=0, Path=1, Obstacle=2 }` 와 새 `MapTileType { Walk=0, Place=1, Env=2, Deco=3 }` 는 숫자 배정이 비호환. 이름을 분리하여 Phase 10A 진행 중 병존 허용, 전환 완료 후 `TileType` 제거 (Phase 10B 또는 이후 cleanup).

- `TileType.Path` (적 이동) → `MapTileType.Walk`
- `TileType.Buildable` (defender 배치) → `MapTileType.Place`
- `TileType.Obstacle` (Phase 9 walkable=Path-only 에서 적 차단) → `MapTileType.Deco` (배경 오브젝트)
- `MapTileType.Env` 는 Phase 9 에 대응 없음 — Phase 10 신설 타입

## 완료 기준

- `MapTileType.cs` 컴파일.
- EditMode 테스트: `MapTileType` 4개 값이 순서대로 0/1/2/3 인지 확인 (sanity check).
- `TileType` 은 건드리지 않음 (Phase 10A 종료 시 별도 작업으로 제거 판단).
