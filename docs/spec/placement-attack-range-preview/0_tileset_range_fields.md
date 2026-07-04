# 0 — TileSetData 범위 필드

## 목적

공격 범위 하이라이트의 타일 스프라이트 / 색 / 펄스 파라미터를 데이터로 노출한다(하드코딩 금지).
unit 1 이 이 값을 읽어 그리고 펄스한다.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Data/TileSetData.cs`
- Asset: 사용 중인 `TileSet*.asset` (rangeTile / 색 / 펄스 값 세팅)

## 구현

`TileSetData` 에 헤더 + 필드 추가:

```csharp
[Header("Attack range highlight (placement-attack-range-preview)")]
// 중립(흰색 계열) solid 타일. 색은 rangeColor 가 tint 로 입힌다.
public TileBase rangeTile;
public Color rangeColor = new Color(1f, 0.85f, 0.1f, 1f); // 노랑
[Range(0f, 1f)] public float rangePulseMinAlpha = 0.35f;
[Range(0f, 1f)] public float rangePulseMaxAlpha = 0.85f;
[Min(0.05f)] public float rangePulseSpeed = 3f; // sin(unscaledTime * speed) 각속도 → 주기 ≈ 2s
```

- `rangeTile`: 무채색 solid 스프라이트 Tile 재사용(있으면). 없으면 1px 흰색 스프라이트로 Tile 하나 생성.
  tint 로 색을 제어하므로 스프라이트 자체는 **무채색이 이상적**(유채색이면 rangeColor 와 곱해져 의도색 안 나옴).
- 코드 기본값은 시작점 — 실제 값은 SO 인스펙터에서 조정.

## 완료 기준

- compile 통과.
- TileSet 에셋 인스펙터에 rangeTile / rangeColor / pulseMinAlpha / pulseMaxAlpha / pulseSpeed
  5필드가 보이고, rangeTile 에 무채색 solid 타일이 배정됨.
- 이 unit 단독으로 시각 변화 없음(unit 1 이 소비).
