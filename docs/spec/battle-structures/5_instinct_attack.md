# unit 5 — 본능 공격 (투사체 1발)

## 목적

본능이 쏜다. **전용 공격 시스템을 만들지 않는다**(계약 10) — 유닛과 같은 파이프라인(`AttackState` + `AttackOutputElement` + `ProjectileRef`)에 베이크만으로 합류한다.

성립 근거(실측): `AttackSystem` 의 통합 공격자 루프는 `AttackState + LocalTransform` 만 요구한다(`WithNone<PendingDeployment>`). 적 원거리(Sniper 계열)가 이미 «호밍 투사체 → 방어유닛 직격» 으로 라이브를 돈다 — 본능은 움직이지 않는 그 형태다. 방어유닛 전용 분기(힐 랭크·frontmost·facing)는 전부 `DefenderUnitTag` 게이트라 본능에 닿지 않고, CC 잠금은 버퍼 부재(계약 8)로 자연 비활성이다.

**행동 변화**: 공격이 저작된 본능이 있는 맵에서만. 미저작 = 0.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SpawnStructureEntities` 의 본능 분기에 공격 베이크
- `Assets/_Project/Tests/EditMode/StructureSpawnAndBreachTests.cs` — 베이크·발사 테스트

## 구현

`SpawnStructureEntities` 본능 분기, `attackDamage > 0` 일 때:

```
AttackState {
    range/cooldownDuration = SO, cooldownRemaining = 0, attackTargetCount = 1,   // v1 = 1발 고정
    targetMask = EnemyTargetDefaults.Resolve((int)data.targetFactions),          // 저작 마스크 재사용(unit 1 과 같은 축)
}
AttackOutputElement { Damage, magnitude = attackDamage }
ProjectileRef { GetOrCreateProjectileDataIndex(data.projectile) + 적 Projectile 베이크와 동일 필드 }
```

- `projectile == null` 인데 `attackDamage > 0` 이면 **경고 + walk-only 베이크**(적 베이크의 «outputs empty → walk-only» 선례) — 조용한 미발사를 막는다.
- 마음(`Core`)은 무변경 — `AttackState` 미부여(README 파이프라인 표 그대로).

### 계약 11 대조 — 저작 마스크 vs 피해풀

v1 본능의 탄은 **직격 호밍**(`HomingToEntity`)만이다. 직격은 타겟 엔티티 직결이라 `ProjectileTargetFaction`(피해풀 선택)이 판정에 관여하지 않는다 — 저작 마스크가 겨눈 것이 곧 맞는 것이고, 두 축이 갈릴 자리가 없다.

⚠ **splash·TileAoe 가 붙은 투사체 SO 를 본능에 물리지 말 것.** 통합 루프의 요청은 `targetFaction` 을 싣지 않아 기본값(Enemy 풀)으로 떨어진다 — 적 본능의 광역이 적을 때리는 오귀속이 된다. 이는 적 원거리 유닛에도 동일한 기존 한계이고, 본능 광역이 필요해지면 그때 host 진영 도출(BossLeap 선례)을 통합 루프에 넣는다 — 후속 후보.

## 완료 기준

- [x] 컴파일 에러 0 · 콘솔 신규 에러 0
- [x] EditMode 전량 그린 — **2042개 / 실패 0 / 의도적 스킵 3**(기준선 2039 + 신규 3)
- [x] 공격 저작 본능 스폰 → `AttackState`(저작 마스크 Resolve)·출력·`ProjectileRef` — `ArmedInstinct_BakesAttackPipeline_WithAuthoredMask`
- [x] **발사 실증** — `ArmedInstinct_FiresProjectileRequest_AtDefenderInRange`. 실 `AttackSystem` 1틱에 브리지가 세운 본능이 사거리 내 방어유닛을 겨눈 `ProjectileSpawnRequest` 를 받는다
- [x] 미저작(damage 0) 본능 무공격 — unit 4 의 `AuthoredInstinct_SpawnsWithSoHp_AndNineBlockedCells`(damage 미저작) 무회귀 통과
- [x] projectile 미지정 + damage > 0 = 경고 후 무공격 — `ArmedInstinct_WithoutProjectile_WarnsAndBakesUnarmed`
- [ ] 리뷰: 스펙 종료 시점 투트랙(4~6 묶음)에 합류

---

**확인 2026-08-09** — 구현 커밋: (아래 커밋 해시). 코드 변화는 `SpawnStructureEntities` 본능 분기의 베이크 블록 하나 — 계약 10(«전용 공격 시스템을 만들지 않는다»)이 그대로 이행됐다.
