# dreamcatcher-taxonomy-cleanup

> 상태: 완료 2026-07-12 (Unity 테스트 실행은 미실시 — 아래 검증 한계 참조)

## 상위 목표

드림캐쳐 카드 택소노미의 **이중 필드·잔재 코드를 정리**해, 신규 카드 작업자가 마주하는 "탐색 미로"를 줄인다. 능력 확장은 하지 않는다(=B1). 완전 통합(scope×payload 단일 모델, baked-slot revoke)은 별도 spec 으로 미룬다(후속 후보 B2).

### 검증 질문

> "신규 드림캐쳐 카드 하나를 추가할 때, 작업자가 봐야 하는 **택소노미 필드가 1개(CardType)**, **commit/apply 경로가 1개**인가?"

## 배경 — 현재 구조의 세금

- `CardType {Squad, Unit, Active}` 와 `CardBinding {Axis, Unit}` 가 **같은 정보를 2번** 인코딩한다(Squad⟺Axis, Unit⟺Unit). 둘의 일관성을 `DcSheetApplier` 가 경고 로그로 감시한다 — 이 감시 자체가 중복의 증거.
- 런타임 행동을 실제로 가르는 건 "페이로드 fan-out 스코프"(축 집합 vs host 1명) 하나뿐. `CardType` 이 "어떤 apply 머신을 돌릴지"의 프록시다.
- `CommitSquad`/`CommitUnit` 는 호출하는 bridge apply 메서드만 다르고 나머지(cap·attach·spend)는 동일한 near-duplicate.
- `placementWarmupSec` = 구 Squad warmup 잔재(런타임 reader 없음, sheet 스키마에만 잔존).

## 작업 단위

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | refactor | `0_binding_removal.md` | `CardBinding` enum·필드 제거, 런타임 가드를 `CardType` 기반으로 전환, sheet/테스트 정리 |
| 1 | refactor | `1_commit_path_unify.md` | `CommitSquad`/`CommitUnit` → 단일 commit 경로, bridge 단일 apply 디스패처 |
| 2 | cleanup | `2_dead_field_removal.md` | `placementWarmupSec`(구 Squad warmup 잔재) 필드·sheet 컬럼 제거 |
| 3 | docs | `3_handoff_summary.md` | 인계 요약 (구현 종료 시) |

## feature-wide 계약

1. **런타임 스코프 경계는 `CardType` 하나로 표현**한다. `CardBinding` 은 제거되고, "이 카드가 host-only 냐 축-집합이냐"는 `CardType.Unit` vs 그 외로 파생된다. (BattleBridge 가드: `card.type != CardType.Unit` → 무부착.)
2. **B1 은 능력을 추가하지 않는다.** 축 경로는 여전히 `effects[]`(StatModifier)만, host 경로는 `mechanics[]`/`attackMods[]`만 소비한다. cross-scope 페이로드(축에 mechanics 적용 = "모두에게 바운싱")는 **B2 로 이관** — 이번 범위 밖.
3. **`category`/`CardCategory` 는 유지**한다. `DeckBuilderView` 가 무의식 프레임 색에 사용하는 **살아있는 소비처**가 있다. (DreamcatcherCard 필드 주석의 "no consumer" 오기는 정정.)
4. **직렬화 안전**: 카드 에셋은 append-only 로 관리돼 왔다. 필드 제거 시 Unity 는 해당 데이터를 드롭하며, 의미는 `CardType` 이 이미 보존하므로 손실 없음. sheet 스키마 변경은 같은 커밋에서 importer/exporter/테스트를 함께 갱신한다.
5. **공용 API 표면 축소**: 컨트롤러는 단일 commit 진입점, bridge 는 단일 `ApplyDreamcatcherCard(entity, card)` 디스패처를 노출한다(내부 두 apply 구현은 유지 — 실제 머신이 다름).
6. **회귀 방지**: 기존 EditMode/PlayMode 테스트(`DeckRulesTests`, `PlacementAuraTest`, `DcSheetImportTests`, `DreamcatcherEffectTest`, `DreamcatcherCombatDamageTest`)가 모두 green 이어야 완료.

## 파이프라인 커버리지

**N/A** — 신규 플레이 오브젝트를 만들지 않고, 생성→렌더 경로를 바꾸지 않는다. 순수 데이터/택소노미·컨트롤러 리팩터이므로 `object-pipeline-map` 대조 대상이 아니다.

## 후속 후보 (이번 범위 밖)

- **B2 — dreamcatcher-scope-payload-unify**: baked-slot(`DcTriggerSlot`/`DcAttackModSlot`/`DamagedCounter`) 의 instanceId 기반 revoke·미래상속 머신 신설 → 어떤 페이로드든 어떤 스코프로. Squad/Unit 을 scope 값으로 강등해 `CardType` 에서 물리 삭제, "모두에게 바운싱" 실현. 실질 ECS(Effects/Combat revoke 경로) 작업 + 회귀 위험 큼. **실제로 cross-scope 페이로드를 요구하는 카드가 생길 때 착수.**
- 밸런싱 노브(덱 캡·각성 코스트)를 타입 파생이 아닌 per-card cost/포인트 예산으로 이전 (B2 동반 시 검토).
