# 4 — Handoff Summary (B 완료)

B(`squad-loadout`) 편성+반입 MVP 종료. 최신 계약은 README + 번호 문서 우선.

## Commit

- 0 `5487ac7` — SquadSave 7슬롯 모델 + 기본 스쿼드
- 1 `0e67b8d` — SquadDraw 순수 출전 로직 + 테스트
- 2 `321bba3` — 스쿼드 편성 UI
- 3 `813ed7d` — 전투 반입(드래프트 스킵) + smoke

## Implemented

- `SquadSave`: 7 unit-id 슬롯(빈칸 "") + IsEmpty/FilledCount/NormalizeSlots. `PlayerProfile.SelectedSquad()`.
- 신규/로드 프로필에 빈 스쿼드 1개(`squad_1`) 자동 보장 + 선택(ProfileStore).
- `SquadDraw.Resolve`: 스쿼드 + 랜덤3 → 랜덤7 (System.Random seed 결정적). 에고 특수처리 없음.
- 편성 UI(`SquadBuilderView`): SquadPanel 내 7슬롯 + 보유 그리드 런타임 생성, 배정/해제/저장. OnEnable 빌드.
- `GameManager.Start` 스쿼드 분기: 맵 Default → SquadDraw → catalog 해석 → `SetDefenderPool` → 스킬 `Roll`/`SetSkillLoadout` → `PlacementRequested` 이벤트.
- `PlacementPhaseView` 가 `GameManager.PlacementRequested` 구독(드래프트 없는 진입). 드래프트 폴백 유지.

## Key Files

- `Assets/_Project/Scripts/Core/Profile/{PlayerProfile,ProfileStore}.cs`
- `Assets/_Project/Scripts/Core/Squad/SquadDraw.cs`
- `Assets/_Project/Scripts/UI/Outgame/SquadBuilderView.cs`
- `Assets/_Project/Scripts/Core/GameManager.cs` (squad 분기), `UI/PlacementPhaseView.cs`
- `Assets/_Project/Scenes/{OutgameScene,BattleScene}.unity`
- 테스트: `Tests/EditMode/{ProfileStoreTests,SquadDrawTests}.cs`, `Tests/PlayMode/SquadCarryInSmokeTest.cs`

## Verified

- EditMode 294 (292 pass / 2 기존 ignore), PlayMode 4/4.
- Play(MCP): 편성 저장 디스크 영속, 게임시작→BattleScene phase=Placement(드래프트 스킵), defenderPool=5(스쿼드2+랜덤3).
- 새 에러 0(기존 `BattleScene/DraftView` 누락 스크립트만).

## Notes

- **되돌리지 말 것**: 에고 +1 없음(필드=랜덤7). GameManager→UI 직접참조 대신 `PlacementRequested` 이벤트. 스킬은 `SkillLoadoutController` 자체 풀 Roll.
- `PlayerProfileSO.asset` 의 직렬화 `profile` 은 빈 기본값 — BattleScene 직접 로드(테스트) 시 드래프트 폴백. 실게임은 Outgame 경유로 디스크 프로필(스쿼드) 로드.
- UI 라벨 영문(한글 폰트 후속). class 라벨/특성/조건/가챠/등급은 전부 후속.

## Follow-up

- **C `ingame-dreamcatcher`**: 준비단계 드래프트 단계 제거 + 드림캐쳐 선택(첫 배치 3중1 + 5웨이브 3중1) + StatModifier 적용. 스쿼드 모드 placement 진입 직후에 끼워넣기.
- 유닛 class 라벨 → 슬롯 조건 + 타입별 특성(스탯 합산, 하드캡 15%).
- 다중 스쿼드 수집/전환 UI, 스쿼드 가챠/꿈런 파밍/교환/리롤.
- 맵/공격패턴 선택 UI 재배치(C와 조율).
- 반복 씬 로드 ECS leak 점검(A handoff 참조).
