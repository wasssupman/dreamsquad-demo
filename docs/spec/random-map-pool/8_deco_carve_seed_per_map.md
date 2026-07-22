# 8. Deco 커빙 시드를 맵 정체성으로 — 배치칸 맵당 고정

## 목적

`fixedMapSeed=0`(랜덤맵 스위치) 이후, 같은 맵인데도 **배치 가능(Place) 칸이 매판 달라지는** 부작용을 제거한다. 원인은 `DesignateDeco`(theme.keepRatio<1 일 때 Place 의 일부를 Deco 로 깎음)의 시드가 matchSeed 파생 local `seed` 라, 매판 다른 셀이 Deco 로 빠졌기 때문.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (deco 커빙 블록, 1줄)

## 구현

- `decoRng` 시드를 local `seed`(matchSeed 파생) → **`_generatedMap.seed`(맵 정체성)** 로 교체:
  ```
  var decoRng = Random.CreateFromIndex((uint)(_generatedMap.seed ^ 0x5A5A5A) | 1u);
  ```
- **문서맵**: `_generatedMap.seed = doc.AuthoringSeed`(수동맵 = -1) → matchSeed 무관 → **배치칸 매판 고정**. 서로 다른 문서맵은 base 타일이 달라 deco 도 다름(맵별 개성 유지).
- **절차 MapGrid 맵**: `_generatedMap.seed = gen seed`(matchSeed 파생) → 매판 변동 유지(절차맵의 의도된 varying).
- keepRatio=1 테마면 커빙 자체가 no-op(무영향).

## 완료 기준

- [x] compile 0 errors
- [x] Play-free 실증: ArkFunnel deco 커빙 판1 vs 판2 다른셀 = **0** (기존 공식은 30). 배치칸 50개 고정
- [ ] (사용자) Play 로 같은 맵 반복 시 배치 가능 타일 동일, 맵 바뀌면 배치판도 바뀜 육안 확인

확인 2026-07-23 (unit 8 — deco 시드 맵 정체성화, 배치칸 맵당 고정 실증).
