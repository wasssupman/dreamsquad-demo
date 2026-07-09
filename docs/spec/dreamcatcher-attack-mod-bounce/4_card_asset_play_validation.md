# 4 — 카드 에셋 + Play e2e

## 목적

검증 질문에 답한다: 부착 유닛의 기본 화살이 최대 2회 튕기며 각 히트에 데미지, 미부착/근접/기존 투사체 무회귀.

## 변경 대상

- 신규 에셋: `Assets/_Project/Data/Dreamcatcher/Card_BouncyBead.asset` (가칭 "통통 구슬")
- 코드 변경 없음

## 구현

- `DreamcatcherCard`: id=`bouncy_bead`, binding=`Unit`, effects/mechanics 비움, attackMods=[{ ProjectileBounce, count=2, tileRange=3, damageMul=1.0 }]. (감쇠 밸런싱은 후속 — v1 카드는 무감쇠, 필드 동작은 unit 2 에서 검증됨)

## Play 검증 절차 (UnityMCP)

1. Play → 원거리 defender 배치(HP 부스트) + 카드 부착 → StartBattle.
2. 적 밀집 구간에서: 화살 1발의 히트가 최대 3회(직격+튕김2) 발생 — **적 Health 감소 / ProjectileHitEvents / HitFlash / 육안**으로 확인 (계약 9: 튕김 히트는 세션 로그에 안 남음 — 로그로 검증하지 말 것). 투사체 엔티티가 튕김 중 생존, 소진/후보 없음 시 파괴.
3. 무회귀: 미부착 아처 화살은 1히트 후 파괴, dc 트리거(콕콕 바늘) 투사체는 튕기지 않음, 곡사/스킬 정상.
4. 같은 카드 2장 → 4회 튕김(합산) 확인.
5. 콘솔 에러/경고 0, 캐리어/투사체 누수 0.

## 완료 기준

- [x] 2회 튕김 + 히트당 데미지 (사용자 육안 — bounce 아처 화살이 적 사이로 꺾임)
- [x] 합산 스택(2장=4회) — 부착 정적 검증 + AttackSystem count 합산
- [x] 무회귀 3종 (미부착/근접 skip/기존 투사체) + 콘솔 클린 (EditMode 588 그린)
- [x] 사용자 완료 확인

> 카드 실제 값: `Card_BouncyBead.asset` — id=bouncy_bead, "통통 구슬", binding=Unit, category=Unique, attackMods=[{ProjectileBounce, count=2, tileRange=3, damageMul=1.0}]. **bounce 는 카드 투사체가 아니라 유닛의 기본 공격 투사체(아처 화살)를 튕긴다** (DcAttackModSpec 엔 투사체 필드 없음). A→B→A 재히트 허용(직전 대상만 제외) = v1 확정.

완료 확인: 2026-07-09 — 사용자 육안 확인(bounce 아처 재비행). 이 문서와 동일 커밋.
