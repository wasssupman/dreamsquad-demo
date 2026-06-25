# 2 — useRealShadows 토글 + 블롭 폴백

## 목적

진짜 그림자(데스크톱) ↔ 블롭(모바일) 전환. 둘은 상호배타.

## 변경 대상

- 수정: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `useRealShadows` serialized + static 미러
- 수정: `SpineUnitView.cs` / `QuadUnitView.cs` — 토글에 따라 캐스터 vs 블롭 분기
- (선택) `TilemapMapView.cs` — receiveShadows/머티리얼도 토글 기반(OFF 면 기존 unlit)

## 구현

BattleBridge:
- `[SerializeField] private bool useRealShadows = true;`
- `public static bool UseRealShadows { get; private set; }`
- 빌드 시 미러: `UseRealShadows = useRealShadows && !Application.isMobilePlatform;`
  (모바일은 강제 OFF=블롭. 에디터/데스크톱은 serialized 값 따름.)

view 스폰 분기 (Tilemap 모드):
- `UseRealShadows` → 캐스터 ON(`shadowCastingMode=TwoSided`), **BlobShadow.Attach 스킵**.
- else → 현행: BlobShadow.Attach, 캐스터 OFF(`shadowCastingMode=Off`).

TilemapMapView:
- `UseRealShadows` → groundTilemap receive 머티리얼 + receiveShadows=true.
- else → 기존 Sprites/Default(또는 unlit), receiveShadows=false.

> 하드코딩 금지: 토글은 serialized. 모바일 판정은 `Application.isMobilePlatform` (런타임).

## 완료 기준

- 데스크톱 Play: 진짜 그림자(바닥 receive + 캐릭터 cast), 블롭 없음.
- `useRealShadows=false` 또는 모바일: 블롭만(현행), 캐스터/receive 꺼짐 — 회귀 없음.
- 토글 전환이 깔끔히 상호배타(둘 다 켜지거나 둘 다 꺼지지 않음).
