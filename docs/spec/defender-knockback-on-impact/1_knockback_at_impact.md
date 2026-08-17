# 1 — 유도탄 넉백을 착탄 시점으로 옮기고, 방향을 「적 진행 반대」로 (Combat)

## 목적

「화살이 맞기도 전에 밀린다」를 없앤다. 그리고 미는 방향을 사수의 위치와 무관하게 만든다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — 발사 시점 발동을 미루고 방향 교체
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs` — 착탄 시점 발동
- `Assets/_Project/Tests/EditMode/AttackSystemUnifiedLoopTests.cs` (U3 갱신 · U3b · U3c)
- `Assets/_Project/Tests/EditMode/ProjectileSystemTests.cs` (착탄 넉백 2건)

## 구현

### 발사 쪽 (`AttackSystem`)

`bool knockbackAtImpact` 를 공격 1회 스코프에 둔다. 유도탄 분기에서 `payload == SingleSplash`
일 때만 켜고, 넉백 블록은 켜져 있으면 건너뛴다.

직격 victim 이 없는 payload(`TileAoe` 등)는 **넘길 곳이 없으므로** 켜지 않는다 — 그쪽은
기존대로 발사 시점에 건다. 조용히 사라지는 것보다 타이밍이 옛날인 편이 낫다.

방향은 `-normalize(PathFollowState.lastMoveDir)` 로 교체(근접 경로도 같이). 은퇴한 식과
그 결함은 README ② 참조. `lastMoveDir` 이 0 이면 **아무것도 쏘지 않는다**.

### 착탄 쪽 (`ProjectileHitSystem`, `SingleSplash` 분기)

피해가 실제로 들어가는 지점 바로 뒤에서, 사수(`ProjectileState.owner`)의 `DefenderCcData` 를
읽어 Impulse 를 enqueue 한다. 신규 컴포넌트 필드 0 · 신규 채널 0(기존 `EnemyCcEvents`).

```
속력 = knockbackDistance / knockbackDuration
방향 = -normalize(victim.lastMoveDir)
```

**⚠ 탄의 진행 방향을 쓸 수 없다.** 유도탄은 도착할 때 좌표가 대상과 정확히 같아진다
(`ProjectileMoveSystem`: `dist <= step → newPos = targetPos`) — 진행 벡터가 0 이 된다.
이것이 「방향은 피격자에게서 뽑는다」가 선택이 아니라 **제약**인 이유다.

훑는 탄(PathHit)의 넉백은 손대지 않는다. 그쪽은 탄 SO 소유이고 방향도 스윕 방향이며,
「지나가며 밀어낸다」가 곧 그 능력이다(부메랑이 두 다리에서 반대 힘이 되는 것이 그 결과).

### 저작 위치는 왜 유닛인가

넉백은 **유닛의 성질**이다. 탄 SO 로 옮기면 같은 화살(`Projectile_Arrow13_GA`)을 공유하는
밀당맨과 마크스맨이 함께 밀게 된다. 유닛이 화살을 바꿔도 넉백은 따라가야 한다.

## 완료 기준

- [x] compile 에러 0
- [x] `Hit_EmitsOwnerKnockback_OppositeVictimTravel` — 착탄 순간에 1회, 방향 = 진행 반대
- [x] `Hit_WithoutOwnerKnockbackAuthoring_EmitsNothing` — 기존 투사체 무변화
- [x] `U3c_ProjectileDefender_DefersKnockbackToImpact` — 발사 시점에 안 건다(증상 회귀 가드)
- [x] `U3b_Knockback_WithoutTravelDirection_EmitsNothing` — phantom impulse 금지
- [x] EditMode 2318 통과 / 0 실패
- [ ] Play 육안: 밀당맨 앞을 지나는 적이 **맞는 순간** 살짝 뒤로 튕기는지 (사용자 확인 대기)

확인: 2026-08-17 · 코드/테스트 완료, 육안 확인 대기
