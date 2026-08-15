# Gift Phase Removal (선물 페이즈 제거)

> 상태: 구현 완료 2026-08-16 (units 0~3). Play 검증은 사용자 pull 후 대기. handoff: `4_handoff_summary.md`

## 한 줄

`GamePhase.Gift` 와 그 연출을 통째로 걷어내고, **드림캐쳐 주입(저장덱 10 + 공용 Active 2 = 12장)만** 남긴다. 루시드/림 분기를 없애 추가 2장은 **항상 Active 스킬**이며, 매치는 기믹 페이즈부터 시작한다. 진입로를 잃는 무의식 카드는 일반 덱 카드로 승격한다.

## 검증 질문

> "매치 시작 → **기믹 리빌 → 배치**" 가 선물 연출 없이 이어지고, 손패가 **저장덱 10 + Active 2 = 12장**으로 매번 정확히 구성되는가? 무의식 카드를 덱 페이지에서 직접 골라 넣을 수 있는가?

## 배경 / 현행

- `GiftPhaseView` 가 진입 신호를 받아 `SetPhase(Gift)` → 덱 조합 → ~6초 연출 → `GimmickPhaseView.BeginReveal(cb)` → `BeginPlacementPhase()` 순으로 넘긴다. **기믹 리빌의 유일한 진입점이 선물 뷰 안에 있다** — 선물을 지우면 기믹도 같이 끊긴다.
- 덱 조합의 정상 경로는 `BuildGiftDeck()`(Lucid/Rim 분기)이고, Gift 우회용 `BuildFallbackDeck()`(저장 10 + Active 2)이 이미 **이번 스펙이 원하는 동작 그대로**다. 폴백을 정상 경로로 승격하는 것이 이 작업의 골자다.
- 무의식(`CardCategory.Subconscious`) 카드 6장은 림의 선물이 유일한 진입로였다. 림이 사라지면 고아가 되므로 덱빌더 제외 필터를 풀어 일반 덱에 편입한다.

## 흐름

```
[변경 전]  RequestPlacement / 재시작 ─► GiftPhaseView(SetPhase(Gift) + 덱조합 + 연출) ─► GimmickPhaseView ─► Placement
[변경 후]  RequestPlacement / 재시작 ─► GimmickPhaseView(기믹 리빌) ─► Placement(진입 시 덱 12장 조합)
```

실전 진입 신호는 `SquadPrepView`(MAP SETUP 확정) → `GameManager.RequestPlacement()` → `PlacementRequested` 하나로 수렴한다. 재시작(`BattleBridge.EnterPlacementOrGift`)은 result-screen-lobby-exit unit 0 이후 호출처가 없는 **dormant 경로**지만 배선은 그대로 이관한다.

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_subconscious-normal-deck.md` | 데이터/UI | 무의식 카드 6장을 일반 덱 선택 풀로 승격 (제외 필터 제거). 선물과 독립 — 먼저 넣어도 무해 |
| 1 | `1_intro-routing-and-deck.md` | 라우팅/로직 | `GimmickPhaseView` 가 진입 소유 + `HandController` 덱 조합 단일화 + `GamePhase.Gift` 제거. **원자적 한 커밋** |
| 2 | `2_gift-code-asset-removal.md` | 삭제 | 죽은 선물 코드 5개·에셋·테스트 2개·씬 오브젝트 제거 |
| 3 | `3_tutorial-cleanup.md` | 튜토리얼 | 선물 워크스루 챕터 + `TutorialProgress` Gift API 제거 |
| 4 | `4_handoff_summary.md` | 인계 | 구현 종료 시 작성 |

### unit 1~3 은 한 커밋으로 합쳤다 (2026-08-15 구현 중 정정)

계획에서는 unit 1 시점에 `GiftPhaseView` 가 "죽은 코드로 남지만 컴파일은 된다"고 봤다. **틀렸다.** `GiftPhaseView` 와 `FirstSessionTutorialController.Gift.cs` 가 unit 1 에서 제거할 `HandController` API(`GiftDeckReady`·`GiftKind`·`GiftBaseCards`·`GiftAddedCards`·`GiftFinalOrder`)와 `TutorialProgress.ShouldRunGiftTutorial` 을 **직접 참조**하므로, 셋을 나눠 커밋하면 어느 순서로도 중간 커밋이 컴파일되지 않는다.

제거 작업의 실제 원자 단위는 **라우팅 + 덱 + 뷰 삭제 + 튜토리얼**이다. 파일별 계약은 각 문서에 그대로 유효하고, 커밋만 하나로 합쳤다.

## Feature-wide 계약

1. **진입 신호는 `PlacementRequested` + 재시작 2개뿐.** 드래프트(`DraftController.DraftConfirmed`) 경로는 **신설하지도 복원하지도 않는다** (사용자 결정 2026-08-15 — 오래 쓰이지 않는 경로다). 기존 `DraftView` 구독은 건드리지 않는다.
2. **`GimmickPhaseView` 가 매치 인트로 진입을 소유한다.** `placementPhaseView` 를 SerializeField 로 갖고 연출 종료·스킵 어느 경로로든 스스로 `BeginPlacementPhase()` 를 호출한다. 내부 `_onDone` 의 **"어떤 경로로든 정확히 한 번"** 보장(`OnDisable` 포함)은 구조 그대로 유지 — 유실되면 배치가 영영 시작되지 않는 이 뷰의 단일 최대 위험이다.
3. **덱 조합은 Placement 진입 단일 경로**다. 매 배치 진입마다 새로 구성(gift-phase 이전 불변식 복귀)하며 캐시·재사용 플래그를 두지 않는다. 구성은 저장덱 10 + Active 2 = 12장, `MatchSeed` 로 `DreamcatcherCycleDeck` 단일 Fisher-Yates. `DreamcatcherCycleDeck` **무변경**.
4. **Lucid/Rim 분기 제거.** 추가 2장의 유일한 출처는 `SkillLoadoutController.Picked` → `activeCards` 래핑이다. 매핑 누락 시 기존대로 경고 후 짧은 큐로 진행(동작 변경 없음).
5. **무의식 카드는 일반 덱 카드다.** 덱빌더/프리셋의 `category == Subconscious` 제외를 제거하고 `DeckRules`(덱 10장, Squad ≤2) 를 그대로 적용한다. `CardCategory.Subconscious` **값 자체는 존속** — 이제 덱 규칙이 아니라 보라 프레임 + "무의식" 칩(`CardCategoryStyle`)이라는 시각 라벨 전용이다.
6. **첫 판 기믹 리빌 스킵은 현행 유지.** `TutorialProgress.ShouldRunCore` 게이트와 기믹 미배정·config 미배선 스킵 3조건 모두 그대로.
7. **선물 튜토리얼 챕터는 제거하되 `PlayerProfile.giftTutorialVersion` 필드는 유지**한다(하위호환). `ResetAll`/`ResetAllInJson` 의 초기화 처리도 유지 — 기존 세이브에 남은 값을 계속 정리한다.
8. **`GamePhase.Gift` enum 값 제거 + 직렬화 에셋 동시 마이그레이션.** ⚠ 계획 단계의 "직렬화된 참조 없음" 판단은 **틀렸다** — `CameraDirectionConfig` 가 이 enum 을 int 로 에셋에 박아 둔다(`CameraPhasePose.phase` 4개 + `breathPhases` 3개). 첫 grep 이 `public Wassup.Core.GamePhase[]` 를 네임스페이스 접두사 때문에 놓쳤고, 정작 `GameManager` 의 기존 주석이 이 사실을 경고하고 있었다. 값을 빼면 저장된 정수의 의미가 밀리므로(배치→전투, 전투→결과) **같은 커밋에서 에셋을 옮긴다**: `1/3/4/5 → 1/2/3/4`, `breathPhases 1,3,4 → 1,2,3`.
9. **ECS/시뮬 변경 0.** 새 맥락·NativeQueue 채널·`BattleBridge` 읽기 경계 변화 없음. `BattleBridge` 는 SerializeField 1개 교체만.
10. **테스트 케이스 정리는 코드 제거와 같은 커밋에서** 한다. Unity 는 테스트 어셈블리를 통째로 컴파일하므로 케이스 하나가 사라진 API 를 참조하면 EditMode 전체가 깨진다. 해당 지점은 unit 1 의 `SkillLoadoutControllerTests`(리플렉션 `ResolveRimGift`)와 unit 2 의 `DreamcatcherCatalogSyncTests`(`GiftDeckComposer.PickRim`) 두 곳이다.

## 파이프라인 커버리지

드림캐쳐 카드는 보드 위 플레이 오브젝트가 아니라 **UI 프레젠테이션**이다. `docs/reference/object-pipeline-map.md` 의 생성→렌더 정거장 체크표는 **N/A** — 카드는 ECS 엔티티/SpriteRenderer 파이프라인을 타지 않고 코드 빌드 Canvas 위젯으로만 존재한다. 이번 작업은 오브젝트를 신설하지 않고 **제거**만 하므로 새 아키타입/정거장도 없다.

## 후속 후보 (현 스코프 밖)

- **무의식 카드 밸런스 재조정** — 선물 전용(랜덤 지급)을 전제로 잡힌 강도·페널티가 자유 선택으로 바뀐다. 특히 희생계약(`leakAllowanceCost: 1`, 환불 없음)과 Squad 캡을 먹는 금 간 성배.
- **재시작 경로 복원 시 재확인** — `OnRestartRequested` 가 되살아나면 기믹 리빌이 재시작마다 재생될지(현재 설계상 재생됨) 결정 필요.
- **배치 전 인트로 연출이 또 늘어날 때** — 지금은 기믹 하나뿐이라 뷰가 직접 소유하지만, 두 개 이상이 되면 얇은 라우터 분리를 재검토한다.
- **`giftTutorialVersion` 필드 최종 제거** — 레거시 세이브가 충분히 정리된 뒤.
