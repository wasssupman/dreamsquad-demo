# 4 — Handoff Summary — gimmick-match-integration

세션 인계 지도. 최신 계약은 README + 번호 문서 우선.

## Commit

- `e3f9c776` docs — 스펙(review rev1)
- `4484b729` unit 0 — BattleConfig SO + GimmickData.description + 기본 에셋
- `113c2abf` unit 1 — 시즌 분리 + 랜덤 배정 + BattleBridge 3곳 스왑 + GM 배선 + 결정론 테스트
- `c9b5f3d5` unit 2 — GimmickGuideView 배치 안내 카드 + 씬 배선
- (unit 3 = 검증 매트릭스, 코드 커밋 없음. 이 docs 커밋에 포함)

## Implemented

- 기믹이 시즌 결합에서 분리됨: 소스 = `BattleConfig{ gimmickEnabled, gimmickPool }`. `SeasonData.gimmick` 제거, 시즌은 맵 테마 전담.
- 매치당 1회 배정: `GameManager.AssignGimmick`(EnsureMatchSeed 직후, 모든 진입 경로 공통). 결정론 `GimmickSelection.PickIndex` + `MatchSeed.DeriveGimmickSeed`. enabled/pool 게이트, empty-pool 경고, Restart 는 초기 배정 유지.
- BattleBridge 3개 소비 지점(config 주입 L4164·픽업 스폰 게이트 L647·디버그 로그 L3878) 모두 `_assignedGimmick` 소비. `SetAssignedGimmick` seam.
- `GimmickGuideView`: 배치 페이즈 상시 안내 카드(제목=displayName, 본문=description). PhaseChanged 구독 + enable-sync. 상단 중앙(배너 아래), sortingOrder 8, raycastTarget=false → 좌상단 메뉴버튼 회피 + 입력 비차단. AssignedGimmick==null 이면 미표시.
- `BattleConfig.asset`(enabled=true, pool=[Gimmick_Overwork]) + `Gimmick_Overwork.description` 기입.

## Key Files

- 데이터: `Data/BattleConfig.cs`, `Data/Config/BattleConfig.asset`, `Data/Gimmick/GimmickData.cs`(description)
- 배정: `Core/GameManager.cs`(battleConfig/AssignGimmick/AssignedGimmick), `Core/GimmickSelection.cs`, `Core/MatchSeed.cs`(DeriveGimmickSeed)
- 주입: `Bridge/BattleBridge.cs`(_assignedGimmick + 3곳 스왑)
- UI: `UI/GimmickGuideView.cs`
- 씬: `Scenes/BattleScene.unity`(GameManager.battleConfig 배선 + GimmickGuideView GO)

## Verified

- compile idle, 에러 0(전 유닛). EditMode **813 pass**(GimmickSelection 4), skip 2(기존 무관).
- Play ON: `gimmick=G1_Overwork` 배정 + `PickupSpawnState built(123셀)` + 레드불 픽업 렌더(스크린샷) + 런타임 에러 0.
- Play OFF(`gimmickEnabled=false`): `gimmick=none` + PickupSpawnState 미주입 + 클린 forest. 검증 후 true 복구.

## Notes

- **미확인(사용자 육안 필요)**: 배치 페이즈에서 안내 카드의 실제 렌더(레이아웃/줄바꿈/메뉴 비가림). draft→gift→placement 진입은 UI 클릭이 필요해 MCP(execute_code 불가)로 자동 구동 못 함 — 빌드·배선·로직·hidden-outside-placement 는 검증됨. `topOffset`(176)/`cardWidth`(640) 는 serialized 라 인게임 튜닝 가능. Score HUD 와 겹치면 topOffset 조정.
- 공유 파일: `BattleBridge.cs`(내 hunk만), `BattleScene.unity`(battleConfig·GimmickGuideView GO만 선별 스테이징 — 타 세션 default-fill `depthParallaxSettings`/`enableAdjacencySynergy` 2줄은 미스테이지로 남김).
- 전투 시작 `OverworkGimmickConfig 주입`(L4164)은 픽업 게이트와 동일 `_assignedGimmick` 소스라 by-construction 보장(ON 경로 픽업 로그가 소스 정상 세팅 증명).
- 현재 pool = Overwork 1개. 비-Overwork pick 은 `is OverworkGimmickData` 로 no-op(설계상, 제약 9).

## Follow-up

- 사용자 육안: 배치 페이즈 안내 카드 확인(위 미확인 항목).
- (선택) assign→inject PlayMode 스모크 테스트.
- 기믹 종류 2번째 추가 시 `effect-trigger-unification`(파킹) 착수 검토.
- `BattleConfig` 시트 임포터, 안내 카드 아이콘/연출.
