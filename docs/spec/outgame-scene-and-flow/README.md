# outgame-scene-and-flow

> 상태: 진행 중 (시작 2026-06-02)
> 상위 분해 A. 후속 B(squad-loadout) · C(ingame-dreamcatcher) · D(dreamcatcher-deck-builder) 의 기반.

## 검증 질문

OutgameScene에서 **게임 시작** → BattleScene 로드 → 전투 → 메인 복귀가 동작하고, **스쿼드/드림캐쳐 버튼**이 placeholder 패널을 열며, **플레이어 프로필이 디스크에 JSON으로 영속**되는가?

## 상위 목표

단일 `BattleScene` 구조를 **Outgame(메뉴) ↔ Battle(전투)** 2-씬 구조로 분리한다. 씬 간 상태를 들고 다닐 영속 저장 기반(JSON + ScriptableObject 홀더)을 깔아 B/C/D 가 슬롯-인 할 수 있게 한다. **A 는 비파괴적**: BattleScene 의 드래프트/전투는 그대로 두고(셀프 폴백), 껍데기와 흐름만 추가한다.

## 작업 단위

| 파일 | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 데이터 모델 | `0_player_profile_data.md` | `PlayerProfile`/`PlayerProfileSO`, `DefenderUnitData.id`+백필, `DefenderCatalog` |
| 1 | 영속 | `1_profile_store_persistence.md` | static `ProfileStore` JSON Load/Save + EditMode 테스트 |
| 2 | 씬/UI | `2_outgame_scene_and_menu.md` | `OutgameScene` + Canvas + `OutgameMenuController` + 3버튼 + placeholder |
| 3 | 흐름 | `3_scene_flow_and_gamemanager.md` | 씬 전환, GameManager 비영속화, 메인 복귀, 빌드세팅, PlayMode smoke |
| 4 | 인계 | `4_handoff_summary.md` | (종료 시 작성) |

## Feature-wide 계약

- **부팅 씬 = OutgameScene** (빌드 index 0). BattleScene 은 거기서 로드된다.
- **매니저 싱글톤은 GameManager 1개만**(제약 5). 새 싱글톤 금지. `ProfileStore`=static 유틸, `PlayerProfileSO`=데이터 에셋.
- **GameManager 는 전투 전용·비영속**: `DontDestroyOnLoad` 제거. 매 전투마다 BattleScene 과 함께 새로 생성·파기.
- **씬 간 상태 캐리어 = `PlayerProfileSO`** ScriptableObject 에셋. 두 씬이 참조, 메모리에 유지. B/C/D 가 여기에 선택 로드아웃을 써넣는다.
- **영속 = JSON** (`Application.persistentDataPath/profile.json`). 유닛/카드는 **안정 string id** 로 참조(에셋 GUID·인덱스 아님). `schemaVersion` 으로 마이그레이션 대비.
- **A 단계 폴백**: `PlayerProfileSO.selectedSquad == null` 이면 GameManager 는 기존 드래프트 플로우로 진행. 드래프트 제거는 C 범위.
- **씬 wiring 은 UnityMCP 자동화 + Play 검증까지** 가 완료(수작업 미룸 금지).

## 후속 후보 (A 범위 밖)

- 스쿼드 7슬롯 편성 UI · 저장 · 7+3 랜덤 출전(랜덤7) — **B `squad-loadout`**
- 인게임 드래프트 단계 제거 → 드림캐쳐 선택(첫 배치 3중1 + 5웨이브마다 3중1) + StatModifier 적용 — **C `ingame-dreamcatcher`**
- 드림캐쳐 10장 세이브덱 빌더 UI — **D `dreamcatcher-deck-builder`**
- 유닛 class 라벨(ranger/guardian/bruiser) — B 진입 시 추가
- 스쿼드 특성/가챠/꿈런 파밍/등급/리롤, 드림캐쳐 무의식 편입 — 메타 진행, 후속 spec
