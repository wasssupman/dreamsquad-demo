# 0 — TileSetData 배치 하이라이트 필드

## 목적

배치 가능 하이라이트(은은한 fill + 밝은 림)의 시각 파라미터를 `TileSetData` 에 데이터로 추가한다.
하드코딩 금지 — 시즌/테마별로 이 에셋만 swap. (정확한 RGBA 는 시안 확정 후 이 기본값을 덮어씀.)

## 변경 대상

- Modify: `Assets/_Project/Scripts/Data/TileSetData.cs`
- Asset: 라이브 TileSet(desert·forest) 에 `placeableTile` 할당.

## 구현

`[Header("Placement highlight (placement-eligible-tile-highlight)")]` 블록 추가:

```csharp
// 배치 가능 칸을 덮는 타일. 안쪽 은은한 fill + 가장자리 밝은 림이 한 스프라이트에 구워져
// "플랫폼(슬랩)" 느낌을 준다(3D 융기 없음). 색은 placeableColor 가 tint.
// 형태 조정(림 두께/베벨)은 이 스프라이트 교체로만 — 페인트 코드는 스프라이트-agnostic.
public TileBase placeableTile;
[Tooltip("배치 하이라이트 tint. 차갑고 낮은 채도(초록 금지 — 초록은 hover 전용). 밝은 벌판이 안 되게 알파는 은은하게.")]
public Color placeableColor = new Color(0.55f, 0.8f, 0.95f, 0.28f); // 연한 시안, 낮은 알파
[Min(0f)]
[Tooltip("드래그/arm 시작 시 하이라이트가 0→placeableColor.a 로 차오르는 시간(초). unscaledTime 기준.")]
public float placeableFadeInDuration = 0.2f;
```

- `placeableColor.a` = 전역 tint 알파(은은). 0.28 시작 — 절반 규모 영역이 판을 뒤덮지 않게 낮게. Play/시안으로 튜닝.
- **림/플랫폼 느낌은 `placeableTile` 스프라이트가 소유**(안 은은fill + 밝은 테두리 픽셀). per-cell 색 없음(균일 tint).
- 초록 계열 금지(hover valid 와 충돌). 노랑 금지(사거리와 충돌). → 차가운 시안/블루 계열이 안전.

## 완료 기준

- 컴파일 0 errors.
- 인스펙터에 하이라이트 헤더 3필드 노출, 기본값 = 연한 시안 α0.28 / 페이드 0.2s.
- 라이브 TileSet 에셋에 `placeableTile` 할당(SaveAssets). 미할당 시 unit 1 이 no-op 로 방어.
- (임시 스프라이트라도 무방 — 시안 확정 후 GUID 유지 교체로 실아트 반영.)
