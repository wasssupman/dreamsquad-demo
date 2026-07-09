# 0 — 워밍업 스쿼드 카드

## 변경 대상

- 수정: `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs` — `CardCategory.Subconscious` + `float placementWarmupSec`
- 수정: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `_activeWarmups` 레지스트리 + 적용 + BeginPlacement clear
- 신규 에셋: `Card_SlowAwakening.asset`

## 구현

`DreamcatcherCard.cs`:
```csharp
public enum CardCategory { Normal, Unique, Subconscious } // append
// 필드 (type 뒤):
public float placementWarmupSec; // 스쿼드 카드: 배치 시 이 초만큼 idle(cooldown). 기본 0.
```

`BattleBridge.cs`:
- `private readonly List<(Wassup.Data.CardTargetAxis axis, float sec)> _activeWarmups = new();`
- `BeginPlacement`: `_activeWarmups.Clear();` (기존 `_activeDcEffects.Clear()` 옆).
- `ApplyDreamcatcherCard(card)` 끝에:
  ```csharp
  if (card.placementWarmupSec > 0f)
  {
      _activeWarmups.Add((card.axis, card.placementWarmupSec));
      foreach (매칭 유닛) ApplyPlacementWarmup(entity, card.placementWarmupSec);
  }
  ```
- `ApplyActiveDcEffectsTo(entity, data)` 끝에: `_activeWarmups` 순회, 축 매칭 시 `ApplyPlacementWarmup`.
- 헬퍼:
  ```csharp
  private void ApplyPlacementWarmup(Entity e, float sec)
  {
      if (!_em.Exists(e) || !_em.HasComponent<AttackState>(e)) return;
      var a = _em.GetComponentData<AttackState>(e);
      a.cooldownRemaining = math.max(a.cooldownRemaining, sec);
      _em.SetComponentData(e, a);
  }
  ```

카드 에셋 `Card_SlowAwakening`: id=slow_awakening, "느린 각성", category=Subconscious, type=Squad, binding=Axis, axis=All, effects=[{AttackSpeed, 50}], placementWarmupSec=2.

## 완료 기준

- [x] 컴파일 + 무회귀 (placementWarmupSec 기본 0 = 기존 스쿼드 카드 무영향)
- [x] Play: 카드 선택 후 배치한 유닛 cooldownRemaining=2.00(2초 idle) → 2초 후 0, **attackSpeedMul=1.50**(+50%) 확인. 콘솔 에러 0.
- [ ] 사용자 육안 확인 (배치 유닛 2초 멈췄다 빨라지는 것)

완료 확인: 2026-07-09 — Play 실증(cooldown 2s→0, attackSpeedMul 1.5), 무예외. 이 문서와 동일 커밋.
