# 1. 등장 풀을 버프 3종으로 + 적용 회귀

## 목적

**2026-07-31 사용자 결정**: 디버프 타일(취약·글래스캐논)은 등장하지 않는다. 공격력·공속·재생 3종만 회전하고, 그 3종이 저작값대로 실제 적용되는지 회귀로 고정한다.

## 변경 대상

- `Assets/_Project/Map/Theme/forest/forest.asset` — `effectTiles` 에서 GUID 2개 제거
- `Assets/_Project/Tests/PlayMode/EffectTileBuffApplyTest.cs` — 신설

## 구현

**풀에서만 뺀다 — 에셋·아이콘·Tile 은 남긴다.** `effect_tile_fragile`(`a84cc90d…`) · `effect_tile_glass_cannon`(`8e0b183d…`) 두 GUID 를 `forest.effectTiles` 에서 제거한다. 되살리려면 그 두 줄을 다시 넣으면 되고, 금 간 방패·방패+칼 아이콘도 그대로 남아 있다. `effectTileCount` 는 3 유지. `desert.asset` 은 `effectTiles: []` 라 대상이 아니다.

**적용 회귀는 PlayMode 로 간다.** 기존 `EffectTileModifierTests`(EditMode)는 이벤트 shape 을 손으로 재현하므로 "어느 에셋이 어느 stat 을 주는가"는 아무도 잡지 않았다 — 에셋에서 `stat` 을 잘못 고쳐도 전부 green 이었다. 새 테스트는 실제 SO → `BattleBridge.AddEffectTile` → `ModifierStats` 까지 통과시킨다(`PlacementAuraTest` 하네스 패턴).

두 가지가 검증 설계의 핵심이다:

1. **baseline 델타로 본다.** 부착 **전** stat 을 읽고 부착 후와 비교한다. 절대값으로 봤다면 유닛 고유 on-place 버프에 오염된다 — 실제로 이 테스트 실행 로그에 `가디언: BoostNearbyDefenders affected=2` 가 찍힌다.
2. **효과 타일이 없는 셀에만 배치한다.** 맵 빌드가 이미 깐 3개 셀에 놓으면 그 타일의 stat 이 배치 시점에 먼저 붙어 대조가 깨진다. `_effectTilesByCell` 을 읽어 그 셀을 건너뛴다.

## 완료 기준

- PlayMode `EffectTileBuffApplyTest` green — 공격력 ×1.25 · 공속 ×1.2 · 재생 +1(합연산).
- Play 육안: `effectTileCount` 를 임시 상향해 다수 추첨을 강제했을 때 빨강·보라가 **0개**.
- `git diff` 가 풀 2줄 삭제만 — `effectTileCount` 는 3 으로 원복.

**완료 확인 2026-07-31** — PlayMode 1/1 green(3종 전부 저작값 일치). 34개 추첨 강제 스크린샷에서 주황·파랑·초록만 등장, 빨강·보라 0(`Assets/Screenshots/effect_tile_buffs_only.png`, 로컬·미추적). `effectTileCount` 3 원복 및 diff 확인, 활성 씬(OutgameScene) 원복. 커밋 `TBD`.
