# ingame-dreamcatcher (C)

> 상태: 초안 (작성 2026-06-02)
> 선행: A `outgame-scene-and-flow`, B `squad-loadout` (완료). 원래 요청의 마지막 핵심 조각.

## 검증 질문

인게임에서 **첫 배치 시점**과 **5웨이브마다** 덱 10장 중 3장이 노출되고, 1장 선택 시 **조건에 맞는 아군 유닛 전체(현재+이후 배치)** 에 스탯 버프가 매치 끝까지 적용되는가? 드래프트 유닛 선택 단계 없이 동작하는가?

## 상위 목표

스쿼드(B)가 유닛 풀을 대체한 위에, 준비/전투 중 **드림캐쳐 카드 선택 → 효과 적용** 흐름을 붙인다. 스코프는 **스탯% 6종 + 고정 기본 덱 + 인게임 3중1 선택 MVP**. 복합 메커닉(splash/summon/crit/pierce/taunt/match-cost), 무의식, 세이브덱 빌더(D)는 후속.

## 작업 단위

| 파일 | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | 데이터 | `0_unit_class_labels.md` | `DefenderClass` enum + `DefenderUnitData.role` + 15유닛 백필 |
| 1 | 데이터 | `1_card_data_and_deck.md` | `DreamcatcherCard` SO(축+효과) + 6 카드 에셋 + 고정 10장 덱 |
| 2 | 적용 | `2_effect_application.md` | BattleBridge active-effects registry + `EnqueueDmgTakenMul` + 축 매칭 + 현재/미래 적용 |
| 3 | 흐름 | `3_selection_flow.md` | 첫 배치 / 5웨이브 트리거(이벤트) + `DreamcatcherController` 3중1 추첨 |
| 4 | UI | `4_selection_ui.md` | 3카드 선택 모달(런타임 빌드) |
| 5 | 인계 | `5_handoff_summary.md` | (종료 시) |

## Feature-wide 계약

- **효과 = 스탯% 6종 / 4채널**: ATK%→DamageMul, AS%→AttackSpeedMul, **HP%→DmgTakenMul 프록시**(받는 피해 감소 = 실효 체력), Move%→MoveSpeedMul. max-HP 미변경(ECS Health 맥락 비침습).
- **타겟 축(MVP)**: `ClassRanger`, `ClassGuardian`, `Cost1`. `DefenderUnitData.role`(신규) + `cost`(기존) 로 판정.
- **class 5종**: Ranger / Guardian / Bruiser / Caster / Support (드캐는 Ranger/Guardian 만 사용; 나머지는 후속/스쿼드 특성용).
- **매치 영구 + 현재·미래 유닛**: 선택 시 현재 매칭 유닛 + 이후 `PlaceDefenderAs` 로 들어오는 매칭 유닛 모두 적용. BattleBridge `_activeDreamcatcherEffects` registry. duration = 매치 길이(대용량).
- **중복 카드 스택**: 같은 카드 2장 선택 시 stat 누적되도록 선택마다 고유 `stackId`(StatModifier merge key 분리).
- **MonoBehaviour 적용 경로만**: 기존 `BattleBridge.EnqueueXxxMul` 패턴 + 신규 `EnqueueDmgTakenMul`. 새 NativeQueue/맥락 없음(Effects 채널 재사용).
- **트리거**: 첫 `PlaceDefenderAs` 1회 + `QueueWave` 에서 (waveIndex+1)%5==0. BattleBridge 가 이벤트 발화, `DreamcatcherController` 구독(없어도 무해).
- **고정 기본 덱**: C는 코드/에셋 내장 10장. 세이브덱 빌더는 D.
- **비파괴**: DreamcatcherController 미배치 시 기존 흐름 그대로. 드래프트 폴백도 영향 없음.

## 후속 후보 (C 범위 밖)

- 복합 효과: row-only, crit, projectile pierce/splash, lowcost summon, guardian taunt-range, match-start cost (+신규 메커닉/채널)
- 무의식 2장 + 꿈런 강제 편입/봉인 표시
- D `dreamcatcher-deck-builder`(10장 세이브덱 UI) + `PlayerProfile.dreamcatcherDecks`
- 진짜 MaxHealthMul 채널(HP 프록시 → 정확 max-HP)
- 선택 중 일시정지를 timeScale 대신 MovementPauseRequest 채널로
