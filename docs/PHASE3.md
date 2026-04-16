# Phase 3 — 전투 비주얼 (투사체 시스템 + 체력바)

> 본 문서는 `PRD.md`, `TRD.md`, `PHASE0~2.md`, `phase0~2-decisions.md`를 전제로 작성되었다. Phase 0~2에서 확립된 아키텍처 경계, 맥락 분리, 추상화 규칙, 금지 패턴은 Phase 3에서도 그대로 유지된다.

---

## 0. Phase 3의 존재 이유

Phase 0~2까지 방어 유닛의 공격은 **즉시 데미지 적용**(AttackSystem → IncomingDamage buffer)으로 구현되어 있다. 전투가 벌어지고 있는지, 적이 피격당하고 있는지, 체력이 얼마 남았는지 **시각적 피드백이 전무**하다. 이 상태에서 Phase 4(배치 시 효과 / 인접 시너지)나 Phase 5(H1/H2/H3 플레이어 측정)를 진행하면:

- 시너지 효과(멀티샷, 스플래시 등)가 "실제로 발동됐는지" 플레이어가 읽을 수 없음
- 스킬 시전 후 적의 체력 감소가 보이지 않아 "스킬이 먹혔는지" 알 수 없음
- H3(패배 귀인) 측정 시 "왜 졌는지" 판단 근거가 부족

Phase 3은 **전투 가독성**을 확보하는 기반 작업이다. 이후 Phase에서 시너지·멀티샷·DoT 등을 올릴 때 이 기반 위에 자연스럽게 확장된다.

### Phase 3이 하는 것 / 안 하는 것

**하는 것:**
- **투사체(Projectile) 시스템**: 방어 유닛이 공격할 때 즉시 데미지 대신 투사체 엔티티를 생성. 투사체가 타깃에 도달하면 데미지 적용.
- **ProjectileData SO**: 속도, 비주얼(mesh/material), 피격 허용 반경(hitThreshold), Phase 4용 onHit 필드.
- **투사체 비주얼/기능 분리**: ECS Component로 이동·충돌·데미지 로직, Entities Graphics로 시각화.
- **유닛 체력바**: 공격/방어 유닛 모두 체력 비율을 보여주는 ECS 기반 쿼드.
- **DefenderUnitData에 ProjectileData 참조 추가**: 방어 유닛이 어떤 투사체를 발사하는지 SO 수준에서 결정. null이면 즉시 데미지 폴백.
- **피격 피드백 최소 구현**: 스케일 펀치 또는 색상 flash(방식 자율, 단순).

**안 하는 것:**
- **멀티샷 / 연사 / DoT / 스플래시** — 기반(onHitEffect enum, splashRadius 필드)만 열어두고 실 구현은 Phase 4.
- **배치 시 효과 / 인접 시너지** — Phase 4.
- **코스트 / 타이머 / 봇** — Phase 5.
- **파티클 이펙트** — VFX Graph/Particle System은 프로토타입 범위 외.
- **로그 스키마 변경** — 투사체 발사/피격 이벤트는 Phase 3에서 로그에 기록하지 않는다. 기존 `placements`/`skill.usages`/`result`만 유지.

---

## 1. Phase 3의 게임 흐름 변화

```
[전투 진행 중]
  방어 유닛 쿨다운 완료
    → AttackSystem이 ECB로 ProjectileSpawnRequest 컴포넌트를 방어 엔티티에 부여 (또는 싱글톤 큐에 enqueue)
    → AttackSystem이 AttackState.cooldownRemaining을 기존과 동일하게 리셋
    ↓
  BattleBridge.Update가 ProjectileSpawnRequest를 드레인하여 MonoBehaviour 측에서
  투사체 엔티티 생성 (_em.CreateEntity + RenderMeshUtility.AddComponents)
    ↓
  ProjectileMoveSystem이 target의 LocalTransform을 읽어 추적 이동
    ↓
  ProjectileHitSystem이 도달 판정 → IncomingDamage append + 투사체 파괴 + 피격 피드백
    ↓
  [적/방어 체력바 실시간 반영]
```

**중요 경로 이원화 (Phase 3 확정):**
- **ECS 내부(AttackSystem, ProjectileMoveSystem, ProjectileHitSystem)** 는 Burst 호환 로직만 수행 → Entity/struct Component만 다룸.
- **투사체 엔티티 생성 (RenderMeshUtility.AddComponents 포함)** 은 반드시 BattleBridge 측 MonoBehaviour 경로를 경유한다 — 기존 `SpawnUnit`/`PlaceDefender` 패턴과 동일. 이 결정은 RenderMeshArray가 managed 객체이므로 ISystem 내부에서 추가 불가하다는 제약에 따른다.

---

## 2. Phase 3 콘텐츠 스펙

### 2.1 ProjectileData SO

```csharp
public enum OnHitEffectType { None, Poison, Fire, Splash, Slow }

[CreateAssetMenu(fileName = "Projectile", menuName = "Wassup/Projectile", order = 13)]
public class ProjectileData : ScriptableObject
{
    public string id;
    public float speed = 10f;
    public float hitThreshold = 0.3f;      // 도달 판정 반경(월드 단위). SO로 승격 — 하드코딩 금지.
    public Mesh visualMesh;                // null이면 built-in Sphere
    public Material visualMaterial;        // null이면 방어 유닛 visualMaterial 상속
    public float visualScale = 0.3f;

    // Phase 3에서는 로드만 되고 사용하지 않는다. Phase 4에서 DoT/Splash/Slow 소비.
    public OnHitEffectType onHitEffect = OnHitEffectType.None;
    public float onHitMagnitude;
    public float onHitDuration;
    public float splashRadius;
}
```

**DefenderUnitData에 필드 추가**: `public ProjectileData projectile;`
- null이면 기존 즉시 데미지 폴백 (Phase 0~2 호환).
- SO가 할당되면 투사체 경로로 전환.

### 2.2 투사체 ECS Component / System

**맥락 소속**: 투사체는 **Combat 맥락** 하위. 폴더: `Assets/_Project/Scripts/Battle/Combat/Projectile/`.

**맥락 경계 해석 (Phase 3 결정, phase3-decisions.md에 기록)**:
- `IncomingDamage`(DynamicBuffer<IComponentData>, Units 소유)에 **쓰기**는 **TRD 2.5.2 규칙 2의 "맥락 간 이벤트는 Buffer/NativeQueue" 채널** 이다. AttackSystem(Combat → Units append)이 이미 이 패턴을 사용 중이며 Phase 0에서 암묵적으로 수용됐다. Phase 3의 ProjectileHitSystem(Combat → Units append)은 **동일 이벤트 채널의 재사용**이며, Phase 0 decision #13(Units lifecycle 예외)과 별도의 "허용 예외"다.
- 위 해석을 phase3-decisions.md 결정번호로 명시하라.

**Component (전부 unmanaged IComponentData/struct)**:

```csharp
public struct ProjectileTag : IComponentData { }

public struct ProjectileState : IComponentData {
    public Entity target;
    public float speed;
    public float damage;       // 발사 시점 스냅샷 (DamageBoost 반영 완료). 비행 중 불변.
    public float hitThreshold;
    // NOTE: onHitEffect 관련 필드는 Phase 3에서 의도적으로 제외 — Phase 4에서 필요 시 재추가(데드 필드 금지, TRD 3.6).
}

// 방어 엔티티에 부착. ProjectileData의 struct 필드만 담는다. managed(Mesh/Material) 정보는 BattleBridge 측 캐시가 SO 레퍼런스 → RenderMeshArray 매핑으로 해결한다.
public struct ProjectileRef : IComponentData {
    public float speed;
    public float hitThreshold;
    public float visualScale;
    public int projectileAssetIndex;   // BattleBridge가 ProjectileData → int 인덱스 발급. MonoBehaviour가 투사체 생성 시 mesh/material 조회에 사용.
}

// AttackSystem이 ECB로 방어 엔티티에 부여. BattleBridge.Update가 드레인/제거.
public struct ProjectileSpawnRequest : IComponentData {
    public Entity shooter;     // self entity (중복이지만 드레인 편의상)
    public Entity target;
    public float damage;       // boost 반영된 스냅샷
    public float speed;
    public float hitThreshold;
    public float visualScale;
    public int projectileAssetIndex;
}
```

**System**:

- `ProjectileMoveSystem` (ISystem, BurstCompile, UpdateInGroup SimulationSystemGroup):
  - `SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: true)`로 target 위치 읽기.
  - target 유효성 판정: `Entity.Null` 비교 + `lookup.HasComponent(target)`. **`EntityManager.Exists`는 Burst 비호환이므로 사용 금지**.
  - target 소실 시 투사체 파괴(ECB.DestroyEntity).
  - 매 프레임 `pos += normalize(targetPos - pos) * speed * dt` + LocalTransform 갱신.

- `ProjectileHitSystem` (ISystem, BurstCompile, UpdateAfter(ProjectileMoveSystem)):
  - 투사체-타깃 거리 < hitThreshold면 `ecb.AppendToBuffer<IncomingDamage>(target, { amount = damage })` + `ecb.DestroyEntity(projectile)`.
  - AttackState를 **절대 쓰지 않는다** — 쿨다운 리셋은 발사 시점의 AttackSystem 책임.

**AttackSystem 변경 (기존 로직 유지 + 분기):**
- 투사체 경로 (방어 엔티티가 `ProjectileRef` 보유):
  - 기존 damage = `attack.damage * damageMul` 계산 유지.
  - 기존 `attack.cooldownRemaining = cooldownDuration * cooldownMul` 리셋 유지.
  - 기존처럼 `ecb.AppendToBuffer(bestTarget, ...)` 대신 `ecb.AddComponent(defenderEntity, new ProjectileSpawnRequest { ... })` 부여.
- 폴백 경로 (ProjectileRef 미부여):
  - 기존 즉시 `IncomingDamage` append 경로 그대로. Phase 2 회귀 테스트가 이 경로로 계속 검증.

### 2.3 체력바 (Units 맥락)

**맥락 소속**: **Units 맥락**. 소유권 = 표시 대상의 Health. 폴더: `Assets/_Project/Scripts/Battle/Units/HealthBar/`. "Visual 유틸 별도 폴더" 옵션은 삭제(TRD 2.5.1 맥락 4종 고정 + CLAUDE.md 새 맥락 금지).

**구현 방식 (확정, World-space Canvas 옵션 폐기):**
- **ECS 기반 쿼드** — HealthBarTag + `HealthBarState { Entity owner; float yOffset; }` 신규 엔티티가 각 유닛당 1개.
- 쿼드 mesh(플레인) + unlit material(녹색 단색 권장; 그라데이션은 자율).
- `HealthBarSystem` (ISystem, Units 맥락): owner의 Health.value/max로 LocalTransform.Scale.x 조정 + owner 위 yOffset 위치.
- `SystemAPI.GetComponentLookup<Health>(true)`, `GetComponentLookup<LocalTransform>(true)` 사용.
- owner 유효성 판정: `Entity.Null` + `lookup.HasComponent`. owner가 소실되면 bar도 ECB로 파괴.

**생성/파괴 경로 (확정):**
- 체력바 엔티티 **생성**은 BattleBridge.SpawnUnit(공격 유닛) / PlaceDefender(방어 유닛) **마지막 부분**에서 `_em.CreateEntity` + HealthBarTag + HealthBarState + RenderMeshUtility.AddComponents(공유 쿼드 mesh + 공유 녹색 material).
- **파괴**는 HealthBarSystem이 owner 유효성 판정에서 자동 파괴. Restart/Teardown 경로는 HealthBarTag 엔티티를 추가로 destroy query에 포함.

### 2.4 피격 피드백

**확정: 스케일 펀치 방식**
- `HitFlashTag { float remaining; float originalScale; }` Component를 ProjectileHitSystem이 타깃에 부여.
- `HitFlashSystem` (Units 맥락, ISystem): remaining을 틱하며 `LocalTransform.Scale = originalScale * (1 + 0.2f * remaining/duration)` 형태로 줄어들다 만료 시 Component 제거 + 원래 스케일 복원.
- duration 0.15f 기본 — hitThreshold와 달리 Phase 3 한정 const 허용. 사용자 튜닝 시점에 SO 승격.

### 2.5 기존 코드 영향 요약

| 파일 | 변경 |
|---|---|
| `DefenderUnitData.cs` | `projectile` SO 필드 추가 |
| `BattleBridge.cs` | ProjectileSpawnRequest 드레인 Update 로직 추가, projectile RenderMeshArray 캐시, PlaceDefender에서 ProjectileRef + 투사체 asset index 부여, TeardownCurrentBattle에 ProjectileTag/HealthBarTag 파괴 추가, SpawnUnit/PlaceDefender 끝에서 체력바 엔티티 생성 |
| `AttackSystem.cs` | ProjectileRef 유무로 투사체/즉시 분기. 쿨다운 리셋·DamageBoost 읽기는 기존 유지 |
| `ProjectileMoveSystem.cs` (신규) | Combat 하위 |
| `ProjectileHitSystem.cs` (신규) | Combat 하위 |
| `HealthBarSystem.cs` (신규) | Units 하위 |
| `HitFlashSystem.cs` (신규) | Units 하위 |
| `DamageApplicationSystem.cs` | 변경 없음 — IncomingDamage buffer 소비 로직 동일 |

**기존 테스트 영향 (중요)**:
- `EffectIntegrationTests.Combat_Applies_DamageBoost_To_Emitted_Damage_And_CooldownReduction_To_Reset` — 현재는 즉시 IncomingDamage 경로를 가정. Phase 3에서 ProjectileRef가 없는 경로(폴백)를 그대로 검증하므로 **테스트는 수정 없이 계속 동작**해야 한다. AttackSystem 분기 조건 `HasComponent<ProjectileRef>(entity)`가 false면 기존 경로로 떨어지므로 회귀 없음.
- 폴백 경로를 실수로 제거하지 말 것 — P3-03 구현 체크포인트로 명시.

---

## 3. 종료 조건 (Done Criteria)

### 3.1 기능 이진 체크 (작업 순서)

**[P3-01] ProjectileData SO + DefenderUnitData 확장**
- [x] `Data/ProjectileData.cs` SO + `OnHitEffectType` enum
- [x] `DefenderUnitData.projectile` 필드 추가
- [x] 기본 투사체 SO 1~3개 생성 (`Assets/_Project/Data/Projectiles/`)
- [x] 기존 10종 방어 유닛 SO에 projectile 레퍼런스 할당(또는 의도적 null 유지로 폴백 검증용 1~2종)
- 선행: Phase 2 완료
- 완료 확인: Inspector에서 DefenderUnitData → projectile 필드에 SO 연결됨

**[P3-02] 투사체 ECS Component + Move/Hit System**
- [x] `Battle/Combat/Projectile/ProjectileTag.cs`, `ProjectileState.cs`, `ProjectileRef.cs`, `ProjectileSpawnRequest.cs`
- [x] `Battle/Combat/Projectile/ProjectileMoveSystem.cs` (ISystem, BurstCompile, ComponentLookup으로 target 유효성)
- [x] `Battle/Combat/Projectile/ProjectileHitSystem.cs` (ISystem, BurstCompile, UpdateAfter ProjectileMove, IncomingDamage append + ECB.DestroyEntity)
- 선행: P3-01
- 완료 확인: EditMode 테스트 — 투사체 엔티티 수동 생성 → tick → 이동 및 도달 시 IncomingDamage 적용 + 투사체 파괴 확인

**[P3-03] AttackSystem 투사체 분기 (폴백 유지)**
- [x] `HasComponent<ProjectileRef>(defenderEntity)` 체크로 분기
- [x] 투사체 경로: `ecb.AddComponent(defenderEntity, new ProjectileSpawnRequest { ... })` + 기존 쿨다운 리셋
- [x] 폴백 경로: 기존 IncomingDamage append 그대로 (Phase 2 회귀 테스트 대상)
- [x] DamageBoost/CooldownReduction 읽기는 기존 경로 유지
- 선행: P3-02
- 완료 확인: 기존 EffectIntegrationTests.Combat_Applies_... 통과 + 투사체 SO 부여된 방어 유닛에서 ProjectileSpawnRequest 컴포넌트 부여됨(MCP execute_code로 검증)

**[P3-04] BattleBridge 투사체 생성/렌더 연동**
- [x] `_projectileRenderCache` (Dictionary<ProjectileData, RenderMeshArray>) + `_projectileAssetByIndex` (ProjectileData[]) 캐시
- [x] PlaceDefender에서 defenderUnit.projectile이 있으면 ProjectileRef 컴포넌트 부여 (projectileAssetIndex 발급)
- [x] `DrainProjectileSpawnRequests()`: Update에서 ProjectileSpawnRequest 쿼리로 드레인 → ECS CreateEntity(ProjectileTag + ProjectileState + LocalTransform + RenderMeshUtility.AddComponents) → ProjectileSpawnRequest 제거
- [x] TeardownCurrentBattle에 ProjectileTag 엔티티 파괴 추가
- 선행: P3-03
- 완료 확인: Play에서 방어 유닛이 작은 구체/큐브를 발사, 타깃 향해 이동, 도달 시 소멸, Restart 후 잔여 0

**[P3-05] 체력바**
- [x] `Battle/Units/HealthBar/HealthBarTag.cs`, `HealthBarState.cs`, `HealthBarSystem.cs`
- [x] 공유 쿼드 mesh + 공유 녹색 material (BattleBridge 캐시)
- [x] BattleBridge.SpawnUnit / PlaceDefender 끝에서 체력바 엔티티 생성 (owner 필드에 방금 만든 유닛 entity 저장)
- [x] HealthBarSystem이 owner의 Health/LocalTransform 읽기 → scale/position 갱신. owner 소실 시 ECB 파괴.
- [x] TeardownCurrentBattle에 HealthBarTag 파괴 추가
- 선행: P3-01
- 완료 확인: Play에서 모든 유닛 머리 위에 녹색 바, 피격 시 줄어듦, 유닛 사망 시 사라짐

**[P3-06] 피격 피드백 (스케일 펀치)**
- [x] `Battle/Units/HitFlashTag.cs` + `HitFlashSystem.cs`
- [x] ProjectileHitSystem이 타깃에 HitFlashTag 부여(remaining=0.15f, originalScale=현재 scale)
- [x] HitFlashSystem이 감쇄하며 scale 복원
- 선행: P3-03
- 완료 확인: 적이 맞을 때 순간 1.2배 크기로 확대 후 복원

**[P3-07] EditMode 테스트 확장 + 마이그레이션**
- [x] `ProjectileMoveSystemTests` (최소 2건): 이동 진행·target 소실 시 투사체 파괴
- [x] `ProjectileHitSystemTests` (최소 2건): 도달 시 IncomingDamage append + 투사체 파괴, 거리 밖에서는 무시
- [x] 기존 `EffectIntegrationTests`의 Combat 테스트가 **폴백 경로**(ProjectileRef 없음)를 명시적으로 사용 — 수정 없이 통과
- [x] HealthBarSystem/HitFlashSystem 테스트는 스킵(플레이 검증으로 충분)
- [x] 기존 19개 회귀 없음, **23/23 pass**
- 선행: P3-02
- 완료 확인: run_tests 전부 pass

**[P3-08] Phase 0~2 회귀 체크**
- [x] 드래프트 → 전투(투사체 비주얼) → 스킬 사용 → 결과 → Restart/Redraft 정상 (P3-04/05/06 플레이 검증)
- [x] 로그 파일 기존 필드 그대로 적재 (Phase 3은 로그 스키마 변경 없음)
- [x] ProjectileData null인 방어 유닛(Guardian/Bruiser/Bastion)은 즉시 데미지 폴백 동작
- 선행: P3-07
- 완료 확인: 한 판 수동 플레이 완주

---

### 3.2 아키텍처 이진 체크

**Phase 0~2 재확인:**
- [ ] BattleBridge가 유일한 MonoBehaviour ↔ ECS 창구 (투사체 RenderMesh 생성도 여기 경유)
- [ ] Effects Component 읽기/쓰기 경계 유지
- [ ] 드래프트/스킬 로직 MonoBehaviour 유지
- [ ] GameManager 유일 싱글톤

**Phase 3 전용:**
- [ ] 투사체 Component/System이 Combat 맥락 하위 (`Battle/Combat/Projectile/`)
- [ ] 체력바 Component/System이 Units 맥락 하위 (`Battle/Units/HealthBar/`)
- [ ] 새 맥락 폴더 0개 (Units/Movement/Combat/Effects 4종 유지)
- [ ] 투사체 System이 Movement의 PathFollowState를 사용하지 않음
- [ ] ProjectileData 전부 SO — speed/hitThreshold/visualScale 전부 SO 필드
- [ ] AttackSystem의 투사체/즉시 분기가 ProjectileRef 유무로만 결정 (if/else 1개, 전략 패턴 아님)
- [ ] ProjectileState에 Phase 3 미사용 데드 필드 없음
- [ ] 투사체 엔티티 생성의 managed 부분(RenderMeshUtility)은 BattleBridge.Update 경로에서만 수행 — ISystem 내에서 X
- [ ] Assembly Definition 2개 체제 유지

---

### 3.3 주관 평가 게이트

Phase 3의 핵심 질문: **전투 상황이 "읽히는가".**

- 3~5명에게 한 판 플레이 시킨 후:
  - "방어 유닛이 공격하고 있다는 것을 알 수 있었는가?" (Y/N)
  - "적 체력이 줄어드는 것을 볼 수 있었는가?" (Y/N)
  - "어떤 적이 위험한지(체력 많음) 구분할 수 있었는가?" (Y/N)
- 통과 기준: 3문항 모두 Y 다수.

---

## 4. 에이전트 자율 결정 영역

- 투사체 기본 mesh (Sphere/Cube/Capsule)
- 체력바 색상 (단색 녹색 / 녹→적 그라데이션)
- 체력바 크기·yOffset 기본값
- 피격 피드백 세기(0.2배 스케일 증가 등 세부 수치)
- 기본 투사체 SO 개수 (1~3종: 예 "Arrow"/"Bolt"/"CannonBall")
- ProjectileData의 방어 유닛별 매핑 테이블(어떤 defender가 어떤 projectile을 쓰나)

**결정 원칙**: 애매하면 단순한 쪽. 확장 자리만 열어두되 Phase 3에서 구현하지 않음. **hitThreshold는 자율 결정이 아님** — SO 필드다.

---

## 5. Phase 3 종료 시 산출물

- 동작하는 Unity 6 프로젝트 (투사체 발사 + 체력바 + 피격 반응)
- EditMode 테스트 23건+ pass
- `phase3-decisions.md` 누적 기록 (특히 IncomingDamage 쓰기 권한 해석)
- Phase 4(배치 시 효과 / 인접 시너지)에서 재활용될 핵심 타입: ProjectileData, ProjectileState, OnHitEffectType(필드 재도입 대상), HealthBarSystem

---

## 6. Phase 순서 (갱신)

| Phase | 내용 | 상태 |
|---|---|---|
| 0 | 실시간 디펜스 루프 | ✅ 완료 |
| 1 | 드래프트 | ✅ 완료 |
| 2 | 스킬 | ✅ 완료 |
| **3** | **전투 비주얼 (투사체 + 체력바)** | **현재** |
| 4 | 배치 시 효과 / 인접 시너지 | 대기 |
| 5 | 마무리 (3분 타이머, 봇, H1/H2/H3) | 대기 |

Phase 3 종료 후 `PHASE4.md`를 작성한다.

---

## 7. TRD 금지 패턴의 Phase 3 재적용

- **투사체 System은 Combat 맥락 안에서만 struct Component를 쓴다** — Movement의 PathFollowState 접근 금지. `IncomingDamage` append는 TRD 2.5.2 규칙 2의 이벤트 채널(phase3-decisions에 기록).
- **체력바 System은 Units 맥락 — Health 읽기만**.
- **새 싱글톤 금지** — ProjectileManager/HealthBarManager 같은 것 도입 금지.
- **"나중을 위한" 인터페이스 금지** — `IProjectile`, `IHitEffect` 추상화 금지. Enum + switch로 시작.
- **수치 하드코딩 금지** — speed/hitThreshold/visualScale은 ProjectileData SO. HitFlash duration 0.15f는 Phase 3 한정 const 허용 (사용자 튜닝 시 SO 승격).
- **Assembly Definition 2개 체제 유지**.
- **데드 필드 금지** — ProjectileState는 Phase 3 실제 사용 필드만. onHit 관련 필드는 ProjectileData에만.
- **투사체 시각 리소스는 SO 레퍼런스** — Resources.Load 패턴 금지.
- **ISystem 안에서 managed 타입 생성 금지** — RenderMeshArray/Material/Mesh 조작은 BattleBridge MonoBehaviour 경로만.

---

**문서 버전**: v1.1
**상태**: 확정, 에이전트(Codex) 전달 준비됨
**v1.0 → v1.1 변경**: critic 리뷰 10건 반영 — IncomingDamage 쓰기 권한 해석 명시, managed 레퍼런스 경로 이원화 (ProjectileSpawnRequest + BattleBridge drain), hitThreshold SO 승격, 체력바 맥락 Units로 확정, World-space Canvas 옵션 삭제, damage 스냅샷 정책 명시, target 소실 Burst 호환 체크 규정, ProjectileState 데드 필드 제거, HitFlash duration const 허용 명시, 테스트 목표 숫자 23/23으로 정정, 로그 스키마 불변 명시.
