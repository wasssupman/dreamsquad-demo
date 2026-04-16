# Phase 2 Decisions Log

> 본 문서는 Phase 2 (스킬) 진행 중 에이전트가 내린 기술적 결정과 근거를 한 줄씩 누적 기록한다.
> CLAUDE.md "기본 워크플로우"와 PHASE2.md 섹션 4의 자율 결정 영역에 따른다.

---

## P2-01 — SkillData SO + 고정 3종 콘텐츠

### 결정

1. **SkillData 필드 구조**: PHASE2.md §2.1 확정안 그대로 — `id`, `displayName`, `description`, `effect`(enum), `target`(enum), `range`, `magnitude`, `durationSec`, `cooldownSec`, `uiTint`. 구현체 2개 이상 될 때까지 `ISkillEffect` 같은 인터페이스 도입 안 함(TRD 5.2 준수).
2. **enum 기반 effect/target**: `SkillEffectType { SlowField, PowerSurge, RapidFire }`, `SkillTargetType { TilePoint, DefenderUnit }`. `BattleBridge.CastSkill*` switch 분기에서 소비 예정(P2-04). enum 값 이름은 PHASE2 확정과 1:1 매핑.
3. **에셋 생성 방식**: Unity MCP `execute_code`로 `AssetDatabase.CreateAsset` 수행 → GUID 안정성 자동 확보, YAML 수동 작성/meta GUID 지정 불필요. Phase 1 Defender SO 수동 YAML 방식 대비 단순·오타 내성 우선.
4. **에셋 경로**: `Assets/_Project/Data/Skills/` 신규 폴더 1개 생성. Phase 1 `Data/Defenders`, `Data/Materials`, `Data/Maps` 관례 이어감.
5. **uiTint 색상 배정**: Slow Field=파랑(0.2,0.6,1), Power Surge=오렌지(1,0.4,0.1), Rapid Fire=노랑(1,0.85,0.1). Phase 1 Defender 머티리얼 색과 겹치지 않도록 조정(슬롯 색을 유닛 색과 혼동 방지).
6. **수치 고정치**: PHASE2.md §2.1 표 그대로 — Slow Field(range=2, mag=0.6, dur=5s, cd=20s), Power Surge(mag=2, dur=8s, cd=30s), Rapid Fire(mag=0.5, dur=6s, cd=25s). 튜닝은 Phase 2 주관 평가 후 검토.

### 검증 결과

- `AssetDatabase.FindAssets("t:SkillData")` = 3건, 각 SO 필드 Inspector 읽기 정상.
- Assets > Create > Wassup > Skill 메뉴 동작(CreateAssetMenu order=12).
- 콘솔 에러 0, 경고 0.

---

## P2-03 — Movement×Slow, Combat×Boost/CDR 읽기 연동

### 결정

13. **GetComponentLookup 방식**: MovementSystem과 AttackSystem 각각 OnUpdate 시작에서 `SystemAPI.GetComponentLookup<T>(isReadOnly: true)` 획득 후 루프 내 `HasComponent + Lookup[entity]`로 읽기. ArchetypeChunk 순회 재배열이나 옵션 Query 추가는 회피 — 효과 미부여 엔티티가 다수일 때의 기본 경로 오버헤드 최소화.
14. **원본 Component 불변 확정**: `PathFollowState.speed`, `AttackState.damage`, `AttackState.cooldownDuration` 3개 필드는 Phase 2에서 **쓰기 금지**. 효과 반영은 step 계산·방출 amount·reset 값에만 곱해서 소비. 회귀 테스트에서 base 값 불변 assert 포함.
15. **방출 경로 변경 범위**: AttackSystem이 IncomingDamage.amount에 damageMul을 곱해 push. `IncomingDamage` Component 자체는 Units 소유라 AttackSystem의 쓰기는 이미 P0-04 결정 #6의 "엔티티 lifecycle 조작" 예외에 속하며, 이번 변경은 그 amount 값 계산에만 영향.
16. **EffectTickSystem 순서 무관성 확인**: Phase 2 현 구현에서 Movement/Combat/EffectTick의 SimulationSystemGroup 내 순서는 결과에 무관. 이유: Movement/Combat는 같은 프레임의 Effects Component remaining 감쇄를 기다리지 않고 읽음 — 한 프레임 늦은 제거를 허용(만료 직전 한 프레임은 효과 유지). Phase 2 허용 범위.

### 검증 결과

- EffectIntegrationTests 2건 신규: Movement×SlowEffect(0.5 multiplier로 step 절반), Combat×DamageBoost/CDR(2x damage, 0.5x cooldown reset) + base 값 불변 assert.
- 회귀 포함 총 19/19 pass, 1.51s.
- 콘솔 에러 0, 경고 0.

---

## P2-02 — Effects 맥락 Component + EffectTickSystem

### 결정

7. **Component 필드 스키마 통일**: `SlowEffect`, `DamageBoost`, `CooldownReduction` 모두 `{ float remaining; float multiplier; }` 동일 형태. 향후 `stackCount` 등이 필요해지면 개별 Component에만 필드 추가(공통 베이스 타입 만들지 않음 — TRD 5.2 준수).
8. **Non-stackable 정책**: 같은 효과 재부여는 **더 긴 remaining 유지 + 최신 multiplier 덮어쓰기**. 스택 카운트/데미지 누적 로직은 Phase 2 제외.
9. **EffectSpawner 단일 쓰기 창구**: 외부(BattleBridge.CastSkill*)에서 Effects Component를 Add/Update할 때 반드시 `EffectSpawner.Apply*` 경유. BattleBridge가 직접 `em.AddComponentData<SlowEffect>`를 호출하면 맥락 쓰기 경계 위반으로 간주. `Apply<T>` 제네릭 함수로 중복 제거.
10. **EffectTickSystem 3-pass 순회**: SystemAPI.Query를 3번 개별 호출(SlowEffect, DamageBoost, CooldownReduction). Component가 서로 다른 entity에 붙을 가능성(적 vs 방어) 때문에 combined query 이득 없음. 동일 entity에 2개 붙어도 pass 분리로 정확히 처리.
11. **OnCreate Burst 제외**: `state.RequireAnyForUpdate(params EntityQuery[])`는 managed array 요구 → P0-07 BC1028 회피 선례대로 OnCreate에서 [BurstCompile] 제거. OnUpdate는 Burst 유지.
12. **SystemGroup 배치**: `[UpdateInGroup(typeof(SimulationSystemGroup))]`만 부여. MovementSystem/AttackSystem과 동일 그룹에 속하되 [UpdateAfter] 불지정 — 3 시스템 간 순서가 Phase 2 결과에 영향 주지 않음(Movement/Combat는 Effects Component를 읽기만 하며 같은 프레임 내 원본 값 유지). Phase 3에서 순서 필요해지면 재검토.

### 검증 결과

- EditMode 테스트 4건 신규: SlowEffect 감쇄, 만료 제거, DamageBoost/CooldownReduction 독립 만료, EffectSpawner 재적용 정책.
- 회귀 포함 총 17/17 pass (기존 13 + 신규 4), 1.63s.
- 콘솔 에러 0, 경고 0.
