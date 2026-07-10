# 4 — 브릿지 warmup → Sleep 적용 교체

## 목적
warmup(쿨다운 직접쓰기)을 Sleep CcEffect 적용으로 교체. placement-aura 의 "배치 후 N초 대기"가
1급 상태로 표현되고, 층위 비대칭(직접 AttackState 쓰기)이 사라진다.

## 변경 대상
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `ApplyPlacementWarmup`
- `Assets/_Project/Tests/PlayMode/PlacementAuraTest.cs` — warmup assert 갱신

## 구현
`ApplyPlacementWarmup(Entity e, float sec)` 교체:
```csharp
// 기존: AttackState.cooldownRemaining = max(cur, sec)  ← 제거
// 신규: Sleep CcEffect 적용(sec 초). defender 는 unit 2 로 CcEffect 버퍼 보유.
if (sec <= 0f) return;
Wassup.Battle.Effects.EffectSpawner.ApplyCc(_em, e, new Wassup.Battle.Effects.CcEffect
{
    kind = Wassup.Battle.Effects.CcKind.Sleep,
    remainingTime = sec,   // 무한 필요 시 float.PositiveInfinity
});
```
- 호출부(RegisterPlacementAura 경유 `_activeWarmups`, ApplyActiveDcEffectsTo)는 그대로 — 적용 방식만 Sleep.
- `AttackState` 직접 쓰기 제거 → warmup 관련 cooldown 조작 코드 정리.

## 테스트 갱신 (PlacementAuraTest)
- 기존 `GetCooldown(...) >= 1.9f` (warmup=cooldown) 제거.
- 대체: 신규 배치 유닛이 **Sleep CcEffect 보유** + **공격 안 함** 확인(공속 버프 1.5 는 유지 검증).
  baseline 비교(LOW1)는 "Sleep 보유 여부"로 대체.

## 완료 기준
- [ ] 컴파일 클린. `ApplyPlacementWarmup` 이 cooldownRemaining 미접근.
- [ ] PlacementAuraTest 갱신본 그린(Sleep 적용·공속 버프·회수 원복).
