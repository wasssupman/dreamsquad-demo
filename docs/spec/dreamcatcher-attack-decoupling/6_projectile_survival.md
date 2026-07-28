# 6 — 투사체 생존 규칙: 방향탄 bounce 개통 + 대상 사망 재조준

## 목적

이 spec 의 **원래 동기**를 닫는다 — 통통구슬(`ProjectileBounce`)이 머신거너에서 안 걸리는 문제.
unit 1 은 이를 "거절"로 정직하게 표시만 했고, 개통은 별도 spec 으로 미뤄뒀다. 그 미룸을
철회하고 여기서 개통한다(`defender-directional-volley/README.md:79` 사용자 결정 = "차단이
아니라 개통").

같은 자리에서 두 번째 생존 규칙도 연다: **대상이 먼저 죽으면 재조준**. 비수처럼 N회에 한 번
나오는 자원이 대상의 죽음으로 통째로 증발하던 문제다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs` — PathHit arm 꼬리
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileMoveSystem.cs` — Homing arm 재조준
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileState.cs` — `retargetTileRange`
- `Assets/_Project/Scripts/Core/Dreamcatcher/DcApplicability.cs` — bounce 게이트에 Directional 추가
- `Assets/_Project/Tests/EditMode/ProjectileRetargetAndBounceTests.cs` (신규)

## 구현

**① 방향탄 bounce.** `PayloadKind.PathHit` arm 이 스윕을 끝낸 뒤, 더 뚫을 수 없게 된 순간
(pierce 예산 소진 **또는** 사거리 끝) `bounceRemaining > 0` 이고 이번 프레임에 맞힌 적이
있으면 그 적을 기준점으로 `BounceRetarget.FindNext` 를 돌려 **호밍으로 전환**한다
(`movement = HomingToEntity`, `payload = SingleSplash`). 엔티티를 유지하므로 뷰/트레일이
끊기지 않는다.

머신건 탄은 `pierceCount: 1` — **관통이 없다**. 실사용 형태는 "관통하다 튕김"이 아니라
"**맞히고 튕김**"이다. `pierce > 1` 탄이 생기면 같은 코드가 전자가 된다.

전환 시 `AttackOutputElement` 스냅샷을 떼어 Damage-only 계약을 유지한다 — PathHit arm 은
`state.damage` 만 쓰지만 SingleSplash arm 은 outputs 가 있으면 전 kind 를 디스패치해서,
그대로 두면 "경로 히트엔 안 걸리던 슬로우가 바운스 홉에만 걸리는" 비대칭이 생긴다.

**② 재조준.** Homing arm 이 대상 소실을 감지하면(파괴 **또는** `DeadTag`) 파괴 대신,
`retargetTileRange > 0` 일 때 현재 위치 반경 안에서 다시 겨눈다. `DeadTag` 까지 보는 이유:
`DamageApplicationSystem` 은 그 태그가 붙는 순간부터 데미지를 뽑지 않아, "죽었지만 아직 파괴
전" 창에 도착한 투사체는 시체를 때리고 증발한다.

## 계약

1. **바운스는 마지막 히트 프레임에서만.** 아무도 못 맞히고 사거리 끝에 닿으면 튕길 기준점이
   없어 그대로 소멸한다. 프레임을 넘겨 `lastVictim` 을 기억하는 상태는 만들지 않는다.
2. **`PathHitRecord` 를 승계하지 않는다.** 전환 후엔 SingleSplash 라 그 버퍼를 읽지 않는다 —
   `pierce > 1` 탄이 A→B 를 뚫고 B 에서 A 로 되튕길 수 있다(SingleSplash 바운스의 A→B→A 선례).
3. **재조준은 opt-in.** `retargetTileRange` 기본 0 = 기존 투사체(화살 등) 동작 불변.
4. **Ballistic/Grenade 는 여전히 비적용.** 착탄 셀이 발사 시점에 고정돼 재조준할 대상이 없다.
   `NeedsHomingRoute` 의 지금 의미는 "Homing 전용"이 아니라 "**착탄 고정 경로는 불가**"다.

## 완료 기준

- [x] EditMode green — `ProjectileRetargetAndBounceTests` 6건 포함 1480 pass / 0 fail
- [x] 통통구슬이 머신거너에 **부착된다**(`EvaluateAttackMod` 가 Directional 허용)
- [x] Play — 머신건 탄이 첫 적을 맞히고 다음 적으로 튕기는 것을 육안 확인
- [x] Play — 비수 대상이 먼저 죽었을 때 투사체가 사라지지 않고 다시 겨누는 것을 육안 확인

사용자 확인 완료 2026-07-28 · `7c19b2bd`(개통) + `f4102f67`(재조준·마감)

### 함정 (재현 방지)

테스트에서 PathHit 투사체를 손으로 만들 때 **`PathHitRecord` 버퍼를 반드시 붙인다**
(`BattleBridge.cs:3409` 가 프로덕션에서 하는 일). 없으면 `ecb.AppendToBuffer` 가 playback
중간에 끊겨 **뒤따르는 `SetComponent`/`DestroyEntity` 가 통째로 유실**된다 — 증상은
"데미지는 들어갔는데 상태만 안 바뀐" 유령이라 프로덕션 로직 결함으로 오진하기 쉽다.
