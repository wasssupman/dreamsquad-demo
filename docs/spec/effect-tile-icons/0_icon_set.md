# 0. 아이콘 5종 재저작

## 목적

`Assets/_Project/Art/EffectTiles/ET_*.png` 5장의 글리프를 효과 아이콘으로 교체한다. 배경(색·보더)과 파일 경로는 유지해 배선을 건드리지 않는다.

## 변경 대상

- `Assets/_Project/Art/EffectTiles/ET_Damage.png` — 칼
- `Assets/_Project/Art/EffectTiles/ET_AttackSpeed.png` — 번개
- `Assets/_Project/Art/EffectTiles/ET_Regen.png` — 하트 + 작은 `+`
- `Assets/_Project/Art/EffectTiles/ET_Fragile.png` — 금 간 방패
- `Assets/_Project/Art/EffectTiles/ET_GlassCannon.png` — 방패 + 칼

`.meta` · `ET_*.asset`(Tile) · `effect_tile_*.asset`(EffectTileData) · `forest.asset` 전부 **무변경**.

## 구현

실측한 기존 규격을 그대로 재현한다: 64×64, 알파 255 전면, 다크 보더 3px, 글리프 순백.

| 타일 | fill (RGB) | border (RGB) |
|---|---|---|
| Damage | 255,128,26 | 102,51,10 |
| AttackSpeed | 38,128,255 | 15,51,102 |
| Regen | 51,204,64 | 20,82,26 |
| Fragile | 184,26,31 | 73,10,12 |
| GlassCannon | 158,51,235 | 63,20,94 |

저작 방식 — 256px 마스크에 AA 로 도형을 그리고 64px 로 축소한 뒤, crisp 하게 깐 배경 위에 알파 블렌드한다. 두 가지가 함정이다:

1. **마스크 RGB 를 흰색으로 채워두고 알파만 변화시킨다.** 투명 픽셀의 RGB 가 검정이면 축소 보간이 글리프 테두리에 회색 프린지를 만든다.
2. **겹침은 팽창 컷으로 분리한다**(글래스캐논). 위 도형을 알파 0 · 굵은 펜으로 한 번 그려 아래 도형을 파낸 뒤 흰색으로 다시 그린다. 간격 없이 겹치면 64px 에서 두 아이콘이 한 덩어리가 된다.

배치 파라미터(256px 로컬 좌표, 원점 = 아이콘 중심): 칼 = 날 폭 38·전장 220, 가드 폭 116; 번개 = 6점 지그재그 폭 108·높이 216; 하트 = 반지름 44 원 2개 + 삼각형, `+` 는 (192,62)·팔 27·두께 14; 방패 = 상단 직선 + 하단 베지어, 금은 폭 17 지그재그 컷; 글래스캐논 = 방패 0.74 배(금 없음, 칼이 파낸 간격이 깨짐을 말한다) + 칼 0.56 배 40° 회전, 간격 펜 20.

## 완료 기준

- 에디터 refresh 후 콘솔 클린 (force reimport 금지 — MCP 브리지 끊김).
- Play 육안 검증: 인게임 타일 크기에서 5종이 서로 구분되고 효과가 읽힌다. 맵당 3개만 뽑히므로 `forest.effectTileCount` 를 임시 상향해 5종을 한 화면에 모은 뒤 **원복**한다.
- `git status` 에 PNG 5개만 modified (`.meta` · 테마 에셋 무변경).

**완료 확인 2026-07-31** — Play(BattleScene) 게임뷰에서 5종 전부 확인: 주황 칼 · 파랑 번개 · 초록 하트+ · 빨강 금간방패 · 보라 방패+칼. 스크린샷 `Assets/Screenshots/effect_tile_icons_all5b.png`(로컬, 미추적). `effectTileCount` 34 임시 상향 후 3 으로 원복, 콘솔 에러/워닝 0, 활성 씬(OutgameScene) 원복. 커밋 `TBD`.
