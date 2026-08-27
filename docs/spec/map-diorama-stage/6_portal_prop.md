# 6 — 포탈 프랍 = 스폰/골 마커 비주얼 (재해석 2026-08-27)

## 목적

원안(«`PortalMarker` 한 쌍 = 텔레포트 링크»)은 v1 제외로 남아 있었다. 사용자 결정(2026-08-27)으로 **포탈 프랍을
스폰/골 마커의 비주얼**로 쓴다 — 스폰 = 빨간 포탈(`SpawnPortal_Red` 그대로), 골 = **노란 수직 포탈**(같은 프리팹의
색상 변형). 텔레포트 포탈 기능은 이 unit 범위 밖(후속 후보로 이관).

## 변경 대상

- 신규 `Assets/_Project/Prefabs/Structures/GoalPortal_Yellow.prefab` — `SpawnPortal_Red` 를 언팩해 파티클 `startColor` 의 색조만 50°(노랑)로 돌린 변형. 머티리얼(`Portal_Circle/Point/Smoke`)은 공유. 생성기: `MapStageAuthoringTools.CreateGoalPortalYellow`(메뉴 `Window/Wassup/Map Stage/Create Goal Portal (Yellow)`, 멱등)
- **rev 2 (2026-08-27, 공유 구조)** — 프랍은 프리팹에 심지 않는다. 신규 `Scripts/Data/MapStage/MarkerPropStyle.cs`(SO, `spawnProp`/`goalProp`) + 정본 `Data/Maps/MarkerPropStyle.asset` · 신규 `Scripts/Presentation/MarkerPropInstaller.cs`(BattleScene `_MarkerProps`) · `MapStage.Enabled` 수명 신호(`OnEnable`, 로직 없는 알림 1개)
- `MapStageDuelGenerator` — 마커만 심는다(rev 1 의 `AttachMarkerVisual` 내장은 제거 → Duel 재생성). `MapStageAuthoringTools.EnsureMarkerPropStyle`/`ApplySharedMarkerProps`(프리뷰 미러)
- `GoalMarker.SetStressTint` — 메쉬/파티클 머티리얼의 저작 `_Color`(HDR 밝기 2.37)를 **곱**한다(덮으면 스트레스 0 에서 포탈이 어두워짐). 회귀 테스트 `GoalMarkerTintTests`
- `RenderPrefabPreview` — `decorate` 뒤 `ApplySharedMarkerProps` 로 «실제로 보일 그림». 러너 `marker_prop_style` · `shared_props_all`(스타일 보장 → Duel 재생성 → 4맵 클린 프리뷰)

## 구현

1. 색은 **`startColor` 에만** 둔다 — `GoalMarker` 의 스트레스 틴트가 머티리얼 `_Color` 에 곱해지므로 색을 머티리얼에 두면 두 writer 가 겹친다. 붕괴(`MarkCollapsed`)는 어두운 절대 틴트 + ×0.6 — `scalingMode=Hierarchy` 라 포탈이 줄어들며 꺼진다.
2. 앵커(`VisualAnchor`) = 렌더러 바운즈 중심 — 파티클 바운즈라 프레임마다 미세 요동. 튜토리얼 포커스·스폰 예보에 허용 범위.
3. 스폰 포탈은 브리지가 색을 건드리지 않는다 — 프리팹 그대로.
4. `SpawnPortal_Red` 는 **보너스 포탈**(`BattleBridge.BonusWave.bonusPortalPrefab`)과 같은 프리팹 — 스폰 지점과 보너스 포탈이 같은 그림. 사용자 관찰: 보너스는 «살짝 누운» 느낌, 마커 포탈은 «수직» — 코드상 둘 다 identity 인스턴스화라 차이 원인은 미확인(후속: 보너스 포탈 전용 변형·핑크 색조).
5. **공유 구조(rev 2)** — 스폰/골 포탈은 맵에 상관없이 하나의 `MarkerPropStyle` 에서 온다. 흐름: 브리지가 스테이지를 `Instantiate` → `MapStage.OnEnable` 이 `MapStage.Enabled` 를 올림(동기) → `MarkerPropInstaller.Apply` 가 `visualRoot == null` 인 `SpawnMarker`/`GoalMarker` 밑에 프랍을 identity 로 얹고 `visualRoot` 등록 → 브리지 `BuildStageMarkerRegistry` 는 이미 채워진 `visualRoot` 를 본다. 프랍은 마커의 자식이라 teardown 이 함께 지운다. 프리팹이 `visualRoot` 를 직접 채웠으면(맵 전용 연출) 그쪽이 이긴다.
   - **왜 브리지가 아닌가**: Mono↔Mono 연출이다. `BattleBridge` 는 ECS 유일 창구이고, 마커 프랍은 ECS 를 모른다 — 브리지에 두면 창구가 아닌 일이 창구에 쌓인다. 왜 `TilemapMapView` 도 아닌가: unit 4 가 그 뷰에서 구조물 프랍 코드를 뺐다(마커 뷰 소유). 왜 마커 자신이 아닌가: 마커는 선언(`런타임 로직 0`)이고 스타일 참조를 맵마다 들면 «맵에 상관없이 공유»가 깨진다. 그래서 **씬 1개 컴포넌트 + 스테이지 수명 신호**.
   - EditMode(테스트·프리뷰)에선 `OnEnable` 이 돌지 않는다 — 프리뷰는 `ApplySharedMarkerProps` 가 같은 `Apply` 를 부른다. 라이브 풀 스테이지는 **공용 프랍을 내장하지 않는다**(`MarkerPropStyleAssetTests.LivePoolStages_DoNotEmbedSharedProps` — 맵 전용 프랍으로 `visualRoot` 를 채우는 것은 허용). 설치자는 활성 서브트리만 훑고(스캐너·등록부와 같은 범위), 스윕은 자기 씬만·Play 중만 — 프리팹 스테이지/프리뷰 씬을 건드리지 않는다. `GoalMarker` 는 `visualRoot` 가 바뀌면 렌더러 캐시를 다시 짓는다(`StressTint_RebuildsRendererCache_WhenVisualRootAssignedLater`).

## 완료 기준

- [x] `GoalPortal_Yellow.prefab` 존재 · 파티클 3 · 루트 identity — 2026-08-27
- [x] Duel 프리뷰(`preview_duel_clean`): 스폰 2 빨간 수직 포탈 · 골 노란 수직 포탈 — 2026-08-27
- [x] EditMode `GoalMarkerTintTests` green(+DioramaMapBuilder·StagePoolDevEntries 31/31) — 2026-08-27
- [x] PlayMode `DioramaStagePlayTests` 3/3 — 2026-08-27
- [ ] Play 육안: 골 포탈이 스트레스에 붉어지고 붕괴 시 줄어들며 꺼진다
- **rev 2 (공유 구조)**
- [x] EditMode `MarkerPropInstallerTests` 3 · Assets `MarkerPropStyleAssetTests` 3 green — 2026-08-27 (러너 `shared-props-em-01`: 38/38, Diorama·StagePool·GoalMarkerTint 포함)
- [x] PlayMode `Markers_ReceiveSharedPortalProps_OnAnyStage` Duel·Street green (+ 기존 Diorama 3) — 2026-08-27 (러너 `shared-props-pm-01`: 5/5)
- [x] 리뷰(critic, 2026-08-27) APPROVE-WITH-NITS 반영: MAJ-1 라이브 풀 테스트를 «공용 프랍 내장 금지»로 재정의 · MAJ-2 GoalMarker 캐시 재구축 · MAJ-3 스윕 범위(자기 씬·Play·활성) + deprecated 오버로드 제거 · MIN-2/3 EnsureMarkerPropStyle 빈 슬롯만 채움·SaveAssetIfDirty · MIN-4 활성 마커만 · MIN-5 씬 배선 테스트에 활성/enabled · MIN-6 빈 슬롯 경고 · MIN-7 프리뷰 상태 `props=` · NIT 2~4
- [x] 프리뷰 4맵(`shared_props_all`): Duel·Street·Subway·StreetDay 모두 스폰 빨간 수직 · 골 노란 수직 포탈 — 2026-08-27 확인(StreetDay 의 spawn (29,0) 은 Battle 프레이밍 16:9 에서 우하단 프레임 밖 — 마커 위치/카메라 문제, 프랍과 무관)

## 후속 후보

- 텔레포트 포탈(원안 `PortalMarker` 쌍 → `PortalLink`) — 필요 시 별도 unit
- 보너스 포탈 전용 변형(핑크·기울기) — 스폰 포탈과 구분
