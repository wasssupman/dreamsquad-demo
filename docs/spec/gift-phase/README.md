# Gift Phase (선물 페이즈)

> 상태: 초안 2026-07-12 — 미착수

## 한 줄

배치 페이즈 **직전**에 새 `GamePhase.Gift` 를 삽입한다. "루시드의 선물"(공용 스킬 2장) 또는 "림의 선물"(무의식 2장) 이벤트가 랜덤 발생하고, 저장 덱 10장 + 선물 2장 = 12장을 발라트로식 셔플 연출로 확정해 보여준 뒤 배치로 넘긴다.

## 검증 질문

> "게임 진입 → **선물 이벤트 확정 → 12장 덱을 촤라락 셔플로 확인 → 각성 버튼으로 카드가 날아가며** → 배치" 흐름이 매끄럽게 이어지고, 확정된 12장이 실제 인게임 사이클 덱과 일치하는가?

## 배경 / 현행

- 현재 전투 진입 덱은 이미 사실상 **저장 10 + 랜덤 스킬 2 = 12장**이다. `DreamcatcherHandController.OnPhaseChanged(Placement)` 가 `ResolveAttachDeck()`(10) + `AppendActiveCards()`(`SkillLoadoutController.Picked` → Active 2장) 를 조합해 `DreamcatcherCycleDeck` 을 만든다.
- 이 스펙은 **"어느 2장이 붙는가"를 선물 이벤트로 결정하고, 그 결과를 연출로 보여준다.** 순수 프레젠테이션 + 덱 조합 seam. **ECS 변경 0, 드림캐쳐 SO 구조 변경 0.**

## 흐름

```
(Draft/Squad/Test 진입 · Restart) ─► SetPhase(Gift) ─► GiftPhaseView 연출 ─► PlacementPhaseView.BeginPlacementPhase()
                                              │
                             Lucid ▷ 스킬(Active) 2장  |  Rim ▷ 무의식 2장
```

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_phase-model-and-routing.md` | 토대 | `GamePhase.Gift`·`GiftKind`·`GiftConfig` SO, 진입/재시작 라우팅 |
| 1 | `1_gift-deck-composition.md` | 로직 | `ComposeGiftDeck` 시드 결정론 + Lucid/Rim 분기 + 폴백, HandController 소비 전환, EditMode 테스트 |
| 2 | `2_subconscious-cards-authoring.md` | 콘텐츠 | 무의식 카드 2~3장 .asset + 카탈로그 등록 + 덱빌더 풀 제외 |
| 3 | `3_gift-view-layout.md` | UI | GiftPhaseView 풀스크린 캔버스·페이즈 게이팅·카드 위젯(정적) |
| 4 | `4_gift-sequence-animation.md` | 연출 | 4-1~4-6 PrimeTween 시퀀스 |
| 5 | `5_scene-wiring-and-verify.md` | 배선 | GameObject/SerializeField, Play e2e 검증, handoff |

## Feature-wide 계약

1. **삽입 방식**: `GamePhase.Gift` enum 값 추가. GameManager 의 세 진입 신호(`DraftConfirmed` / `PlacementRequested`)와 재시작(`BattleBridge.OnRestartRequested`)을 가로채 먼저 `SetPhase(Gift)`, 연출 완료 시 `PlacementPhaseView.BeginPlacementPhase()` 호출.
2. **덱 authority 는 `DreamcatcherHandController`**. Gift 진입 시 확정 12장(순서 포함)을 계산·캐시하고, Placement 진입 시 기존 `AppendActiveCards` 대신 **캐시된 덱**으로 `DreamcatcherCycleDeck` 을 만든다.
3. **연출 순서 = 실제 사이클 큐 순서**. 애니메이션이 보여주는 확정 12장은 런타임 덱과 동일해야 한다(노티가 거짓이 되지 않음).
4. **결정론**: 이벤트 선택·카드 추출·셔플 전부 **매치 시드** 기반. seeded RNG 보다 프로젝트의 구조적 결정론 원칙 준수(`docs/reference/lessons`, match-seed-unification).
5. **Lucid 선물** = 기존 `SkillLoadoutController.Picked`(2 Active) 재사용. **Rim 선물** = 카탈로그 `CardCategory.Subconscious` 카드에서 시드 2장 추출, 풀<2 면 임의 폴백.
6. **무의식 카드는 덱빌더 10장 선택 풀에서 제외**(선물 전용). 기존 효과 채널만 사용, 신규 메커닉/채널 없음.
7. **하드코딩 수치 금지**: 이벤트 가중치·연출 타이밍·무의식 카드 값은 `GiftConfig` SO / 카드 .asset 에서 나온다.
8. **재시작마다 재생**: Restart 도 Gift 를 거치며 이벤트를 재추첨한다.
9. **UI 는 `useUnscaledTime`**(전투 timeScale 슬로모 영향 회피), 캔버스는 `UiCanvasSetup.Ensure` 로 self-build.

## 파이프라인 커버리지

드림캐쳐 카드는 보드 위 플레이 오브젝트(유닛/적/투사체/해저드/VFX)가 아니라 **UI 프레젠테이션**이다. `docs/reference/object-pipeline-map.md` 의 생성→렌더 정거장 체크표는 **N/A** — 카드는 EntityManager/ECS 엔티티/SpriteRenderer 파이프라인을 타지 않고 코드 빌드 Canvas 위젯으로만 존재한다. 무의식 카드 신설도 데이터(.asset) 저작이며 새 아키타입/정거장을 만들지 않는다.

## 후속 후보 (현 스코프 밖)

- **`ownedCardIds` 실제 보유 인벤토리** — 가챠/꿈런 파밍으로 무의식 카드를 실제 소유. 지금은 "보유 무의식 = 카탈로그 전체 Subconscious" 로 간주하고 resolver seam 만 얇게 남긴다. (백로그 [L] "드림캐쳐 카드 보유/콘텐츠 확장")
- **무의식 카드 대량 콘텐츠 + 신규 메커닉/채널** — 현재는 기존 채널로 2~3장 최소 저작만.
- **선물 이벤트 종류 추가** — 현재 Lucid/Rim 2종 고정.
- **선물 결과 리롤/선택권** — 현재 완전 랜덤 확정.
