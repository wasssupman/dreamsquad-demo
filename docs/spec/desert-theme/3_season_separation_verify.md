# 3 — 시즌 와이어링 + forest 분리 + Play 검증

## 목적

desert 를 시즌으로 선택 가능하게 하고, 테마 데이터를 forest 와 분리(전용 PropData 카탈로그)한 뒤 Play 로 육안 검증한다.

## 변경 대상

- `Assets/_Project/Data/Season/season_S2_desert.asset` (신규 SeasonData → desert MapThemeData).
- `Assets/_Project/Data/Season/SeasonRegistry.asset` — `allSeasons=[forest, desert]`, `defaultSeason=forest`(유지).
- `Assets/_Project/Data/Theme/desert/*.asset` — forest 중립 PropData 13종 복제.
- `Assets/_Project/Map/Theme/desert/desert.asset` — `tileProps` 를 desert 복제본으로 재연결, forest 텍스처 레거시 참조 null.

## 구현

- `season_S2_desert`: seasonId "S2_Desert", displayName "Sunscorched Dunes", mapTheme=desert, backdrop=forest 재사용(임시).
- 레지스트리에 desert 추가, 기본은 forest 유지(desert 선택형). 검증 시에만 임시로 default=desert.
- **분리(테마 데이터)**: PropData 13종 `Data/Theme/desert/` 복제 + tileProps 재연결(→desert 0 forest 참조 확인). 중립 프리팹/스프라이트/오토타일/Tile_Sand 는 공유.

## 계약

- 기본 게임 룩 불변(default=forest 커밋). desert 는 `defaultSeason` 한 줄로 전환.
- 분리는 "테마 데이터" 한정 — 프리팹/아트 복제 안 함(사용자 합의).
- 씬 SaveScene 금지(사용자 WIP 보존). 에셋만 커밋.

## 완료 기준

- Play(desert 활성) 168 프랍 + sand 바닥 렌더, console 0. tileProps → desert 13 / forest 0.
- 사용자 육안 통과(option 1: 현 상태 + 값 튜닝). teal 엣지·프랍 moss·sand 색조는 후속.

확인: 2026-06-30 사용자 육안 통과(arid 사막). 스샷 `desert_v3_final.png`. 커밋 대기.
