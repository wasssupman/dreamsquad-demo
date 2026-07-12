# Gift Phase (선물 페이즈)

> 상태: 초안 2026-07-12 (critic 리뷰 1회 반영) — 미착수

## 한 줄

배치 페이즈 **직전**에 새 `GamePhase.Gift` 를 삽입한다. "루시드의 선물"(공용 스킬 2장) 또는 "림의 선물"(무의식 2장) 이벤트가 랜덤 발생하고, 저장 덱 10장 + 선물 2장 = 12장을 발라트로식 셔플 연출로 확정해 보여준 뒤 배치로 넘긴다.

## 검증 질문

> "게임 진입 → **선물 이벤트 확정 → 12장 덱을 촤라락 셔플로 확인 → 각성 버튼으로 카드가 날아가며** → 배치" 흐름이 매끄럽게 이어지고, 확정된 12장이 실제 인게임 사이클 덱과 **정확히 일치**하는가?

## 배경 / 현행

- 현재 전투 진입 덱은 이미 사실상 **저장 10 + 랜덤 스킬 2 = 12장**이다. `DreamcatcherHandController.OnPhaseChanged(Placement)` 가 `ResolveAttachDeck()`(10) + `AppendActiveCards()`(`SkillLoadoutController.Picked` → Active 2장) 를 조합해 `DreamcatcherCycleDeck` 을 만든다.
- 이 스펙은 **"어느 2장이 붙는가"를 선물 이벤트로 결정하고, 그 결과를 연출로 보여준다.** 프레젠테이션 + 덱 조합 seam.

## 흐름

```
(Draft/Squad/Test 진입 · Restart) ─► SetPhase(Gift) ─► GiftPhaseView 연출 ─► PlacementPhaseView.BeginPlacementPhase()
                                              │
                             Lucid ▷ 스킬(Active) 2장  |  Rim ▷ 무의식 2장
```

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_phase-model.md` | 토대(additive) | `GamePhase.Gift`·`GiftKind`·`GiftConfig` SO 추가. 동작 변경 0 |
| 1 | `1_gift-deck-composition.md` | 로직 | `GiftDeckComposer` 시드 결정론 + Lucid/Rim + 폴백, HandController 가 Gift 에서 CycleDeck 생성·캐시(`Hand(12)` 연출·배치 재사용), EditMode |
| 2 | `2_subconscious-cards-authoring.md` | 콘텐츠 | 무의식 카드 2~3장 .asset + 카탈로그 배열 등록 + 덱빌더 풀 제외 |
| 3 | `3_gift-view-routing-wiring.md` | 통합 | GiftPhaseView(정적) + 진입/재시작 라우팅 hand-off + **배치 HUD 노출 트리거 재게이팅** + 씬 배선. flow 항상 성립 |
| 4 | `4_gift-sequence-core.md` | 연출 코어 | intro 텍스트·10+2 등장·**정착 12장 == 캐시**·각성버튼 fly-out→BeginPlacement |
| 5 | `5_gift-sequence-juice.md` | 연출 juice | 촤라락 셔플 비주얼·임팩트 플래시·stagger·test fast-forward·중단 leak 안전 |

## Feature-wide 계약

1. **삽입 방식**: `GamePhase.Gift` enum 값을 `Placement` **앞**에 추가. 진입 신호(`DraftConfirmed`/`PlacementRequested`)와 재시작(`BattleBridge.OnRestartRequested`)을 GiftPhaseView 가 받아 `SetPhase(Gift)` 후 연출, 완료 시 `PlacementPhaseView.BeginPlacementPhase()` 를 **명시 호출**.
2. **덱 authority 는 `DreamcatcherHandController`**. Gift 진입 시 확정 12장(순서 포함)을 계산·캐시하고, Placement 진입 시 기존 `AppendActiveCards` 대신 **캐시된 덱**을 소비. 캐시 없으면(Gift 우회) 기존 경로 폴백.
3. **연출 순서 = 실제 사이클 큐 순서 (이중 셔플 금지)**. `DreamcatcherCycleDeck` 생성자는 **항상** Fisher-Yates 를 돌린다(변경하지 않음). Gift 진입 시 **실제 CycleDeck 인스턴스를 1회 생성**하고 `deck.Hand(12)`(handSize=12=전체 큐 순서, `DreamcatcherCycleDeck.cs:51`)로 확정 순서를 읽어 연출한다. **동일 인스턴스를 배치에서 재사용** → 재셔플/재구성 없음, 순서 100% 일치. **CycleDeck 코드 무변경**.
4. **결정론**: 이벤트 선택·카드 추출·셔플 전부 **매치 시드**(`EnsureMatchSeed`, `GameManager.Start`) 기반. `MatchSeed` 는 재시작 간 고정이므로 **재시작은 동일 결과를 재생**한다(연출은 매번 재생, 결과는 결정론적으로 동일 — restartIndex 서브시드 미사용). 구조적 결정론 원칙 준수.
5. **Lucid 선물** = 기존 `SkillLoadoutController.Picked`(2 Active) 재사용. **Rim 선물** = 카탈로그 `CardCategory.Subconscious` 카드에서 시드 2장 추출, 풀<2 면 임의 폴백. **Rim 이면 롤된 Active 스킬 2장은 인핸드에 붙지 않는다** — 스킬↔무의식 교환이 의도이며 "Lucid 무회귀"는 Lucid 경로에만 적용(m3).
6. **무의식 카드는 덱빌더 10장 선택 풀에서 제외**(선물 전용). 기존 효과 채널만 사용, 신규 메커닉/채널 없음. 기존 `slow_awakening` 이 이미 저장 덱에 있으면 그대로 유지(제거만 가능, 재추가 불가·`DeckRules.Validate` 는 통과) — 마이그레이션 무해.
7. **하드코딩 수치 금지**: 이벤트 가중치·연출 타이밍·무의식 카드 값은 `GiftConfig` SO / 카드 .asset 에서.
8. **UI 는 `useUnscaledTime`**, 캔버스는 `UiCanvasSetup.Ensure` self-build. 각성 버튼 fly 타깃은 `AwakeningPanel`(배치 전까지 `SetActive(false)`)의 rect 를 직접 참조하지 않고 **고정 스크린 좌표**(우하단 `-40,220` 관례)로 계산(m4).
9. **배치 HUD 재게이팅**: `DefenderSelector`·`AwakeningGaugeView` 는 현재 진입 이벤트(`DraftConfirmed`/`PlacementRequested`)로 노출된다. C# 이벤트는 전 구독자 fan-out 이라 GiftPhaseView 가 가로막을 수 없으므로, 이들의 노출 트리거를 **`PhaseChanged(Placement)`** 로 옮긴다(선물 도중 HUD 튀어나옴 방지).

## 구조 변경 범위 (정직한 명시)

**드림캐쳐 설계(SO 스키마·카드 택소노미·덱 규칙·각성 파이프라인) 변경 0.** `DreamcatcherCard` 필드/enum, `DeckSave`/`DreamcatcherDeck`/`DeckRules`/`AwakeningConfig`, ECS 전투/모디파이어 채널, **`DreamcatcherCycleDeck`(계약 3, Hand(12) 재사용)** 모두 **불변**.

다음은 스키마가 아닌 **런타임/콘텐츠** 변경이다:
- `DreamcatcherHandController` — 덱 조합 **시점**을 Placement→Gift 로 이동 + Lucid/Rim 분기 + 확정 덱 인스턴스 캐시(원래 이 클래스가 덱 authority — 책임 범위 내).
- `DreamcatcherCard.category` — 기존 dormant 필드를 **읽기만** 시작(Rim 풀 필터·덱빌더 제외). 스키마 변경 아님, 주석만 갱신.
- `DreamcatcherDeckBuilderView` — 무의식 카드를 소유 그리드에서 제외(UI 동작).
- 신규 무의식 `.asset` 2~3장 — 기존 스키마로 콘텐츠 추가.
- `DefenderSelector`·`AwakeningGaugeView` — 노출 트리거 변경(계약 9, 드림캐쳐 설계와 무관한 UI 배관).
- ECS/시뮬 변경 **0**(BattleBridge read 경계 불변, 새 맥락/큐 0).

## 파이프라인 커버리지

드림캐쳐 카드는 보드 위 플레이 오브젝트가 아니라 **UI 프레젠테이션**이다. `docs/reference/object-pipeline-map.md` 의 생성→렌더 정거장 체크표는 **N/A** — 카드는 ECS 엔티티/SpriteRenderer 파이프라인을 타지 않고 코드 빌드 Canvas 위젯으로만 존재. 무의식 카드 신설도 .asset 저작이며 새 아키타입/정거장을 만들지 않는다.

## 후속 후보 (현 스코프 밖)

- **`ownedCardIds` 실제 보유 인벤토리** — 가챠/꿈런 파밍. 지금은 "보유 무의식 = 카탈로그 전체 Subconscious" 로 간주하고 resolver seam 만 얇게. (백로그 [L])
- **무의식 카드 대량 콘텐츠 + 신규 메커닉/채널**.
- **선물 이벤트 종류 추가**(현재 Lucid/Rim 2종 고정), **선물 리롤/선택권**.
