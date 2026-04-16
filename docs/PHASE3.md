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
- **ProjectileData SO**: 속도, 비주얼(mesh/material), 향후 onHit 효과 타입 필드(Phase 3에서는 단일 타깃 직격만 구현).
- **투사체 비주얼/기능 분리**: ECS Component로 이동·충돌·데미지 로직, Entities Graphics로 시각화. 비주얼 교체가 기능에 영향 없음.
- **유닛 체력바**: 공격 유닛과 방어 유닛 모두에게 체력 비율을 보여주는 시각 표시.
- **DefenderUnitData에 ProjectileData 참조 추가**: 각 방어 유닛이 어떤 투사체를 발사하는지 SO 수준에서 결정.
- **피격 피드백 최소 구현**: 투사체 도달 시 타깃 색상 깜빡임(flash) 또는 스케일 펀치 — 방식은 자율 결정.

**안 하는 것:**
- **멀티샷 / 연사 패턴** — 기반 구조만 열어두고 실 구현은 Phase 4(시너지).
- **DoT(독, 화염) / 스플래시 대미지** — ProjectileData에 `onHitEffect` enum 필드만 추가, 실 처리 로직은 Phase 4.
- **배치 시 효과 / 인접 시너지** — Phase 4.
- **코스트 / 타이머 / 봇** — Phase 5.
- **파티클 이펙트** — Phase 3은 mesh/material 기반 최소 비주얼. VFX Graph는 프로토타입 범위 외.

---

## 1. Phase 3의 게임 흐름 변화

Phase 2까지의 흐름에서 **전투 중 시각적 변화**가 추가된다:

```
[전투 진행 중]
  방어 유닛 쿨다운 완료 → 투사체 엔티티 생성 (작은 구체/큐브, 색상 = 방어 유닛 색)
    ↓
  투사체가 타깃을 향해 이동 (ProjectileData.speed)
    ↓
  타깃 도달 → IncomingDamage 적용 + 투사체 엔티티 파괴 + 피격 피드백
    ↓
  [적/방어 유닛 체력바가 실시간 반영]
```

기존: `AttackSystem → IncomingDamage buffer 직접 append`
변경: `AttackSystem → ProjectileSpawnRequest 생성 → ProjectileSystem이 이동 → 도달 시 IncomingDamage 적용`

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
    public Mesh visualMesh;           // null이면 built-in Sphere
    public Material visualMaterial;   // null이면 방어 유닛 머티리얼 상속
    public float visualScale = 0.3f;
    // Phase 3에서는 None만 사용. Phase 4에서 DoT/Splash 구현.
    public OnHitEffectType onHitEffect = OnHitEffectType.None;
    public float onHitMagnitude;      // Phase 4에서 사용할 효과 세기
    public float onHitDuration;       // Phase 4에서 사용할 효과 지속
    public float splashRadius;        // Splash일 때 범위 (Phase 4)
}
```

**DefenderUnitData에 필드 추가**: `public ProjectileData projectile;`
- null이면 기존 즉시 데미지 폴백 (Phase 0~2 호환).
- SO가 할당되면 투사체 발사 경로로 전환.

### 2.2 투사체 ECS Component / System

**새 맥락 판단**: 투사체는 어느 맥락에 속하는가?
- 투사체의 이동 → Movement? 아니다 — Movement는 공격 유닛 경로 이동 전용.
- 투사체의 데미지 적용 → Combat.
- **결정: 투사체는 Combat 맥락에 배치한다.** 이유: 투사체는 공격의 연장선이며 AttackSystem에서 발원한다. Movement 맥락의 PathFollowState와 무관한 독자적 이동 로직(타깃 추적)을 사용한다.

**폴더**: `Assets/_Project/Scripts/Battle/Combat/Projectile/`

**Component**:

```
ProjectileTag : IComponentData { }              // 투사체 식별
ProjectileState : IComponentData {
    Entity target;          // 추적 대상
    float speed;
    float damage;           // 도달 시 적용할 데미지 (DamageBoost 이미 반영된 값)
    OnHitEffectType onHit;  // Phase 3에서는 None만
    float onHitMagnitude;
    float onHitDuration;
    float splashRadius;
}
```

**System**:

- `ProjectileMoveSystem` (ISystem, BurstCompile): 매 프레임 target의 LocalTransform.Position 방향으로 speed * dt 이동. target이 사라졌으면(Exists == false) 투사체 파괴.
- `ProjectileHitSystem` (ISystem, BurstCompile, UpdateAfter ProjectileMoveSystem): 투사체가 타깃에 충분히 가까우면(거리 < hitThreshold) IncomingDamage append + 투사체 파괴. hitThreshold는 0.3f 정도(자율 결정).

**AttackSystem 변경**:
- 기존: `ecb.AppendToBuffer(bestTarget, new IncomingDamage { amount = ... })`
- 변경: `DefenderUnitData.projectile != null`이면 ProjectileSpawnRequest Component 부여 대신 **BattleBridge에 spawn 요청 queue** 또는 **AttackSystem 내에서 직접 ECB로 투사체 엔티티 생성**.
- **선택**: AttackSystem은 ECB로 투사체 엔티티를 직접 생성한다(BattleBridge 경유 불필요 — 투사체는 전투 내부의 일시 엔티티이므로 Bridge 경계 밖이 아님). projectile SO 정보는 방어 유닛 엔티티에 부착된 `ProjectileRef` Component가 보관.

**새 Component on Defender**:
```
ProjectileRef : IComponentData {
    float speed;
    float visualScale;
    OnHitEffectType onHit;
    float onHitMagnitude;
    float onHitDuration;
    float splashRadius;
    // Material/Mesh 정보는 BattleBridge에서 spawn 시 RenderMesh로 설정
}
```

실제로는 BattleBridge.PlaceDefender에서 ProjectileData가 있으면 ProjectileRef Component를 붙이고, AttackSystem이 이를 읽어 투사체 엔티티를 ECB로 생성하는 흐름.

**투사체 비주얼**: `RenderMeshUtility.AddComponents`로 mesh+material 부여 (기존 유닛 패턴 동일). Material이 null이면 방어 유닛의 visualMaterial을 상속.

### 2.3 체력바

**구현 방식 (자율 결정, 추천: ECS 쿼드 방식)**:

**(A) ECS 기반 쿼드** (추천):
- 각 유닛 엔티티 생성 시 `HealthBarTag` + `HealthBarState { Entity owner }` Component를 가진 별도 엔티티를 생성.
- 쿼드 mesh(plane) + unlit material(녹→적 색상 or 고정 녹색).
- `HealthBarSystem` (ISystem): owner의 Health.value/max 비율로 LocalTransform.Scale.x 조정 + owner의 Position 위 0.8f 오프셋에 위치.
- owner가 파괴되면 bar도 파괴.

**(B) World-space Canvas**:
- 유닛당 Canvas + Slider. 성능 부담이 큼(100+ 유닛 시 문제).

**선택 권장**: (A) ECS 기반. 프로토타입 비주얼 수준이면 충분하고, 유닛 수 증가에 안전.

### 2.4 피격 피드백

투사체 도달 시 타깃에 최소한의 시각 반응:
- **방식 A**: `HitFlashTag` Component → `HitFlashSystem`이 0.1초간 스케일을 1.2배로 키운 뒤 복원.
- **방식 B**: Material 색상 깜빡임 (Entities Graphics에서 MaterialColor override).

**자율 결정**: 단순한 쪽. 방식 A(스케일 펀치) 권장.

### 2.5 기존 코드 영향

- **AttackSystem**: 투사체 경로와 즉시 데미지 경로를 분기. `HasComponent<ProjectileRef>` 여부로 판단.
- **BattleBridge.PlaceDefender**: ProjectileData가 있으면 ProjectileRef Component + 투사체 RenderMesh 캐시 추가.
- **BattleBridge.TeardownCurrentBattle**: ProjectileTag 엔티티도 파괴 대상에 추가.
- **DamageApplicationSystem**: 변경 없음 — IncomingDamage buffer 소비 로직 동일.
- **HealthBarSystem**: 신규 — Units 맥락? **아니다. 체력바는 순수 비주얼이므로 별도 `Visual` 유틸 또는 Combat에 배치 (자율 결정)**.

---

## 3. 종료 조건 (Done Criteria)

### 3.1 기능 이진 체크 (작업 순서)

**[P3-01] ProjectileData SO + DefenderUnitData 확장**
- [ ] `Data/ProjectileData.cs` SO + `OnHitEffectType` enum
- [ ] `DefenderUnitData.projectile` 필드 추가
- [ ] 기본 투사체 SO 1~3개 생성 (`Assets/_Project/Data/Projectiles/`)
- [ ] 기존 10종 방어 유닛 SO에 projectile 레퍼런스 할당
- 선행: Phase 2 완료
- 완료 확인: Inspector에서 DefenderUnitData → projectile 필드에 SO 연결됨

**[P3-02] 투사체 ECS Component + System**
- [ ] `Battle/Combat/Projectile/ProjectileTag.cs`, `ProjectileState.cs`
- [ ] `Battle/Combat/Projectile/ProjectileMoveSystem.cs` (ISystem, BurstCompile)
- [ ] `Battle/Combat/Projectile/ProjectileHitSystem.cs` (ISystem, BurstCompile, UpdateAfter ProjectileMove)
- [ ] `ProjectileRef` Component on defender entities
- 선행: P3-01
- 완료 확인: EditMode 테스트 — 투사체 생성 → 이동 → 타깃 도달 시 IncomingDamage 적용 + 투사체 파괴

**[P3-03] AttackSystem 투사체 분기**
- [ ] `HasComponent<ProjectileRef>` 있으면 투사체 엔티티 ECB 생성, 없으면 기존 즉시 데미지
- [ ] 투사체 엔티티에 ProjectileState + ProjectileTag + LocalTransform + RenderMesh 부여
- [ ] DamageBoost/CooldownReduction 효과는 기존대로 발사 시점에 반영 (투사체 damage에 포함)
- 선행: P3-02
- 완료 확인: Play에서 방어 유닛이 작은 구체를 발사하고, 구체가 적에게 날아가는 것 확인

**[P3-04] BattleBridge 투사체 연동**
- [ ] PlaceDefender에서 ProjectileData → ProjectileRef Component 부여
- [ ] 투사체 RenderMeshArray 캐시 (기존 defenderRenderCache 패턴)
- [ ] TeardownCurrentBattle에 ProjectileTag 엔티티 파괴 추가
- 선행: P3-03
- 완료 확인: 한 판 풀 플레이에서 투사체가 정상 생성/이동/소멸, Restart 후 잔여 투사체 0

**[P3-05] 체력바**
- [ ] 유닛 생성 시 체력바 엔티티 동시 생성 (ECS 쿼드 또는 자율 결정 방식)
- [ ] `HealthBarSystem` — owner의 Health 비율로 스케일/위치 매 프레임 갱신
- [ ] owner 파괴 시 체력바도 파괴
- 선행: P3-01
- 완료 확인: Play에서 적 + 방어 유닛 머리 위에 녹색 바가 보이고, 피격 시 줄어듦

**[P3-06] 피격 피드백**
- [ ] 투사체 도달 시 타깃에 시각 반응 (스케일 펀치 or 색상 flash)
- [ ] 0.1~0.2초 후 원래 상태 복원
- 선행: P3-03
- 완료 확인: 적이 맞을 때 순간 커지거나 색 변화

**[P3-07] EditMode 테스트 확장**
- [ ] ProjectileMoveSystem 테스트 (이동, 타깃 추적, 타깃 소실 시 파괴)
- [ ] ProjectileHitSystem 테스트 (도달 시 IncomingDamage 적용 + 투사체 파괴)
- [ ] 기존 19개 테스트 회귀 없음
- 선행: P3-02
- 완료 확인: run_tests 전부 pass (목표: 기존 19 + 신규 3+ = **22/22**)

**[P3-08] Phase 0~2 회귀 체크**
- [ ] 드래프트 → 전투(투사체 비주얼) → 스킬 사용 → 결과 → Restart/Redraft 정상
- [ ] 로그 파일 정상 적재 (기존 필드 + 변경 없음)
- [ ] ProjectileData null인 방어 유닛은 기존 즉시 데미지 폴백 동작
- 선행: P3-07
- 완료 확인: 한 판 수동 플레이 완주

---

### 3.2 아키텍처 이진 체크

**Phase 0~2 재확인:**
- [ ] BattleBridge가 유일한 MonoBehaviour ↔ ECS 창구
- [ ] Effects Component 읽기/쓰기 경계 유지
- [ ] 드래프트/스킬 로직 MonoBehaviour 유지
- [ ] GameManager 유일 싱글톤

**Phase 3 전용:**
- [ ] 투사체 Component/System이 Combat 맥락 하위에 위치 (Battle/Combat/Projectile/)
- [ ] 투사체 System이 Movement의 PathFollowState를 사용하지 않음 (독자 이동 로직)
- [ ] ProjectileData 전부 SO — 속도/스케일/onHit 하드코딩 없음
- [ ] 체력바가 ECS 기반이면 별도 System으로 분리, Health Component를 읽기만
- [ ] AttackSystem의 투사체/즉시 분기가 ProjectileRef 유무로만 결정 (if/else 1개, 전략 패턴 아님)
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
- 투사체 hitThreshold 수치 (0.2~0.5 범위)
- 체력바 구현 방식 (ECS 쿼드 vs World-space Canvas)
- 체력바 색상 (단색 녹색 / 녹→적 그라데이션)
- 체력바 크기·오프셋
- 피격 피드백 방식 (스케일 펀치 vs 색상 flash)
- 피격 피드백 지속 시간 (0.1~0.3s)
- ProjectileRef에 material 인덱스를 담을지, BattleBridge가 spawn 시 RenderMesh를 세팅할지
- 기본 투사체 SO 개수 (1~3종: 예를 들어 "Arrow"/"Bolt"/"Cannon Ball")

**결정 원칙**: 애매하면 단순한 쪽. 이후 Phase에서 확장할 자리만 열어두되 지금 구현하지 않음.

---

## 5. Phase 3 종료 시 산출물

- 동작하는 Unity 6 프로젝트 (투사체 발사 + 체력바 시각화)
- EditMode 테스트 22건+ pass
- `phase3-decisions.md` 누적 기록
- Phase 4(배치 시 효과 / 인접 시너지)에서 재활용될 핵심 타입: ProjectileData, ProjectileState, OnHitEffectType, HealthBarSystem

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

- **투사체 System은 Combat 맥락 안에서만 Component를 쓴다** — Movement의 PathFollowState 접근 금지.
- **새 싱글톤 금지** — ProjectileManager 같은 것 도입 금지.
- **"나중을 위한" 인터페이스 금지** — `IProjectileStrategy` 추상화 금지. OnHitEffectType enum + switch.
- **수치 하드코딩 금지** — 모든 투사체 수치는 ProjectileData SO.
- **Assembly Definition 2개 체제 유지**.
- **투사체 시각 리소스는 SO 레퍼런스** — Resources.Load 패턴 금지.

---

**문서 버전**: v1.0
**상태**: 확정, 에이전트 전달 준비됨
