# 1 — 투머치토커 에셋 저작 + 카탈로그 등록 + Play 검증

## 목적

`Defender_TooMuchTalker.asset` 을 저작하고 카탈로그에 등록해 로스터에 노출, 수면 잠금을 Play 로 실증한다.

## 변경 대상

- 신규 `Assets/_Project/Data/Defenders/Defender_TooMuchTalker.asset` (+ .meta 짝 커밋)
- `Assets/_Project/Data/DefenderCatalog.asset` — 등록 (미등록 = 로스터 미노출)

## 구현

스탯 (설계 확정치, 전부 SO):

| 필드 | 값 | 비고 |
|---|---|---|
| id / displayName | `too_much_talker` / 투머치토커 | desc: "파이터 · 근접형. 쉴 새 없는 수다로 적을 재운다." |
| role / cost / rarity | 3(근접) / 3 / 2 | CC 계열 캐스터와 동급 레어리티 |
| health | 800 | 브루저(1000) 대비 약체 |
| attackRange / attackTargetCount | 2 / 1 | 근접 표준. 단일 타겟 = 전 히트 수면 |
| attackCooldown / hitDelaySec | 3.0 / 0.3 | 저속 |
| outputs | Damage 35 | DPS ≈ 11.7 (브루저 75) — 가치는 잠금 |
| **sleepOnHitSec** | **3.5** | ≥ 쿨다운 → 상시 잠금 |
| projectile / aggroCapacity / knockback / directionalAttack / hazardCast | 없음 / 0 / 0 / 0 / 비활성 | |
| onPlaceEffect | None | 정체성 순수 유지 (광역 수면 펄스는 후속) |

비주얼 (전부 기존 재사용, placeholder 전제 — guid 유지 교체):

- Spine: Casual Character `full_skins` + 파츠 재조합 — **수다 컨셉 파츠**(입/표정 강조) 위주로 기존 카탈로그에서 선택. 애니 세트는 브루저(근접) 것을 따른다(idle/attack/death/drag/deploy).
- portrait / deployCutsceneFrames / placementVfxPrefab: 기존 유닛(브루저 계열) 것을 placeholder 참조.
- 잠 연출(zzz)은 `StatusFxKind.Sleep` 기존 리컨사일 자동 — 배선 작업 없음.

## 완료 기준

- [ ] 로비 스쿼드 페이지에 투머치토커 노출(카탈로그 직독) + 스쿼드 편성 가능.
- [ ] Play: 배치 → 적 히트 → 대상 zzz + 이동/공격 정지 확인.
- [ ] Play: 혼자 때리는 동안 대상이 계속 잠금 유지(수면 3.5 ≥ 쿨다운 3.0), 파이터가 안 때리면 3.5초 후 자연 기상.
- [ ] Play: 잠든 적을 다른 타워가 히트 → 즉시 기상(wake-on-hit) 확인.
- [ ] 콘솔 에러 0.
