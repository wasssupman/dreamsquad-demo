# Texture Variants Runtime Swap

**작업 구분**: 6

## 목적

ProjectileData 가 텍스처 변종 배열을 지정하면, view 풀이 spawn 마다 1장을 선택해 MaterialPropertyBlock 으로 `_BaseMap`/`_MainTex` 를 swap 한다.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Data/ProjectileData.cs`
- Modify: `Assets/_Project/Scripts/Presentation/ProjectileViewPool.cs`

## ProjectileData 신규 필드

```csharp
public enum TextureSelectMode
{
    Random,      // _visualRng 으로 매 spawn 마다 1장
    Sequential,  // spawnCounter % length
    First,       // 항상 [0]
}

[Header("Texture variants")]
public Texture2D[] textureVariants;          // 비어있으면 prefab 원본 사용
public TextureSelectMode selectMode = TextureSelectMode.Random;
```

## ProjectileViewPool 확장

- 풀이 ProjectileData 별 `_spawnCounter` Dictionary 보관.
- spawn 시:
  ```csharp
  if (data.textureVariants != null && data.textureVariants.Length > 0) {
      int idx = data.selectMode switch {
          TextureSelectMode.Random => _visualRng.Next(data.textureVariants.Length),
          TextureSelectMode.Sequential => _GetAndIncrementCounter(data) % data.textureVariants.Length,
          TextureSelectMode.First => 0,
          _ => 0,
      };
      mpb.SetTexture(_BaseMap, data.textureVariants[idx]);
      mpb.SetTexture(_MainTex, data.textureVariants[idx]);   // legacy fallback
  }
  ```
- 적용 대상: task 4 의 MPB 적용 path 와 동일 (root + 자식 Renderer 전체).
- 풀 반환 시 MPB 초기화 (task 4 와 동일 — 이미 초기화 처리되어 있으면 추가 변경 없음).

## 자산 (이번 task 에서 와이어링 안 함)

베이크된 12장 (`wind/stone/fire/water_var*`) 은 task 5 에서 생성됨. 실제 ProjectileData 자산이 이를 참조하는 것은 task 7 demo 에서. 이번 task 는 인프라만.

## 완료 기준

- compile + Play smoke: textureVariants 가 비어있으면 prefab 원본 텍스처 그대로 (회귀 없음).
- textureVariants 에 임시로 1~3장 넣어 시연 시 spawn 마다 다른 텍스처 보임 (Random) / 순차 (Sequential).
- 풀 반환 후 다른 ProjectileData 의 view 로 재사용될 때 텍스처 잔재 없음.
- read_console Error/Warning 0.

확인 2026-04-28 / 커밋: (pending)
