# Phase 3 Decisions Log

> Superseded: 확정/구현 완료 내용은 `PHASE3.md`에 통합됨. 본 문서는 히스토리/리뷰 기록으로만 유지.

> Phase 3 (전투 비주얼 — 투사체 시스템 + 체력바) 진행 중 에이전트가 내린 기술적 결정과 근거를 한 줄씩 누적 기록한다.

---

## P3-01 — ProjectileData SO + DefenderUnitData 확장

### 결정

1. **ProjectileData 필드 확정**: PHASE3.md §2.1 스키마 그대로 — `id`, `speed`, `hitThreshold`, `visualMesh`, `visualMaterial`, `visualScale`, `onHitEffect` + 보조 4필드(Phase 4용). `hitThreshold`는 하드코딩 피하고 SO로 승격 (TRD 5.3 준수).
2. **OnHitEffect 보조 필드의 Phase 3 비사용 명시**: `onHitEffect`/`onHitMagnitude`/`onHitDuration`/`splashRadius`는 Phase 3에서 로드만 되고 소비 없음. 주석으로 명시. ProjectileState에는 전달하지 않아 ECS 쪽 데드 필드 0건.
3. **투사체 3종 선정 (자율 결정 기록)**: Arrow(speed=12, scale=0.25), Bolt(speed=16, scale=0.3), CannonBall(speed=7, scale=0.5). hitThreshold 공통 0.35~0.4. 각 투사체는 물리적 성격 차이가 느껴지도록 속도/크기 변주.
4. **방어 유닛 매핑**: Archer/Ranger/Scout→Arrow, Marksman/Piercer/Sniper→Bolt, Cannon→CannonBall. Guardian/Bruiser/Bastion은 근접(range≤1.5)이므로 **projectile=null 유지** — 폴백 경로 회귀 검증 대상.
5. **에셋 생성 방식**: `AssetDatabase.CreateAsset` 경유 (Phase 1 Defender SO가 수동 YAML 작성 → Phase 2 이후 방식 일관). SO 필드 할당도 SerializedObject로 진행.

---

## P3-02 — 투사체 ECS Component + Move/Hit System

### 결정

6. **맥락 경계 해석 (중요)**: ProjectileHitSystem의 `IncomingDamage` append는 Units 소유 Buffer 쓰기지만, **TRD 2.5.2 규칙 2("맥락 간 이벤트는 Buffer")의 이벤트 채널**로 해석한다. 기존 AttackSystem(Combat→Units append)과 동일 패턴. Phase 0 decision #13(Units lifecycle 예외)과는 별개의 "이벤트 채널 예외"이며 phase0/phase2의 암묵적 수용을 Phase 3에서 명문화.
7. **target 유효성 체크**: `SystemAPI.GetComponentLookup<LocalTransform>(true).HasComponent(target)` + `target == Entity.Null` 비교로 해결. `EntityManager.Exists()`는 Burst 비호환이므로 사용 금지.
8. **Move/Hit 분리 이유**: 단일 시스템으로 합칠 수도 있지만 `[UpdateAfter]` 의존성 명시와 BurstCompile 단순성을 위해 2-pass로 분리. 성능은 투사체 수가 적은 Phase 3 규모에서 문제되지 않음.
9. **거리 판정 XZ 평면만**: 도착 판정은 수평 거리만 사용 (y 무시). 체력바/투사체가 유닛 위에 떠 있어도 충돌 감지에 영향 없음.
10. **ProjectileRef의 int assetIndex**: managed Mesh/Material을 IComponentData에 담을 수 없는 제약을 int 인덱스 + BattleBridge 캐시 리스트로 우회. BattleBridge는 `_projectileRenderByIndex: List<RenderMeshArray>` 보유.
11. **ProjectileSpawnRequest 드레인 경로 분리**: AttackSystem(Burst ISystem)은 struct Component ECB 부여만 수행. 실제 엔티티 생성(RenderMeshUtility.AddComponents 포함)은 BattleBridge.Update에서 드레인. 이원화 경로 확정.
12. **Non-stackable 스냅샷**: ProjectileState.damage는 발사 시점 DamageBoost 반영값으로 고정. 비행 중 boost 만료해도 피격 데미지 변동 없음 = "물리적 발사체" 모델.

### 검증

- EditMode 테스트 4건 신규(ProjectileSystemTests): 이동 진행·target 소실 시 파괴·도달 시 데미지+소멸·범위 밖 무시.
- 회귀 포함 23/23 pass.

---

## P3-03 — AttackSystem 투사체 분기

### 결정

13. **분기 기준**: `projectileRefLookup.HasComponent(defenderEntity)` 유무. 단일 if/else, 전략 패턴/인터페이스 도입 없음 (TRD 5.2).
14. **쿨다운 리셋 위치 불변**: AttackSystem이 발사 성공 시 항상 수행. ProjectileHitSystem은 AttackState에 쓰기 않음.
15. **폴백 경로 보존**: `HasComponent<ProjectileRef>` false일 때 기존 즉시 데미지 경로 그대로 — 기존 EffectIntegrationTests(Combat × DamageBoost/CDR)가 수정 없이 통과. Guardian/Bruiser/Bastion이 이 경로로 계속 동작.
16. **emittedDamage 단일 계산**: `attack.damage * damageMul` 을 한 번 계산 후 투사체/폴백 양쪽에 동일하게 사용. 코드 중복 제거.

---

## P3-04 — BattleBridge 투사체 생성/렌더 연동

### 결정

17. **투사체 RenderMeshArray 캐시 키 = (ProjectileData, Material)**: `visualMaterial`이 null일 때 defender.visualMaterial로 폴백 → 같은 ProjectileData라도 쏘는 defender의 색에 따라 다른 RenderMeshArray 필요. 튜플 Dictionary로 해결.
18. **Mesh 폴백**: ProjectileData.visualMesh가 null이면 built-in Sphere 사용. 에셋 추가 없음.
19. **LocalTransform.FromPositionRotationScale로 초기 스케일 부여**: 투사체 visualScale은 SpawnRequest에서 전달받아 LocalTransform.Scale로 설정. 이후 ProjectileMoveSystem이 Position만 수정(Scale 불변).
20. **drain은 Update 내부, DrainGoalEvents 직전**: 투사체가 생성된 같은 프레임에 Move/Hit이 돌지 않도록 순서 유의. 실제로는 SimulationSystemGroup이 같은 프레임에 돌지만 1틱 지연 효과 허용.
21. **Teardown 확장**: ProjectileTag 엔티티 파괴 쿼리 추가. Restart/Redraft 시 잔여 투사체 0.

---

## P3-05 — 체력바 (Units 맥락, ECS 쿼드)

### 결정

22. **맥락 = Units**: 소유권 = Health (Units 소유). `Battle/Units/HealthBar/` 서브폴더 신설. "Visual 유틸 별도 폴더"/World-space Canvas 옵션은 전부 폐기.
23. **우형 = 공유 Cube + unlit 녹색 Material**: 전체 유닛에 1개 material + 1개 RenderMeshArray. 런타임 동적 생성 후 OnDestroy에서 `Destroy(material)` 회수. URP/Unlit 셰이더 + BaseColor(0.2, 0.95, 0.2).
24. **Uniform Scale**: LocalTransform 기본(uniform scale only) 사용. 체력 비율로 Scale 전체 축소. PostTransformMatrix 도입 안 함 — Phase 3 가독성 충분. Non-uniform은 필요 시 Phase 4에서 검토.
25. **생성 경로**: BattleBridge.SpawnUnit(공격) / PlaceDefender(방어) 말미에서 `CreateHealthBar(owner, yOffset=0.9, baseScale=0.35)`. BattleBridge가 책임 (엔티티 lifecycle → Units 맥락 내부 처리 경계 준수).
26. **파괴 경로**: HealthBarSystem이 owner의 Health/LocalTransform 부재 시 ECB 자체 파괴. Teardown에도 HealthBarTag 파괴 쿼리 추가 (명시적 double-safety).

---

## P3-06 — 피격 피드백 (HitFlash)

### 결정

27. **스케일 펀치 방식 채택**: 색상 flash 대비 Entities Graphics에서 구현 부담 적음 (MaterialColor override 없이 LocalTransform.Scale만 조작).
28. **Duration const 허용**: `HitFlashDuration = 0.15f`를 ProjectileHitSystem과 HitFlashSystem에 const로. TRD 5.3 "하드코딩 금지"는 "튜닝 대상"에 적용 — Phase 3 한정 허용으로 PHASE3.md §4에 명시. 플레이어 튜닝이 필요해지면 SO 승격.
29. **중복 hit 처리**: 타깃에 이미 HitFlashTag가 있으면 `SetComponent`로 `remaining` 갱신 + `originalScale` 보존. 없으면 새로 AddComponent. 연타 시 "펑펑" 보이되 원래 스케일은 첫 hit 시점 값으로 고정 → 스케일 드리프트 방지.
30. **PeakBonus = 0.2f**: 1.0 → 1.2배로 순간 팽창. 자연 감쇄로 원래 값 복귀. 튜닝 필요 시 const 조정.

---

## P3-07/P3-08 — 테스트·회귀

### 결정

31. **테스트 커버리지**: ProjectileMove 2건 + ProjectileHit 2건 = 신규 4건. HealthBar/HitFlash는 시각 피드백 시스템이며 EditMode 테스트가 부자연스러워 스킵 — 사용자 플레이 검증으로 대체.
32. **기존 테스트 무수정 통과**: EffectIntegrationTests.Combat_Applies_DamageBoost_... 는 폴백 경로를 검증하므로 ProjectileRef가 없는 엔티티 컨텍스트에서 그대로 통과. 회귀 0.
33. **로그 스키마 불변**: Phase 3은 BattleLogSchema 손대지 않음. 투사체 발사/피격 로그 기록은 Phase 4 시너지 단계에서 필요성 재검토.

### 검증 결과

- EditMode 23/23 pass (기존 19 + 신규 4), 1.81s.
- 사용자 플레이 검증: 투사체 비주얼 ✓, 체력바 ✓, HitFlash ✓, Restart 잔여 0 ✓.
