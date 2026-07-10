# 5 — PlayMode 검증

## 목적
action-lock(Sleep/Stun) 핵심 계약을 회귀 방지.

## 변경 대상
- 신규 `Assets/_Project/Tests/PlayMode/ActionLockTest.cs`

## 시나리오 (BattleBridge 직접 구동, EffectSpawner.ApplyCc 로 상태 주입)
1. **Sleep 공격+이동 정지**: 공격 가능한 유닛에 Sleep(예:5s) 부여 → 몇 프레임 후 공격 발생 0(쿨다운 소진에도 START 안 함) + 위치 변화 없음(이동 유닛이면).
2. **wake-on-hit**: Sleep 유닛에 IncomingDamage 1회 적용 → Sleep 제거 확인, 이후 공격 재개.
3. **Stun no-wake**: Stun 부여 후 피격 → Stun 유지(공격/이동 계속 정지), 시간 만료로만 해제.
4. **infinite**: `remainingTime=+∞` Sleep → 수 초 경과에도 유지.
5. **양 진영 + aggro-chase(critic MED1)**: 적 유닛에 Sleep → 이동/공격 정지. **`Marching` 뿐 아니라 `AiState.Chasing`(aggro) 적도** 정지 확인(Marching-only 우연통과 방지). 넉백(impulse)은 잠 중에도 적용됨을 확인.
6. **Sleep+Stun 공존 후 피격(critic 누락)**: 한 유닛에 Sleep+Stun 동시 → 1회 피격 → **Sleep 만 제거·Stun-lock 유지**(계약 3 핵심, kind별 merge 검증).
7. **잠 중 쿨다운 틱(critic 누락)**: 잠자는 동안 cooldown 계속 감소 → wake 시 즉시 공격(구 warmup 대체 동작) 확인.
8. **placement-aura 통합**: (PlacementAuraTest 갱신본으로 커버) 신규 배치 유닛 Sleep→깨어남 후 공속.

## 완료 기준
- [ ] PlayMode 시나리오 그린. 콘솔 에러 0.
- [ ] 기존 Dreamcatcher/Dreamstone/PlacementAura PlayMode 회귀 없음.
- [ ] EditMode CcActionLockTests 그린.
