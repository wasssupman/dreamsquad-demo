# Phase 4 Decisions Log

> Phase 4 (배치 시 효과 / 인접 시너지 / enemy→defender 공격 / Splash onHit) 결정 누적.

---

## P4-01 데이터 스키마 확장

1. **OnPlaceEffectType / OnHitEffectType enum 위치**: 각각 `Wassup.Data` 네임스페이스에 둬서 SO 필드와 같은 파일 맥락. 별도 Enums.cs 분리 안 함.
2. **SO 수치 할당**:
   - Guardian onPlace=BoostNearbyDefenders (r=1.5, m=1.3, d=6s)
   - Scout onPlace=SlowPulse (r=2.5, m=0.7, d=3s)
   - Bastion onPlace=BoostNearbyDefenders (r=1.5, m=1.2, d=10s)
   - Tanker attack(dmg=10 → 20, r=1.2 → 1.5, cd=1.5 → 0.6) 튜닝 후
   - Basic attack(dmg=5 → 10, r=1.0 → 1.5, cd=1.0 → 0.5) 튜닝 후
   - Swift attack 비활성 유지 (passive 통과 적 보존)
   - CannonBall Splash (radius=1.2, damageMul=0.5)
3. **ProjectileState 데드 필드 복귀 근거**: Phase 3에서 제거했던 onHitEffect/splashRadius/splashDamageMul이 Phase 4에서 Splash 실 사용과 함께 복귀. 데드 필드 금지 원칙은 "실 사용 없는 필드 추가 금지"이며 복귀는 해당 원칙 위반 아님.

## P4-02 AttackSystem SynergyBuff + onHit 전달

4. **SynergyBuff HasComponent 폴백**: SynergyBuff 부재 시 synergyMul=1f. Phase 2 DamageBoost/CDR과 동일 패턴.
5. **곱셈 순서**: `emittedDamage = attack.damage * damageMul * synergyMul`. Base × Boost × Synergy.
6. **ProjectileRef onHit 필드 전달**: AttackSystem이 ProjectileSpawnRequest에 복사, 역매핑 없음.

## P4-03 `_defenderByTile` 튜플화

7. **Dictionary 값 타입 변경**: `<Vector2Int, Entity>` → `<Vector2Int, (Entity entity, DefenderUnitData data)>`. 시너지 재계산 시 같은 타입 비교에 DefenderUnitData 레퍼런스 필요.
8. **호출처 5곳 마이그레이션**: field 선언, StartBattle Clear, CastSkillOnDefender TryGetValue, PlaceDefender set, 신규 RecomputeSynergyFor foreach. CastSkillOnDefender는 `.entity` unwrap 추가.

## P4-04 RecomputeSynergyFor + EffectSpawner.SetSynergy/RemoveSynergy

9. **SynergyBuff 쓰기 창구 = EffectSpawner**: Phase 2 decision #9 일관성. BattleBridge가 직접 AddComponent 금지, 반드시 `EffectSpawner.SetSynergy` / `EffectSpawner.RemoveSynergy` 경유.
10. **SynergyPerNeighbor = 0.1f const**: Phase 4 한정. 플레이어 튜닝 필요해지면 SO 승격.
11. **양방향 전파**: 배치 셀 자신 + 4방향 인접 5개 셀 재계산. 각 셀에서 4방향 이웃 수 count.
12. **activations HashSet dedup**: `_synergyActivatedEntities`에 Add 성공한 경우만 `_synergyActivations++`. 같은 엔티티가 여러 번 시너지를 얻어도 1회로 집계. 새 엔티티(다른 Entity id)는 별도 카운트.
13. **peakCount API**: RecomputeSynergyFor 직후 `CreateEntityQuery<SynergyBuff>().CalculateEntityCount()` 측정 후 Max. 매 틱 측정 아님.

## P4-05 onPlace 발동 + 순서

14. **PlaceDefender 순서**: onPlace → RecomputeSynergyFor → Log. onPlace는 주변 스냅샷이므로 자신의 SynergyBuff 상태와 무관.
15. **BoostNearbyDefenders 자신 포함**: 자율 결정 허용됐으나 포함하기로 확정 — "강한 피드백" 방향.
16. **SlowPulse 대상**: 반경 내 모든 AttackUnitTag. EffectSpawner.ApplySlow로 위임.

## P4-05.5 명시적 배치 선택 (UX 개선)

17. **Random placement 폐기**: Phase 0 decision #18(random from pool) 번복. Phase 4 핵심이 "배치 결정의 깊이"이므로 player가 직접 defender 타입을 선택. 시너지/onPlace가 의도적 전략이 됨.
18. **UI 형태**: DefenderSelector MonoBehaviour, SkillBar 런타임 빌드 패턴 재사용. 좌하단 7슬롯.
19. **GameManager.SelectedDefender 상태 공유**: PlacementInput과 DefenderSelector 직접 참조 대신 GameManager property 경유.
20. **PlaceDefenderAs 신규 메서드**: 명시적 타입 받음. 기존 PlaceDefender(random)은 테스트/폴백용 유지.
21. **draft confirm 시 첫 슬롯 자동 선택**: 즉시 배치 가능.

## P4-06 DefenderDeathEvent 경로

22. **GoalReachedEvent 패턴 복제**: DefenderDeathEvent + DefenderDeathEventsSingleton + NativeQueue. BattleBridge 라이프사이클 3지점(Start/Teardown/OnDestroy).
23. **UnitLifecycleSystem 쿼리 분리**: `DeadTag + DefenderUnitTag + DefenderTile` 신규 루프에서 enqueue → DestroyEntity. 기존 일반 DeadTag 루프에 `.WithNone<DefenderTile>()` 필터로 중복 파괴 방지.
24. **DrainDefenderDeathEvents 순서**: Update 내에서 SpawnRequest 드레인 → DefenderDeath 드레인 → GoalReached 드레인. 사망이 goal 체크보다 먼저 반영돼 재계산 기반 일관.
25. **_occupiedTiles 해제**: Drain 시 `_defenderByTile.Remove(cell)`와 함께 `_occupiedTiles.Remove(cell)` 호출 — 죽은 타일에 재배치 가능.

## P4-07 적→방어 공격

26. **AttackUnitData 확장**: attackDamage/attackRange/attackCooldown. attackDamage<=0이면 passive 유지(Swift 그대로).
27. **SpawnUnit에서 AttackState 조건부 부여**: `entry.unitType.attackDamage > 0f`일 때만 AttackState. 없으면 AttackSystem 공격자 루프가 건너뜀.
28. **defender에 IncomingDamage 버퍼**: PlaceDefenderAs에서 추가. DamageApplicationSystem이 기존대로 소비 → Health 감소 → DeadTag 경로.
29. **AttackSystem 공격자 루프**: defender snapshot 별도 수집. 방어→적 루프와 같은 OnUpdate 내 순차 처리. Boost/Synergy/Projectile 분기 **없음** — 적 데미지는 raw damage only.
30. **Tanker/Basic 수치 튜닝 (P4-07 테스트 중)**: dmg 20/10, range 1.5/1.5, cd 0.6/0.5. 체력 낮은 defender가 1~2회 공격에 죽도록.

## P4-08 ProjectileHitSystem Splash AOE

31. **스냅샷 패턴**: onUpdate 시작부에서 AttackUnitTag+LocalTransform 엔티티를 ToEntityArray+ToComponentDataArray로 스냅샷. projectile iteration 내부에 nested query 없음(Entities 1.4.x 안전).
32. **직격 제외**: 스플래시 루프에서 `if (candidate == target) continue;`로 직격 타깃 2중 데미지 방지.
33. **AttackUnitTag 필터**: WithAll 필터로 HealthBar 등 다른 엔티티가 splash 대상에서 자동 제외.
34. **splashDamageMul 기본 0.5**: CannonBall 기본. ProjectileData SO 필드로 튜닝 가능.
35. **SpawnProjectile 단방향 복사**: req의 onHit 필드를 ProjectileState로 그대로 복사. assetIndex로 ProjectileData 역조회 금지.

## P4-09 로깅 v4

36. **phase 기본값 "phase4"**: Phase 3은 스키마 무변경이라 "phase2" 유지했었으나 Phase 4가 synergy/on_place_usages 신규 → 스키마 명시적 전환.
37. **SynergyRecord / OnPlaceUsageLog 추가**: BattleLogEntry에 synergy + on_place_usages 필드.
38. **BattleLogger.RecordOnPlace / SetSynergyStats**: 부가 메서드. DraftController/BattleBridge 호출 연결.

## P4-10 테스트

39. **신규 테스트 2건**:
   - `ProjectileSystemTests.Hit_Splash_Damages_Neighbors_Excluding_Direct_Target_And_Non_AttackUnit` — 직격 100, 반경 내 50, 범위 밖 0, 비-AttackUnit 0.
   - `EffectIntegrationTests.Combat_Attacker_With_AttackState_Damages_Defender_In_Range` — 적 공격이 defender IncomingDamage에 attack.damage 그대로 추가.
40. **테스트 총 26/26 pass** (기존 23 + Phase 4 신규 3건).

## P4-11 수동 플레이 회귀

41. 전 플로우(드래프트→확정→SelectorUI→onPlace→적공격→Splash→Restart/Redraft) 사용자 수동 통과 확인. 콘솔 에러 0.
