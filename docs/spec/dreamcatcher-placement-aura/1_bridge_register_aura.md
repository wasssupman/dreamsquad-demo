# 1 — Bridge: future-only 오라 등록 + Unit 경로 분기

## 목적

host-bound future-only 오라를 레지스트리에 등록(현재 유닛 미적용)하고, `ApplyDreamcatcherCardToUnit`
가 PlacementAura payload 를 처리해 **회수 핸들**을 반환하게 한다.

## 변경 대상
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`

## 구현

### (a) future-only 등록 메서드 (신규 — H1: 기존 current-unit 루프 재사용 금지)
```csharp
// dreamcatcher-placement-aura — host-bound future-only aura. _defenderByTile 루프 없음
// → 현재 유닛/host 미적용, ApplyActiveDcEffectsTo(신규 배치)에서만 상속. revocable handle 반환.
private int RegisterPlacementAura(Wassup.Data.CardTargetAxis axis, float asPercent, float warmupSec)
{
    int handle = _dcHandleCounter++;
    if (asPercent > 0f)
    {
        ushort sid = _dcStackCounter++;
        _activeDcEffects.Add(new ActiveDcEffect {
            axis = axis, stat = Wassup.Battle.Effects.StatKind.AttackSpeedMul,
            mult = 1f + asPercent / 100f, stackId = sid, handle = handle });
    }
    if (warmupSec > 0f) _activeWarmups.Add((handle, axis, warmupSec));
    return handle;
}
```

### (b) `ApplyDreamcatcherCardToUnit` 반환 bool→int (H2 규약)
- 규약: **<0 = 실패(무차감)**, **0 = 성공·회수불필요**(엔티티 부착형: 슬롯이 엔티티와 함께 소멸),
  **>0 = 성공·회수핸들**(host 사망 시 revoke).
- 메커니즘 루프에 PlacementAura 분기 추가(트리거 가드 이전, 즉발류):
```csharp
if (m.payload.kind == Wassup.Data.DcPayloadKind.PlacementAura)
{
    if (m.payload.magnitude <= 0f) { Debug.LogWarning($"...PlacementAura non-positive magnitude — skipped."); continue; }
    auraHandle = RegisterPlacementAura(card.axis, m.payload.magnitude, m.payload.duration);
    attached++;
    continue;
}
```
- 메서드 상단 `int auraHandle = 0;` 추가. 말미 반환: `if (attached == 0) return -1; return auraHandle;`
  (기존 `return attached > 0`(bool) 대체). 실패 가드들도 `return false`→`return -1`.
- 다중 PlacementAura = last-wins(auraHandle 덮어씀) + 현재 카드는 1개(느린 각성). 경고 불요.

## 완료 기준
- [ ] 컴파일 클린. **grep 으로 BattleBridge 에 `RegisterPlacementAura` + `PlacementAura` 분기 실존 확인**(H4 재발 방지).
- [ ] `ApplyDreamcatcherCardToUnit` 유일 호출자(HandController:209)만 시그니처 영향 — unit 2 에서 대응.
- [ ] 등록만으로 현재 유닛/host 에 즉시 적용되지 않음(루프 없음).
