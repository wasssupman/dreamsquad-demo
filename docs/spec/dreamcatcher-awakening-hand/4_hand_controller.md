# 4 — DreamcatcherHandController + 구 3중1 플로우 dormant

## 목적

게이지·12장 순환덱·적용 API 를 묶는 매치 컨트롤러를 세우고, 구 3중1 트리거를 배선 해제한다. UI 는 아직 없음 — 공개 API 와 이벤트만 제공(뷰는 unit 5~8 이 구독).

## 변경 대상

- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs` (신규)
- 씬: `DreamcatcherController` 컴포넌트 비활성(dormant) + 신규 컨트롤러 GameObject 배선 (unity-feature-wiring)

## 구현

1. **참조**: `BattleBridge`, `PlayerProfileSO`, `DreamcatcherCardCatalog`, `DreamcatcherDeck`(폴백), `AwakeningConfig`, `SkillLoadoutController`, **`DreamcatcherCard[] activeCardBySkill`**(unit 2 의 Active 카드 6종 — `card.skill` 매칭으로 롤 결과를 카드로 변환).
2. **라이프사이클**: `GamePhase.Placement` 진입 시 매치 상태 리셋 —
   - 부착덱 resolve: 구 컨트롤러의 `ResolveDeck` 패턴 이식(세이브덱 검증 → catalog 해석 → serialized 폴백). 구 코드는 수정하지 않는다(dormant 원칙).
   - Active 2장: `SkillLoadoutController.Picked`(기존 롤 결과 — 시드·로그 흐름 그대로) → `activeCardBySkill` 매핑. 매핑 없는 스킬은 warn + 스킵. **Active 가 2장 미만이어도 경고 후 있는 만큼만 주입해 진행**(큐 = 10+α, critic M2 — 손패/순환 로직은 큐 크기에 무의존).
   - `DreamcatcherCycleDeck(부착10 + Active, seed)` 생성 — seed 는 기존 매치 시드 체계(`GameManager.MatchSeed`) 재사용.
   - **리셋 불변식(critic M3)**: 매 Placement 진입마다 ① 새 CycleDeck ② **부착 레지스트리 clear** ③ `Gauge = gaugeStart` ④ 이벤트 구독은 OnEnable/OnDisable 대칭으로 중복 구독 방지.
3. **게이지**: `int Gauge` + bridge `EnemyKilledAwakening`/`DefenderDied` 구독 가산(`min(g+r, gaugeMax)`), `event Action<int> GaugeChanged`.
4. **사용 API** (unit 7~8 드래그가 호출; 커밋 시점에 호출됨 — pending 은 뷰 소관):
   - `CanUse(entryId)` — 손패 포함 + `Gauge >= CostFor(card.type)` + (Unit 타입) 대상 부착 수 < `maxAttachPerUnit`.
   - `CommitSquad(entryId)` — 검증 → `bridge.ApplyDreamcatcherCard(card)` → 차감 → `deck.UseAndRecycle`.
   - `CommitUnit(entryId, Entity target)` — 검증(부착 상한 포함) → `bridge.ApplyDreamcatcherCardToUnit(target, card)` **성공 시에만** 차감 + `deck.UseUnit` + 부착 레지스트리(`entryId ↔ entity`) 등록. 실패 시 무차감·무순환.
   - Active 커밋은 **타겟 형태별 3개 시그니처**(제네릭 인자 금지, critic 지적): `CommitActiveTile(entryId, Vector2Int cell)` → `CastSkillAtTile` / `CommitActiveDefender(entryId, Vector2Int cell)` → `CastSkillOnDefender`(기존 API 가 cell 을 받는 비대칭 주의 — Unit 카드의 Entity 와 다름) / `CommitActivePortal(entryId, Vector2Int entry, Vector2Int exit)` → `CastPortal`. 성공 시에만 차감 + `UseAndRecycle`. **SkillRuntime 쿨다운·CostRuntime 미사용**(계약 7 — bridge `skillRuntime` 배선 해제는 unit 8).
   - `event Action HandChanged` — 사용/회수 시 발화(사유 enum 포함: Used/Recovered — 뷰의 자동 복귀 판단용).
5. **회수**: `DefenderDied(entity, data)` 구독 → 레지스트리에서 entity 의 entryId 전부 `deck.Recover` → 레지스트리 제거 → `HandChanged(Recovered)`.
6. **일시정지 금지**: pause lease 사용하지 않음. (슬로모 lease 는 뷰(unit 6) 소유.)
7. **dormant 전환**: BattleScene 의 `DreamcatcherController` 컴포넌트 disable + `DreamcatcherSelectionView` 비활성(참조·에셋 유지). 덱 로그(`SetDreamcatcherDeck`)는 새 컨트롤러가 이어서 호출(offer/pick 로그 대체는 후속 후보).

## 완료 기준

- [ ] 컴파일 클린. Play 진입 시 구 3중1 모달 미출현, 배치/전투 정상.
- [ ] 적/아군 사망 시 `GaugeChanged` 상승 확인(로그), 상한 100 고정.
- [ ] 테스트 훅(컨텍스트 메뉴/임시 로그)으로 Commit 3종 호출 → 효과 적용 + 차감 + 순환 확인. Active 는 스킬 집행(예: Meteor 착탄) 확인.
- [ ] defender 사망 → 부착 entryId 큐 맨 뒤 복귀. 유닛당 4번째 부착 시도 거절.
