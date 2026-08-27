# 6 — 포탈 프랍 = 스폰/골 마커 비주얼 (재해석 2026-08-27)

## 목적

원안(«`PortalMarker` 한 쌍 = 텔레포트 링크»)은 v1 제외로 남아 있었다. 사용자 결정(2026-08-27)으로 **포탈 프랍을
스폰/골 마커의 비주얼**로 쓴다 — 스폰 = 빨간 포탈(`SpawnPortal_Red` 그대로), 골 = **노란 수직 포탈**(같은 프리팹의
색상 변형). 텔레포트 포탈 기능은 이 unit 범위 밖(후속 후보로 이관).

## 변경 대상

- 신규 `Assets/_Project/Prefabs/Structures/GoalPortal_Yellow.prefab` — `SpawnPortal_Red` 를 언팩해 파티클 `startColor` 의 색조만 50°(노랑)로 돌린 변형. 머티리얼(`Portal_Circle/Point/Smoke`)은 공유. 생성기: `MapStageAuthoringTools.CreateGoalPortalYellow`(메뉴 `Window/Wassup/Map Stage/Create Goal Portal (Yellow)`, 멱등)
- `MapStageAuthoringTools.AttachMarkerVisual(marker, prefabPath)` — 마커 호스트 밑에 프랍을 얹고 `visualRoot` 등록(루트 identity = 수직)
- `MapStageDuelGenerator` — 스폰 2 에 빨강, 골에 노랑 배선 → Duel 재생성
- `GoalMarker.SetStressTint` — 메쉬/파티클 머티리얼의 저작 `_Color`(HDR 밝기 2.37)를 **곱**한다(덮으면 스트레스 0 에서 포탈이 어두워짐). 회귀 테스트 `GoalMarkerTintTests`
- `RenderPrefabPreview(decorate:)` + 러너 `preview_duel_portals`(what-if) / `preview_duel_clean`

## 구현

1. 색은 **`startColor` 에만** 둔다 — `GoalMarker` 의 스트레스 틴트가 머티리얼 `_Color` 에 곱해지므로 색을 머티리얼에 두면 두 writer 가 겹친다. 붕괴(`MarkCollapsed`)는 어두운 절대 틴트 + ×0.6 — `scalingMode=Hierarchy` 라 포탈이 줄어들며 꺼진다.
2. 앵커(`VisualAnchor`) = 렌더러 바운즈 중심 — 파티클 바운즈라 프레임마다 미세 요동. 튜토리얼 포커스·스폰 예보에 허용 범위.
3. 스폰 포탈은 브리지가 색을 건드리지 않는다 — 프리팹 그대로.
4. `SpawnPortal_Red` 는 **보너스 포탈**(`BattleBridge.BonusWave.bonusPortalPrefab`)과 같은 프리팹 — 스폰 지점과 보너스 포탈이 같은 그림. 사용자 관찰: 보너스는 «살짝 누운» 느낌, 마커 포탈은 «수직» — 코드상 둘 다 identity 인스턴스화라 차이 원인은 미확인(후속: 보너스 포탈 전용 변형·핑크 색조).
5. Street/Subway/StreetDay 의 visualRoot 배선은 **map-stage-tile-scale**(재저작) 에서 함께 — 지금 워크트리의 세 프리팹은 15×6 축소 중(미커밋).

## 완료 기준

- [x] `GoalPortal_Yellow.prefab` 존재 · 파티클 3 · 루트 identity — 2026-08-27
- [x] Duel 프리뷰(`preview_duel_clean`): 스폰 2 빨간 수직 포탈 · 골 노란 수직 포탈 — 2026-08-27
- [x] EditMode `GoalMarkerTintTests` green(+DioramaMapBuilder·StagePoolDevEntries 31/31) — 2026-08-27
- [x] PlayMode `DioramaStagePlayTests` 3/3 — 2026-08-27
- [ ] Play 육안: 골 포탈이 스트레스에 붉어지고 붕괴 시 줄어들며 꺼진다

## 후속 후보

- 텔레포트 포탈(원안 `PortalMarker` 쌍 → `PortalLink`) — 필요 시 별도 unit
- 보너스 포탈 전용 변형(핑크·기울기) — 스폰 포탈과 구분
