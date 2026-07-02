# 3 — 잔여 필드/코드 retire

## 목적

이관 완료 후 dead 가 된 필드·헬퍼·마이그레이션 코드를 제거한다. `placementWeight` 는 참조를 확인해 유지/제거를 결정한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/MapThemeData.cs` (`tileProps` 제거)
- `Assets/_Project/Scripts/Data/PropData.cs` (`excludeFromDistantRing`, `distantRingWeight` 제거; `placementWeight` 검토)
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` (`RingWeight` 제거)
- `Assets/_Project/Scripts/Editor/ThemePropPoolMigration.cs` (삭제)

## 구현

### 삭제 확정

- `MapThemeData.tileProps` 필드 (마이그레이션 완료로 소스 불필요; 직렬화 orphan 은 무해).
- `PropData.excludeFromDistantRing`, `PropData.distantRingWeight` (원경 opt-out 을 리스트 소속이 대체).
- `TilemapMapView.RingWeight` 헬퍼 (unit 2 에서 호출 제거됨).

### placementWeight 결정

먼저 참조 확인:

```
grep -rn "placementWeight" Assets/_Project --include="*.cs"
```

- **PropDataEditor 등 authoring 코드가 참조하면**: `placementWeight` 를 **유지**하되 tooltip 을 "authoring 기본값 — 테마 리스트에 추가 시 초기 weight seed. 런타임 배치는 MapThemeData 의 WeightedProp.weight 가 권위" 로 갱신. placement 코드에서의 참조는 이미 unit 1 에서 제거됨.
- **참조가 없으면**: `placementWeight` 도 제거.

이 결정은 grep 결과에 따라 unit 내에서 확정하고 handoff 에 한 줄 기록.

### dead ref 확인

제거 후:

```
grep -rn "tileProps\|excludeFromDistantRing\|distantRingWeight\|RingWeight" Assets/_Project/Scripts --include="*.cs"
```

→ 0 매칭 (또는 주석만).

## 완료 기준

- compile 성공, `read_console` 0 error.
- `run_tests` EditMode `BackgroundPropPlacerTests` green (회귀 없음).
- 위 grep dead ref 0.
- Play→스크린샷 재확인: unit 2 검증 결과와 동일 (배치 회귀 없음).
