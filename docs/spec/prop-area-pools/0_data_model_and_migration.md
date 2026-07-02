# 0 — 데이터 모델 + 마이그레이션

## 목적

근경/원경 가용 프랍을 두 개의 명시적 가중치 리스트로 소유하는 데이터 모델을 도입한다. 기존 `tileProps` 데이터를 두 리스트로 복사하는 일회성 마이그레이션까지 수행한다. 이 단위에서는 placer 를 아직 바꾸지 않는다 (behavior 무변경, 데이터만 추가).

## 변경 대상

- `Assets/_Project/Scripts/Data/MapThemeData.cs`
- `Assets/_Project/Scripts/Editor/ThemePropPoolMigration.cs` (신규, 일회성 — unit 3 에서 삭제)

## 구현

### WeightedProp

`MapThemeData.cs` 안에 `TerrainSurfaceVariant` 처럼 nested serializable 로 추가:

```csharp
[Serializable]
public sealed class WeightedProp
{
    public PropData prop;
    [Min(0f)] public float weight = 10f;
}
```

### MapThemeData 필드

기존 `public PropData[] tileProps;` 는 **유지**하고 (마이그레이션 소스), 두 리스트를 추가:

```csharp
[Header("Play Area Props (prop-area-pools)")]
[Tooltip("플레이 영역(Env 셀) 근경 프랍 풀. 항목별 weight = 룰렛 base. weight<=0/prefab 없음=제외.")]
public WeightedProp[] playAreaProps;

[Header("Distant Ring Props (prop-area-pools)")]
[Tooltip("외곽 터레인 링 원경 프랍 풀. 근경과 독립. 같은 프랍을 다른 weight 로 등록 가능.")]
public WeightedProp[] distantRingProps;
```

### 마이그레이션 (일회성 에디터)

`ThemePropPoolMigration.cs` — 메뉴 `Wassup/Dev/Migrate Theme Prop Pools`:

- `AssetDatabase.FindAssets("t:MapThemeData")` 로 모든 테마 순회 (현재 `forest.asset`, `desert.asset`).
- 각 테마에서 `playAreaProps`/`distantRingProps` 가 이미 비어있지 않으면 skip (멱등).
- `playAreaProps` ← `tileProps` 전체. weight = `Mathf.Max(0, prop.placementWeight)`.
- `distantRingProps` ← `tileProps` 중 `!excludeFromDistantRing`. weight = `prop.distantRingWeight >= 0f ? prop.distantRingWeight : Mathf.Max(0, prop.placementWeight)` (기존 `RingWeight` 규칙 계승).
- `EditorUtility.SetDirty` + `AssetDatabase.SaveAssets`.

`#if UNITY_EDITOR` 가드 불필요 (Editor 폴더 = 자동 에디터 전용 asmdef).

## 완료 기준

- compile 성공 (read_console 0 error).
- 메뉴 실행 후 `forest.asset` 인스펙터에서 `playAreaProps` = 기존 tileProps 전부, `distantRingProps` = 꽃 등 excludeFromDistantRing 제외 목록으로 populate 확인.
- 멱등: 메뉴 재실행 시 값 변화 없음.
- placer 미변경이므로 Play 시 프랍 배치 **육안 동일**(회귀 없음).

확인: 2026-07-02 · `a0ce63c` — compile 클린, forest 9/6·desert 13/13 populate, 멱등성(재실행 skip 0/2) 검증.
