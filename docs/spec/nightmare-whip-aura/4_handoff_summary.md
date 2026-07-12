# 4 — Handoff Summary (완료 2026-07-12)

## Commit

spec `417f9f07` → u0 `b6ce8ab4`(payload 계약+순수함수) → u1 `70057706`(펄스 arm+베이크) → u2 `29085e9b`(authoring+e2e) → u3 `bfa858fb`(원샷 연출, rev 2 로 대체) → u3 rev 2(부착 오라 — 이 커밋). 브랜치 주의: 병행 세션 사정으로 `feat/mobile-ui-safe-area` 위에 커밋됨 — whip 커밋은 전부 whip 파일만 건드려 cherry-pick 분리 가능.

## Implemented

- **채찍질** = `PeriodicTimer(1s)` × `AllyMoveSpeedAura(9)`: 매 펄스 host 기준 Chebyshev 3타일 내 **같은 진영** 유닛(host 제외, entity 비교)에 `MoveSpeedMul ×1.2`(TTL 1.5s) — `StatModifierApplyEventsSingleton` 경유, modifier 계층 코드 무변경.
- 유지 = merge-refresh(duration>period), 해제 = TTL 자연 만료(범위 이탈·보스 사망, revoke 없음).
- arm = `BossPeriodicTriggerSystem` 페이로드 분기(진영 풀 lazy 빌드, degenerate skip: mag==0/dur<=0). 신규 시스템/채널/슬롯 필드 0.
- **오라 연출 (rev 2)** = 메커닉 데이터 선언(`DcPayloadSpec.auraPrefab/auraScale` — WindAura, scale 1.6) → 베이크 등록 → `DcAuraVisualPool`(plain class, 씬 배선 없음)이 host 뷰 추종. **kind-blind** — bridge/범용 인프라에 payload kind 분기 0. rev 1 원샷(`Projectile_WhipPulse`)·StatusFx 편입은 기각(3_whip_pulse_visual.md rev 이력 참조 — "지형 말뚝" 오독 / 메커닉 지식 누수).
- 베이크 = `BakeNightmareMechanics` 오라 등록 + duration<=period 경고 1줄. (rev 1 원샷 arm 코드는 SO 게이트로 잔존, 현재 미사용.)

## Key Files

- `Battle/Combat/AuraPulse.cs`(순수 타겟 선택) · `BossPeriodicTriggerSystem.cs`(whip 분기) · `DcMechanic.cs`(enum 9) · `BattleBridge.cs`(:4269 베이크)
- 에셋: `Projectile_WhipPulse.asset` · `Enemy_Boss_Nightmare.asset`(3번째 mechanic) · `WavePlan_BossTest.asset`(t=7 Basic ×4 동행 — 검증용 편성)
- 테스트: `Tests/EditMode/AuraPulseTests.cs`(6)

## Verified

- 컴파일 클린 · EditMode 701/703 그린(신규 AuraPulse 6, skip 2 무관) · ecs-review 전관점 PASS(M0/L1 반영).
- 스크립트 배틀(SessionState 캐리→`StartBattle()`) + 프레임 모니터: in-range 진입 ≤1s 후 1.00→1.20(다수), out-range(cheb8+) 1.00, 보스 자신 1.00, 이탈 후 TTL 1.5s 유지. 폭격 타이머와 동시 tick(직교).
- 연출: 조건 캡처(미니언 근접 시에만) 8샷 — 보스 위치 골드 펄스+1s 잔상, 콘솔 에러 0. VFX 선정은 오프스크린 렌더 12종 대조(Axe 계열 = 도끼 메쉬 노출 기각).

## Notes (되돌리면 안 되는 의도)

- **duration(1.5) > periodSeconds(1)** 이 점멸 방지 authoring 계약 — 줄일 땐 쌍으로. 베이크 경고 유지.
- **메커닉 연출은 메커닉 데이터가 선언, 드림캐쳐 파이프라인이 구동** (사용자 확정 2026-07-12) — StatusFx/bridge reconcile 에 payload kind 분기 금지. 새 메커닉 오라 = auraPrefab 선언만.
- **rev 1 원샷 arm 은 buffed>0 게이트** — 효과 없는 연출 금지. `projectile=null`(dataIndex -1) = 무연출 authoring.
- 해제는 revoke 가 아니라 **TTL 만료** — 이탈 즉시 해제로 "개선"하려면 spec 재설계 필요(기각된 접근 B).
- `WavePlan_BossTest` 의 t=7 그룹은 whip 관찰용 — 제거하면 e2e 재현 어려움.

## Follow-up

- 전용 채찍 스윙/버프 링 연출 고도화 · defender-side 오라 카드(데이터만으로 성립) · 버프 아이콘(unit-status-fx 계열) · 수치 실측 튜닝(펄스/TTL/±%/반경 — 전부 SO).
- nightmare-catcher 잔여 후속(기본공격 원거리화·어그로 면역 등)은 그쪽 README 참조.
