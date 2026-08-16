# 6 — 인계 요약 (dreamcatcher-content-4)

## Commit

| 해시 | 내용 |
|---|---|
| `afbda28d` | unit 0 — 공통 어휘 + bake seam (게임 동작 변화 0) |
| `fa3a5eff` | unit 1 — 궤도 궤적 `MovementKind.OrbitAroundPoint` |
| `e87e563a` | unit 2 — 관통 페이로드 재타격 쿨타임 |
| `71da7335` | units 4·5 — 악몽 사냥 · 퇴직 위로금 (카탈로그·이름 레지스트리 공유로 한 커밋) |
| `a630d32e` | unit 3 — 불꽃 팽이 (주기 궤도 화염구) |
| `49554c6d` | 업스트림 머지 (17커밋) |
| `2ef68650` | 투트랙 리뷰 반영 — fail-open 차단 · 통행 층 · 궤도 e2e |
| `a36e784e` | unit 7 — 차폐 소팅 · 즉시 발동 · 체감 튜닝 |

> ⚠ 리뷰 반영 중 `BattleBridge.cs` 의 2건(궤도 스폰 위치 M3 · 적 bake loud 거절 M4)은 그 파일을
> 동시에 편집하던 **타 세션 커밋(`on-place-skill-rework` 계열)에 딸려 들어갔다.** 코드는 정상이고
> 유실 없지만, `git log -- BattleBridge.cs` 로 이 spec 의 이력을 좇으면 그 2건이 안 보인다.

## Implemented

- **불꽃 팽이** — 6초마다 host 주위를 도는 화염구가 5초간 돌며 스치는 적을 0.5초 간격으로 20씩.
  부착 즉시 첫 발동. 유닛 뒤로 돌면 몸에 가린다.
- **악몽 사냥** — 잠든 적을 때린 그 타격이 ×2. 근접은 피해자별(옆의 깨어 있는 적은 그대로).
- **퇴직 위로금** — 퇴근하면 비워진 그 칸에 운석. **사망에서는 안 떨어진다**(그 역도 참).
- 엔진 축 2개: `MovementKind.OrbitAroundPoint`(궤도 수학) · `PathHit` 재타격 쿨타임.
- 방어유닛 **주기 트리거 개방** — 여태 bake 가 `periodSeconds` 를 안 실어 조용히 무발동이었다.

## Key Files

- `Battle/Combat/Projectile/Orbit.cs` — 궤적 수학(위상 0 고정 = 결정론) + `Tangent`
- `Battle/Combat/Projectile/ProjectileMoveSystem.cs` — 궤도 arm (`elapsed` 를 굴리는 주체)
- `Battle/Combat/Projectile/PathHitRecord.cs` · `ProjectileHitSystem.cs` — 재타격 판정/기록
- `Battle/Combat/BossPeriodicTriggerSystem.cs` — 궤도 발사 arm + 이 시스템 최초의 ECB
- `Battle/Combat/AttackSystem.cs` — 수면 배율 2지점 (`AnyActiveSleep`)
- `Bridge/BattleBridge.cs` — `RetireDefender`(슬롯 직독→운석) · `SpawnProjectile`(궤도 분기) ·
  투사체 프레임의 `boardSortOrder`
- `Bridge/BattleBridge.Dreamcatcher.cs` — bake 전량(주기 배선 · 궤도 · OnRetire · 즉시 발동)
- `Presentation/ProjectileViewPool.cs` — 깊이 소팅 적용 + `baseSortingOrders`

## Verified

- EditMode **2446 전량** — 잔여 실패 5건(`MapDocument_*` 4 · Whirlpot 1)은 이 작업과 무관한
  **사전 실패**(해당 에셋은 이 작업에서 수정되지 않았고 재실행에서도 동일하게 재현).
- PlayMode 이 feature 분 **12/12** — 퇴근 교차 무발동 3건 + 수면 배율 2건 포함,
  에디터 포커스 on/off 양쪽에서 2회 연속.
- 궤도 × 재타격 **end-to-end 3건**(수동 시계 없이 궤도 arm 이 창을 여는 것을 고정).
- 투트랙 리뷰: Track B(ECS) **APPROVE** · Track A(품질) REQUEST CHANGES → 지적 반영 완료.
- 사용자 Play 확인 완료 (2026-08-16) — 도는 그림 · 차폐 · 즉시 발동 · 크기/지속.

## Notes — 되돌리면 안 되는 것

1. **소팅은 sim 좌표로 셀을 역산한다.** view 좌표를 쓰면 `BoardSpace.ToView` 가 z 를 화면
   높이로 접어 행 정렬이 무너진다. `SpineUnitView.UpdateSortingOrder` 의 주석이 같은 경고다.
2. **재타격은 기록 버퍼가 있을 때만 켠다**(`rehits = cooldown > 0 && hasRecords`). 기록이 이
   레짐의 유일한 방어선이라, 없으면 스윕 안 전원을 프레임마다 때린다. 궤도의 관통 예산을
   `int.MaxValue` 로 되돌리는 것도 금지 — 그 조합이 «불멸 + 매 프레임 타격» 을 만든다.
3. **수면 특효의 «피해자별» 은 근접에서만 참이다.** 원거리는 발사 시점 대상 기준으로 탄
   damage 에 구워져 splash/bounce/관통 2차 피해가 배율을 승계한다(`shatter_hymn` 과 같은
   관례). 계약 4 의 ⚠ 절 참조 — 근본 해결은 두 카드를 같이 옮겨야 해서 후속 후보다.
4. **측정 창은 벽시계가 아니라 «대조 더미 피격 횟수»** 다. 초 단위 창은 에디터 부하에 따라
   같은 6초가 6회 공격이 되기도 0회가 되기도 해서 비율이 NaN 으로 터졌다.
5. **퇴근 스위트의 단정은 «운석 한 발만큼 빠졌나»** 이지 «체력이 그대로» 가 아니다. 대조군
   실측 결과 인접 더미가 라이브 웨이브발 주변 피해를 20씩 받는다(호스트인 힐러는 데미지
   출력이 없다). 절대값 단정으로 되돌리면 바로 빨개진다.

## Follow-up

- **시트 push 1회** — 카드 3장 (계약 13, feature 종료 시). **아직 안 했다.**
- **원격 푸시** — `ahead 22`, 사용자 승인 대기.
- 리뷰 잔여(작음): 탄 SO 값의 두 seam(bake 스냅샷 vs 드레인 읽기) · 부착판정(>0)과
  bake판정(>1) 하한 불일치 · `PathHitRecord.Contains` 소비자 0 · `RetireDefender` 의
  Temp 배열 try/finally · 문서 문안 불일치 3건.
- 기능 확장 후보는 README 「후속 후보」 참조 (화염구 다중화 · `BossPeriodicTriggerSystem`
  개명 · 재타격 쿨타임의 다른 소비자 · `OnRetire` × 다른 payload · 실아트 3장).
