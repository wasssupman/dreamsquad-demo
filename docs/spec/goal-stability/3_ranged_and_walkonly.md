# 3. 원거리 + walk-only 개통 — "모든 적이 공격 가능"

## 목적

투사체 적(5종)이 골을 착탄 피해자로 인식하고, 공격 능력 없는 walk-only 2종(Runner/Swift)에게 골 전용 공격을 부여해 "모든 적이 공격 가능한 최후의 대상"을 완성한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/Projectile/` (`ProjectileHitSystem.cs` 및 targetFaction 판정 지점)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (walk-only 스폰 grant)
- `Assets/_Project/Scripts/Battle/Combat/TauntAttackGrantSystem.cs`

## 구현

1. **원거리 개통**: `ProjectileTargetFaction` 은 Defender/Enemy 2값 enum 이라 골 피해자를 모른다. 구현 전 착탄 경로를 조사해 최소 변경을 택한다 —
   - 단일 타겟(호밍/직격)이 골 엔티티면 그대로 피해 적용되는지 먼저 확인(타겟 엔티티 직결이면 무변경일 수 있음).
   - **AoE 피해자 풀 주의(리뷰)**: `ProjectileHitSystem` 의 TileAoe 피해자 풀은 `AttackUnitTag`/`DefenderUnitTag` 쿼리로 구축되어 골이 **양쪽 모두에 없다** — AoE 탄을 쏘는 적이 골을 타겟으로 발사하면 피해 0 이 된다. 적 발사 탄에 한해 직격 타겟이 골이면 피해가 성립하도록 보장한다(피해자 풀에 `GoalPoint` 후보 포함 또는 직격 보정). defender/bridge-cast 발사 경로는 무변경.
2. **walk-only grant**: 적 스폰 시 `attackMethod == None` 이고 현재 맵에 M>0 골이 존재하면 `AttackState{ targetMask = Goal }` 를 부여 — 수치(dmg/interval/range)는 이미 스폰 시 부착되는 `AggroAttackProfile` 재사용(도발 공격 프로필 선례). 골이 전부 붕괴한 뒤에는 마스크에 유효 대상이 없어 자연 무해(제거 불필요).
3. **도발 병존(리뷰 M2 반영)**: 현재 `TauntAttackGrantSystem` 은 grant 쿼리가 `WithNone<AttackState>` 이고 strip 이 `RemoveComponent<AttackState>` 다 — goal-grant 로 AttackState 를 이미 가진 Runner/Swift 는 도발이 **통째로 건너뛰어지고**, 어찌 걸려도 strip 이 골 마스크째 제거한다. 정리:
   - `TauntAttackGranted` 태그에 `int previousTargetMask` 필드를 부여.
   - grant 분기 2개: (a) `WithNone<AttackState>` — 현행 그대로 부여+제거. (b) `WithAll<AttackState>` + `WithNone<TauntAttackGranted>` — 기존 마스크를 `previousTargetMask` 에 저장하고 `Defender` 비트 OR.
   - strip: `previousTargetMask != 0` 이면 마스크 원복, 0 이면 현행대로 AttackState 제거.

## 완료 기준

- [ ] compile + 기존 EditMode green.
- [ ] Play: 원거리 적이 사거리에서 골 포격(방어유닛이 사거리에 있으면 그쪽 우선 유지), Runner/Swift 가 골 앞에서 공격.
- [ ] 도발(가디언) 중 Runner/Swift 가 방어유닛을 때리고, 도발 종료 후 골 공격으로 복귀.
- [ ] 골 붕괴 후 walk-only 적이 정상 유출(잔존 AttackState 무해 확인).
