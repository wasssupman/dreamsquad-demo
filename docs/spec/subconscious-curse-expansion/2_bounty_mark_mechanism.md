# 2. 살찌운 제물 — 표식/보상/회수 메커니즘

## 목적

필드의 악몽 1체에 표식: 즉시 받는 피해 −30%(적이 튼튼해짐 — 리스크 선불), 처치 시 각성치 ×3(리턴은 잡아야만), 유출 시 무보상 회수. **적을 겨냥하는 최초의 드림캐쳐** — 이 unit 은 메커니즘만 (API 직호출 검증), 드래그 UX 는 unit 3.

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — `DcPayloadKind.BountyMark = 15` append + 필드 해석 주석
- `Assets/_Project/Scripts/Battle/Units/EnemyKilledEvent.cs` — `public Entity entity` append (awakeningReward 선례) + enqueue 지점(`DamageApplicationSystem`, Units 소유) 채움
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — `ApplyBountyMark(Entity enemy, DreamcatcherCard card)` + 표식 등록부 + `event System.Action<Entity> EnemyGone`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — EnemyKilled/GoalReached 드레인에서 표식 등록부 조회 → `EnemyGone` 발화 + empower reconcile 주석(`:1109` "적이 드림캐쳐 origin 슬롯을 가질 일은 없지만") 갱신 — 본 카드가 그 전제를 깨는 최초 사례 (critic m6)
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs` — `CommitMarkEnemy(entryId, enemy)` + `EnemyGone` 구독 회수
- `Assets/_Project/Data/Dreamcatcher/Card_FattenedOffering.asset` 신규 + 카탈로그 등록
- PlayMode 테스트

## 구현

**카드 인코딩** — `id=sub_fattened_offering`, `displayName=살찌운 제물`, `type=Unit`, `category=Subconscious`, `axis=All`,
`mechanics=[{ trigger: None, payload: BountyMark, magnitude: 3.0 (각성 배율), tileRange: 30 (받는 피해 감소 %) }]`
(tileRange 정수 재사용은 ApplyStackToTarget 의 maxStack 선례 — DcMechanic 주석에 명문화.)

**ApplyBountyMark**:
1. preflight — ECS live + enemy 존재 + **적 판별**(DefenderUnitTag 부재 + 적 태그 실측 확인) + 등록부에 이미 있으면 거절(-1, 무차감). 이중 표식 = AwakeningReward 이중 배율이라 반드시 사전 거절.
2. `AwakeningReward.value ×= magnitude` (반올림, ≥1 보장) 덮어쓰기 — **처치 경로 무수정**: `EnemyKilledEvent.awakeningReward` 가 enqueue 시점에 이 값을 복사하므로 기존 `EnemyKilledAwakening` 흐름이 배율된 보상을 자동 지급한다.
3. `EnqueueStatModifier(enemy, DmgTakenMul, 1 − tileRange/100f, DcDuration, sid, origin=Dreamcatcher)` — 기존 채널. `DamageApplicationSystem:77` 이 victim 의 dmgTakenMul 을 이미 소비. empower aura 는 defender 한정 쿼리(`BattleBridge.cs:1110-1112`, `StatModifierSlot + DefenderUnitTag`)라 적에겐 안 켜짐(실측 확인).
   - **모디파이어 수명 계약**: TTL=DcDuration(영구), `_activeDcEffects`/revoke 레지스트리에 **등록하지 않는다** — 소멸은 오직 엔티티 수명(사망/유출)과 함께. 회수 로직 불필요가 의도다 (critic gap 명문화).
   - **불변식 주의 (critic m6)**: 이 mark 는 적에 Dreamcatcher-origin 모디파이어를 얹는 **최초 사례**. 향후 origin 기반 판정(오라/dispel/UI)을 추가할 때 반드시 진영/태그 게이트를 유지할 것.
4. 등록부(entity 키) 기록. `BeginPlacement` 에서 clear(기존 레지스트리 선례).

**회수 (기존 드레인 2곳 훅)**:
- 처치: EnemyKilled 드레인 — append 된 `entity` 로 등록부 조회 → `EnemyGone(entity)` 발화 + 등록부 제거. 보상은 2에서 이미 baked.
- 유출: GoalReached 드레인 — `GoalReachedEvent.entity` (기존 필드) 동일 처리. 보상 없음(자연).
- 유의: `DrainGoalEvents` 는 패배 트리거 시 조기 return(`BattleBridge.cs:3103`) — 같은 프레임 뒤 큐의 표식 EnemyGone 이 미발화할 수 있으나 매치 종료 직후라 무해. 살아있는 표식 적 역시 매치 종료 시 EnemyGone 없이 남는다 — bridge 등록부와 컨트롤러 `_attachedTo` 모두 BeginPlacement clear 로 리셋되므로 상태 누수 없음 (critic gap 명문화).

**컨트롤러 `CommitMarkEnemy`**:
- `TryGetUsableAttach` 재사용(Active 거절) → `ApplyBountyMark` → 성공 시 `UseUnit`(풀 이탈) + `_attachedTo[entryId] = (enemy, 0)` + Spend. (호스트가 defender 가 아닐 뿐 수명주기는 Unit 부착과 동일.)
- `EnemyGone` 구독: `host == entity` 인 entry `Recover`(큐 맨 뒤) + `AttachmentsChanged`. `OnDefenderDied` 회수와 대칭. 구독/해지는 기존 선례(`DreamcatcherHandController.cs:89-102`)처럼 OnEnable/OnDisable 대칭 (critic gap).
- `AtAttachCap` 은 **의도적으로 미적용** — 적 표식 상한은 이중 표식 preflight(1개)가 이미 강제하고, 부착 캡은 defender 슬롯 개념이라 무의미 (critic m4).
- 라우팅 방어: BountyMark 카드가 실수로 `CommitAttach`(defender 경로)에 유입돼도 payload 가 어떤 bake 분기에도 안 걸려 trigger=None 가드(`BattleBridge.Dreamcatcher.cs:275`)로 attached=0 → -1 → **무차감 거절**된다. 이 암묵 방어를 계약으로 명시 — 정식 라우팅은 unit 3 드래그 판별이 담당 (critic m4).
- 유의: unit-dreamcatcher-icons 의 부착 스트립은 defender 뷰 대상 — 적 호스트 스트립은 비목표(인디케이터는 unit 3).

## 완료 기준

- [ ] compile 0 에러
- [ ] PlayMode: ① 표식 → 처치 시 각성치 ×3 지급 + 카드 큐 맨 뒤 복귀 ② 표식 → 유출 시 보상 없음 + 카드 복귀 ③ 이중 표식 거절(무차감) ④ 표식 적의 실수령 피해 30% 감소 실측
- [ ] `EnemyKilledEvent` entity append 로 인한 기존 드레인(점수/로그/각성) 무회귀
