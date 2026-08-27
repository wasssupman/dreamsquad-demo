# 11 — Duel 스테이지 재저작 (main 현행 23×10 · Street 제작방식)

## 목적

풀 정리(2026-08-26) 후 라이브는 사용자 저작 3장(Street·Subway·StreetDay). 남길 옛 맵은 **Duel 하나**이고,
Street 와 같은 제작방식(바닥 Plane + 스프라이트 프랍 + 마커, 루트=원점·원점 xz=0)으로 다시 만든다.
판형 정본은 **main 현행 `MapDocument_Duel`(23×10)** — `MapStage_DuelClassic`(21×12, 강·벽) 은 `ba70aaab~1` 시점의
옛 스냅샷이라 쓰지 않는다.

main 현행 Duel 의 논리(디코드 결과):
- 열린 마당 23×10. 차단은 **중앙 분리대 x=11 의 6칸** — y∈{0, 3,4,5,6, 9} (통로 y1~2 · y7~8 두 개)
- 골 (2,4) · 스폰 = 적 마음 (20,4) 파생 (20,3) lane0 / (20,5) lane1 (`SiegeSpawnOffsets` 순서)
- 본능 4: 방어 `Structure_GuardInstinct` (4,2)/(4,7) · 적 `Structure_WatchInstinct` (18,2)/(18,7)
- 보너스 포탈 (11,2)/(11,7) · 적 진영 배치 제한 x=17..22 (placeMask 04 = Air 만)
- 공중 경유점 (11,4) — 차단 셀 위라 스테이지 형식 검증이 거부 → **생략** (후속 후보: 차단 셀 위 공중 waypoint)

## 변경 대상

- 신규 `Assets/_Project/Editor/MapStageDuelGenerator.cs` — `Window/Wassup/Map Stage/Generate Duel Stage` (재현용, 멱등)
- 산출 `Assets/_Project/Art/Theme/duel/MapStage_Duel.prefab`
- `Assets/_Project/Data/Maps/MapStagePool.asset` — `entries[0]` = Duel + `Deck_Duel` (fallback0/직접 Play = Duel, main 과 동일), 이어서 Street·Subway·StreetDay
- `RalphEditorTasks.cs` — `duel_stage` · `stage_preview`(카메라 프리뷰 PNG 렌더 — 원격 육안 검증)

## 구현

1. 루트 `MapStage_Duel`: `playAreaCells (23,10)` · `gridOriginLocal (0, 0.19, 0)`(Street 와 같은 발바닥 높이) · `previewTileSize 1`.
2. 바닥: Unity Plane(10×10) × `M_Street_Floor` — 본판 중심 (11.5, 0, 5) 스케일 (2.4, 1, 1.5) + 좌/우/전방 확장판(Street 패턴, 16:9 가장자리 공백 방지). MeshCollider 유지(배치 레이캐스트).
3. 배경/장식: 후방 backdrop(`image 2614`, Street 와 같은 스케일·30° 틸트·회색 틴트) + 후방 가장자리 z 11~14 에 Street 프랍 스프라이트(`2623/2624/2625`) 4~5개 — **placeholder**. Duel 전용 아트가 오면 스프라이트만 교체.
4. 중앙 분리대: `PropFootprint` 호스트 3개 — (11,0) 1×1 · (11,3) 1×4 · (11,9) 1×1. 셀마다 `image 2622` 스프라이트 자식(스케일 1.3, 30° 틸트, `SpriteShadowCaster.mat`).
5. 적 진영: `PlacementBlockZone` (17,0) size (6,10). (스테이지 BlockZone 은 전 층 0 — main 의 «Air 만 허용»과 다르다. 층별 BlockZone 은 후속 후보.)
6. 적 마음 자리 (20,4): **비움**(사용자 결정 2026-08-26 — 때릴 수 없는 목표물처럼 보이는 장식은 두지 않는다). 계약 11 유지, 배치 금지는 적 진영 BlockZone 이 덮는다.
7. 마커: `SpawnMarker` (20,3)/(20,5) · `GoalMarker` (2,4) · `BonusSpawnMarker` (11,2)/(11,7) · `StructureMarker` ×4 (unit 10).
8. `Post` 자식 Volume — Street 프로파일 재사용(placeholder). `PushStagePostVolume` 이 스테이지 수명으로 넘긴다.
9. 프리뷰: `stage_preview` 태스크가 임시 씬에 프리팹을 놓고 `MapStageCameraFraming.FrameActiveScene(16/9)` 로 Battle 카메라를 맞춘 뒤 1920×1080 PNG 를 `.omc/ralph/` 에 쓴다 — 에이전트가 이미지로 확인.

## 완료 기준

- [x] `StagePoolBuildabilityTests` green (23×10 ≤ 30×12 캡 · 포탈 규칙 · 연결성) — 2026-08-26 `df1a117a`
- [x] 프리뷰 PNG 에서 바닥·분리대·마커 위치가 격자와 맞음(분리대가 x=11 열에, 통로 두 개) — 2026-08-26 (`preview_duel` 러너 태스크)
- [x] PlayMode `DioramaStagePlayTests`(Duel 로 재지정): 적이 분리대 셀을 밟지 않고 골(−x) 방향 전진 · 본능 4기 스폰 — 2026-08-26 `df1a117a`
- [x] `BonusWavePullTest`("Duel" pin) green — 포탈 2 → 보너스 적 — 2026-08-26 `df1a117a`
- [x] 사용자 결정 2026-08-26: ⓐ 적 마음 장식 **제거** ⓑ **맵마다 다른 덱** — Duel=`Deck_Duel`(Jjangssen) · Street=`Deck_Serpent`(Nightmare) · Subway=`Deck_Zig`(Mamemo) · StreetDay=`Deck_Coil`(Nightmare); main 현행 세대(gen 7·컨셉 5) 맵 덱을 재배정, 옛 맵 이름 개명은 «레거시 덱 정리» 후속 ⓒ 세 맵 **live 유지**
