# 3 — 실드셔틀 에셋 저작 + 카탈로그 등록 + Play 검증

## 목적

`Defender_ShieldShuttle.asset` 을 저작·등록하고 실드 메커니즘 전체를 Play 로 실증한다.

## 변경 대상

- 신규 `Assets/_Project/Data/Defenders/Defender_ShieldShuttle.asset` (+ .meta 짝 커밋)
- `Assets/_Project/Data/DefenderCatalog.asset` — 등록

## 구현

스탯 (설계 확정치, 전부 SO):

| 필드 | 값 | 비고 |
|---|---|---|
| id / displayName | `shield_shuttle` / 실드셔틀 | desc: "가디언 · 근접형. 아군에게 실드를 셔틀한다." |
| role / cost / rarity | 2(가디언) / 4 / 2(에픽) | guardian(cost2)·bastion(cost3) 위 티어 |
| health / aggroCapacity | 1200 / 2 | 어그로 용량은 기존 가디언과 동일 |
| attackRange / cd / damage | 2 / 2.0 / 20 | 근접 저딜 — 어그로 유지용 공격 |
| **shieldCastCooldown (A)** | **4.0** | |
| **shieldAmount (B)** | **150** | 재부여 max 갱신 |
| **shieldTargetCount (C)** | **2** | |
| **shieldTargetFilter** | **MinHealth** | 위험한 아군 우선 — 유닛 정체성 |
| projectile / knockback / sleepOnHit / directional / hazard / onPlace | 없음/0/0/0/비활성/None | |

비주얼: Casual Character 파츠 재조합(방패 계열 gear 우선 탐색) + 초상/배치VFX/보이스는 기존 가디언 계열 placeholder(guid 유지 교체 전제). 컷씬 없음.

## 완료 기준

- [ ] 로비 스쿼드 페이지 노출 + 편성 가능.
- [ ] Play: 배치 4초 후 자신+주변 아군에 체력바 실드 세그먼트 표시(MinHealth 우선).
- [ ] Play: 피격 시 실드 먼저 소진(HP 불변) → 실드 깨진 뒤 HP 차감. 완전 흡수 히트는 데미지 넘버 미표시.
- [ ] Play: 소진 후 다음 주기(≤4s)에 재부여, 같은 셔틀의 재부여는 max 갱신(출처당 150 초과 없음).
- [ ] Play: 실드셔틀 2기 배치 → 같은 아군에 교차 출처 합산(세그먼트가 300 스케일로 표시), 각자 재부여해도 출처당 150 유지.
- [ ] Play: 어그로(적 끌어오기) 기존 가디언과 동일 동작. 콘솔 에러 0.
