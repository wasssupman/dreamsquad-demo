# 1. Data Model — Season / Backdrop / Registry

## 목적

시즌-백드롭-레지스트리 SO 3종 + EdgeAnchor enum + EdgePropEntry struct 를 정의한다. 이 단위가 끝나면 다른 단위가 의존할 데이터 계약이 모두 잡혀있다.

## 변경 대상

신규 스크립트 (`Assets/_Project/Scripts/Data/Season/`)

- `SeasonData.cs`
- `SeasonBackdropData.cs`
- `SeasonRegistry.cs`
- `SeasonRuntime.cs`

## 구현

### SeasonData.cs

```csharp
[CreateAssetMenu(menuName = "Wassup/Season/SeasonData", fileName = "season")]
public sealed class SeasonData : ScriptableObject
{
    public string seasonId = "S1_Forest";
    public string displayName = "Verdant Bloom";
    public MapThemeData mapTheme;          // 시즌이 제압
    public SeasonBackdropData backdrop;    // 1종
}
```

### SeasonBackdropData.cs

```csharp
public enum EdgeAnchor
{
    NorthLeft, NorthCenter, NorthRight,
    EastTop, EastMiddle, EastBottom,
    SouthRight, SouthCenter, SouthLeft,
    WestBottom, WestMiddle, WestTop,
}

[Serializable]
public struct EdgePropEntry
{
    public PropData propData;       // 기존 PropData SO 직접 참조
    public EdgeAnchor anchor;
    public Vector2 worldOffset;     // anchor 위치에서 미세 보정
    public float yawDegrees;        // +y 회전 (보드 중앙 향함이 0)
    public float scaleMultiplier;   // 1.0 기본
}

[CreateAssetMenu(menuName = "Wassup/Season/SeasonBackdropData", fileName = "backdrop")]
public sealed class SeasonBackdropData : ScriptableObject
{
    [Header("Far Backdrop")]
    public Texture2D farBackdropTexture;
    public float backdropDistance = 60f;
    public float backdropHeightWorld = 30f;
    public Color backdropTint = Color.white;

    [Header("Edge Props")]
    public EdgePropEntry[] edgeProps = Array.Empty<EdgePropEntry>();

    [Header("Edge Layout")]
    public float edgePadding = 1.5f;   // tile 단위. 보드 외곽까지의 거리
}
```

### SeasonRegistry.cs

```csharp
[CreateAssetMenu(menuName = "Wassup/Season/SeasonRegistry", fileName = "SeasonRegistry")]
public sealed class SeasonRegistry : ScriptableObject
{
    public SeasonData[] allSeasons = Array.Empty<SeasonData>();
    public SeasonData defaultSeason;

    public SeasonData activeSeason => defaultSeason;  // 토너먼트 메타 hook 자리
}
```

### SeasonRuntime.cs

```csharp
public static class SeasonRuntime
{
    private static SeasonRegistry _registry;

    public static void Bind(SeasonRegistry registry) => _registry = registry;
    public static SeasonData Active => _registry != null ? _registry.activeSeason : null;
}
```

`BattleBridge` 가 `Awake` 에서 정확히 한 번 `SeasonRuntime.Bind(seasonRegistry)` 호출한다. `BuildMapForBattle` 을 포함한 다른 모든 경로는 `SeasonRuntime.Active` 만 read 한다.

## EdgePropEntry 사용 계약 (1번 단위에선 필드 정의만, 5번 단위에서 강제)

`EdgePropEntry.propData` 가 가리키는 PropData 는 다음을 만족해야 한다:

- `placementWeight = 0` — BackgroundPropPlacer 자동 분포에서 제외 (`BackgroundPropPlacer.cs:287` 의 `<= 0` 가드).
- `billboardMode = PropBillboardMode.None` — 정적 풍경. BackdropMounter 도 PropBillboard 컴포넌트를 disable 하지만 SO 단계에서도 일치시켜둔다.

이는 5번 단위에서 6종 prop_concept SO 일괄 수정 + 신규 2종 PropData 생성 시점에 강제된다.

## 완료 기준

- 4 스크립트 컴파일 통과: `mcp__UnityMCP__read_console` clean.
- Inspector 에서 `Wassup/Season/SeasonData` / `SeasonBackdropData` / `SeasonRegistry` 메뉴가 보이고 `Create` 가능.
- 본 단위에서는 SO 인스턴스를 만들지 않는다. 5번 단위에서 채움.

## 의존

- 선행: 0번 (미커밋 분리)
- 후행: 2, 3, 5번이 이 데이터 모델을 사용한다.

확인 일자: 2026-05-10 / 커밋: 49b209c
