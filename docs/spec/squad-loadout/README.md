# squad-loadout (B)

> 상태: 완료 2026-06-02 · **rev 2026-06-05 (출전 결정화)**
> 선행: A `outgame-scene-and-flow` (완료). 후속 C `ingame-dreamcatcher` 와 함께 인게임 드래프트 대체.
> 커밋: 0 `5487ac7` · 1 `0e67b8d` · 2 `321bba3` · 3 `813ed7d`. handoff → `4_handoff_summary.md`.
>
> **rev 2026-06-05**: 출전 편성을 **결정적**으로 변경. 기존 "스쿼드 + 랜덤3 → 랜덤7" 은 매 게임 라인업이 달라져 "저장한 스쿼드가 안 지켜진다"는 혼란을 유발. 이제 **저장 스쿼드를 그대로 반입**(비어있지 않은 슬롯, 중복 제거, 순서 보존, 최대 7). 랜덤 fill/셔플/seed 제거. 상세 `1_squad_draw.md` rev 절.

## 검증 질문

OutgameScene에서 **7슬롯 스쿼드를 편성·저장**하고 게임을 시작하면, BattleScene이 **드래프트 UI 없이** 그 **저장 스쿼드 그대로**(rev 2026-06-05 — 가변 랜덤 제거) 유닛으로 배치/전투를 진행하는가? 스쿼드 미설정 시 기존 드래프트로 폴백하는가?

## 상위 목표

A가 깐 프로필 영속/씬 흐름 위에, 드래프트의 "유닛 선택" 역할을 **스쿼드 편성 + 반입**으로 대체한다. 스코프는 **편성 + 반입 MVP**: 특성/조건/가챠/파밍/등급/class 라벨은 전부 후속.

## 작업 단위

| 파일 | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | 데이터 | `0_squad_data_model.md` | `SquadSave`(7 unit-id 슬롯+이름), `selectedSquadId`, 기본 스쿼드 1개 자동 생성 |
| 1 | 로직 | `1_squad_draw.md` | 순수 SquadDraw: 스쿼드7 + 랜덤3 → 랜덤7. seed 결정적 + EditMode 테스트 |
| 2 | UI | `2_squad_builder_ui.md` | SquadPanel 내용: 보유 유닛 그리드 + 7슬롯 배정/해제/저장/선택 |
| 3 | 통합 | `3_battle_carry_in.md` | GameManager.Start 스쿼드 분기: 드래프트 스킵 → SquadDraw → SetDefenderPool → Placement. PlayMode smoke |
| 4 | 인계 | `4_handoff_summary.md` | handoff |
| 5 | 회귀/UX | `5_map_setup_step.md` | 인게임 회귀 수정(맵 스타일·프랍·유닛선택 UI) + 배치 이전 맵 설정 스텝(MAP SETUP) |

## Feature-wide 계약

- **에고 특수처리 없음**: 스쿼드 7슬롯이 곧 필드 후보. 출전 = 랜덤 7 (별도 +에고 없음). Bruiser 를 넣고 싶으면 슬롯에 배정.
- **출전 규칙 (rev 2026-06-05, 결정적)**: 저장 스쿼드의 비어있지 않은 유닛을 **그대로** 반입(중복 제거, 슬롯 순서 보존, 최대 7). 랜덤 fill/셔플/seed 없음 → 매 게임 동일. (이전: 스쿼드+랜덤3→랜덤7. 폐기.)
- ~~**가변 3 풀**~~: rev 2026-06-05 제거.
- **기본 스쿼드 (rev 2026-06-05)**: 신규/스쿼드 없는 프로필은 7슬롯 스쿼드 1개 자동 생성 + 선택. **선택 스쿼드가 비어 있으면 보유 유닛 앞 N(≤7)개로 시드**(playable starter). 신규 설치/구버전 실행 기기에서 빈 스쿼드 → 드래프트 폴백으로 빠지던 문제 해소. 채워진 스쿼드는 덮어쓰지 않음. (이전: 빈 7슬롯, 플레이어가 직접 채움.)
- **드래프트 폴백 유지**: `selectedSquadId == null/""` 또는 선택 스쿼드가 비었으면 GameManager 는 기존 드래프트(A 폴백). 비파괴.
- **스킬은 유닛과 독립**: 스쿼드 모드도 `SkillLoadoutController.Roll()` 로 스킬 2개 roll 후 `SetSkillLoadout`. (드래프트 BeginDraft 가 하던 일을 분기에서 수행.)
- **맵은 기본값**: 스쿼드 모드는 `MapGenerationOptions.Default`. 맵/공격패턴 선택 UI 재배치는 C(prep-flow).
- **class 라벨/특성/조건/가챠 금지**: 본 spec 범위 밖. 후속.

## 후속 후보 (B 범위 밖)

- 유닛 class 라벨(ranger/guardian/bruiser) + 슬롯 조건 + 타입별 특성(스탯 합산, 하드캡 15%)
- 스쿼드 가챠/꿈런 파밍/교환 아이템/등급/리롤, 스쿼드 여러 개 수집·전환 UI
- 맵/공격패턴 선택 UI 를 Outgame 또는 prep 단계로 재배치 (C와 조율)
- ~~코어 슬롯 확정(7슬롯 중 3 코어) 등 랜덤 완화 변형~~ → rev 2026-06-05 에서 완전 결정화로 해소(랜덤 제거). 다시 변동성을 원하면 별도 spec.
