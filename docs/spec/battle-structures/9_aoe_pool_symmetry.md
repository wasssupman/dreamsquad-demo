# unit 9 — 광역 피해풀 진영 대칭

## 목적

**광역 피해(메테오 · TileAoe · splash)가 적 거점에 먹게 한다.** 승리 조건에 필수는 아니다 — unit 8 의 근접·호밍만으로도 적 마음을 깎을 수 있다. 그래도 하는 이유는 빼면 **조용한 무효**가 남기 때문이다: 메테오가 적 마음 위에 정확히 떨어져도 0 데미지. 플레이어는 그것을 버그로 읽는다.

**이 병은 이 코드베이스가 이미 한 번 앓았다.** `ProjectileHitSystem:95` 주석 그대로:

> 골은 `AttackUnitTag` 가 없어서(유닛이 아니다) 여기 빠져 있었고, 그 결과 **보스의 AreaBarrage 가 골에 떨어져도 안정도가 한 톨도 안 줄었다.** 근접 공격은 타겟에 직접 append 라 멀쩡했기 때문에 증상이 "보스만 타워를 못 부순다" 는 형태로 조용했다.

그때 **방어** 풀만 `WithAny<DefenderUnitTag, GoalTowerTag>` 로 고쳤다. **적** 풀은 `WithAll<AttackUnitTag>` 그대로다 — 같은 증상이 진영만 바꿔 남아 있다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs` — 두 풀 스냅샷 + victim 스윕 2곳(splash · TileAoe)

## 구현

**스냅샷 2벌 → 1벌 + 진영 비트 필터.**

ECS 쿼리는 `FactionTag` 의 **값**으로 필터할 수 없다(shared component 가 아니다). 그리고 `StructureTag` 는 양 진영 공용이라 태그 조합만으로는 «적 거점» 을 뽑을 수 없다. 그래서 값 필터가 불가피하고, 값 필터를 쓸 거라면 스냅샷을 두 벌 뜰 이유가 없다.

```
victimQuery = WithAny<AttackUnitTag, DefenderUnitTag, StructureTag>
              WithAll<LocalTransform, FactionTag>
              WithNone<UltimateLeapState>

스윕: wantMask = hitsDefenders ? Factions.AnyDefender : Factions.AnyEnemy
      (faction & wantMask) == 0 → continue
```

- 스냅샷이 1벌로 줄어 배열 할당이 감소한다. 추가 비용은 이미 거리 검사가 도는 루프 안 비트 검사 한 줄.
- **`GoalTowerTag` 특례가 은퇴한다** — 방어 마음은 `StructureTag` + `DefenderCore` 비트로 걸린다. 미래의 방어 본능도 코드 변경 0으로 걸린다(현행은 못 걸린다).
- `BlockingHazard`(방벽)는 `AnyDefender`/`AnyEnemy` 어디에도 없다 → 지금처럼 광역 피해자가 아니다. **동작 보존.**

⚠ **`WithNone<UltimateLeapState>` 가 방어 풀에도 걸리게 된다.** 현재 그 제외는 적 풀에만 있다. 구현 시 `UltimateLeapState` 가 어느 진영에 붙는지 확인하고(시전자 = 적으로 보인다 — `UltimateLeapSystem:82` 가 `targetFaction = Defender`), 방어유닛에 붙을 수 있다면 통합 쿼리에서 그 제외를 **victim 스윕 시점 조건으로 내린다**. 조용히 방어유닛이 광역에서 빠지는 회귀가 이 unit 의 유일한 실질 위험이다.

## 완료 기준

- 컴파일 0
- EditMode 신설 3케이스:
  - 방어 광역(`targetFaction = Enemy`)이 **적 마음·적 본능을 깎는다** (신규 동작)
  - 같은 광역이 **방어 마음을 깎지 않는다** (자기편 오폭 금지 — 통합 풀의 최대 위험)
  - 보스 광역(`targetFaction = Defender`)이 **방어 마음을 깎는다** (`GoalTowerTag` 특례 은퇴 후에도 `:95` 가 고친 동작이 보존됨)
- EditMode 전량 무회귀 (기준선 2049 / 실패 0 / 의도적 스킵 3)
- 기존 PlayMode 골 3종 그린 — 침략 맵에서 방어유닛이 광역 피해자로 남아 있는지가 여기서 잡힌다

---
확인 2026-08-10 · `9537a91d` — EditMode 2060/실패 0(신설 3개 통과: 적 거점 편입 · 자기편 오폭 금지 · 방벽 무회귀) · `UltimateLeapState` 는 적 전용이라 방어 측 통합에 영향 없음(`HealthThresholdSystem:190`)
