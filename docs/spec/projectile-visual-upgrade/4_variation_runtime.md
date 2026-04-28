# Variation Runtime (Tint / Jitter / Emission)

**작업 구분**: 4

## 목적

ProjectileData 의 결정적 노브와 per-shot 랜덤 노브를 view 풀이 MaterialPropertyBlock 으로 적용하도록 한다. 시뮬레이션 결정성과 분리된 시각 전용 RNG 사용.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Data/ProjectileData.cs`
- Modify: `Assets/_Project/Scripts/Presentation/ProjectileViewPool.cs`

## ProjectileData 신규 필드

```csharp
[Header("Variation - deterministic")]
public Color tintColor = Color.white;
public float emissionMultiplier = 1f;

[Header("Variation - per-shot random")]
[Range(0f, 1f)] public float scaleJitter = 0f;     // ±N% 스케일
[Range(0f, 0.5f)] public float hueJitter = 0f;     // HSV hue ±N
[Range(0f, 360f)] public float rotationJitter = 0f; // 발사 시 Y축 roll 범위(deg)
```

## ProjectileViewPool 확장

- `Spawn(entity, data, ...)` 가 `ProjectileData` 자체를 받도록 (현재 prefab/scale/facing 분리 인자를 data 로 통합) 또는 추가 인자로 `tintColor/emissionMul/jitter` 묶음 전달.
- 시각 RNG: `private System.Random _visualRng = new System.Random(seed)` — 시드는 `BattleBridge.InitializeBattleSingletons` 에서 단일 값(예: `(int)Time.realtimeSinceStartupAsDouble`) 또는 sessionId 기반. 시뮬 RNG 와 별도.
- spawn 마다:
  ```csharp
  float scaleMul = 1f + (float)(_visualRng.NextDouble() * 2 - 1) * data.scaleJitter;
  float hueShift = (float)(_visualRng.NextDouble() * 2 - 1) * data.hueJitter;
  float rollDeg = (float)(_visualRng.NextDouble() * 2 - 1) * data.rotationJitter;
  Color finalTint = ApplyHueShift(data.tintColor, hueShift);
  float emission = data.emissionMultiplier;
  ```
- MaterialPropertyBlock:
  - `_BaseColor` (URP) / `_Color` (legacy) 둘 다 시도.
  - `_EmissionColor` = finalTint * emission.
  - 적용 대상: view 의 root + 직계 자식 모든 `Renderer` (`GetComponentsInChildren<Renderer>(includeInactive: false)`).
- Roll: 비행체 root transform 에 `localRotation *= Quaternion.Euler(0, 0, rollDeg)` (facing 정책과 충돌 안 하도록 prefab 의 forward 축 기준 roll).
- 풀 반환 시 MPB 초기화 (`renderer.SetPropertyBlock(null)`).

## ApplyHueShift 보조

```csharp
private static Color ApplyHueShift(Color c, float hueShift01)
{
    Color.RGBToHSV(c, out float h, out float s, out float v);
    h = Mathf.Repeat(h + hueShift01, 1f);
    return Color.HSVToRGB(h, s, v).WithAlpha(c.a);
}
```

## 완료 기준

- compile + Play smoke: 같은 ProjectileData 로 발사된 N발의 색조/스케일이 미세하게 다름 (가시 확인).
- jitter 가 0 일 때 모든 발사가 정확히 동일 시각 (회귀 보호).
- 시뮬 결정성 회귀 없음: 동일 시드로 wave 재생 시 데미지/이벤트 시퀀스 동일 (시각 RNG 가 시뮬 결정에 영향 없음을 코드 리뷰로 확인).
- 풀 반환 후 같은 view 가 다른 spawn 에 재사용될 때 이전 MPB 잔재 없음.
- read_console Error/Warning 0.
