# 2 — defender 에 CcEffect 버퍼 부여

## 목적
defender 도 CC(Sleep/Stun 등)를 받을 수 있도록 스폰 시 `CcEffect` 버퍼를 미리 부착.
(적은 이미 BattleBridge:4226 에서 부여받음.)

## 변경 대상
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — defender 스폰(bake) 경로

## 구현
defender 엔티티 생성부(IncomingDamage 등 버퍼를 붙이는 지점)에 추가:
```csharp
if (!_em.HasBuffer<CcEffect>(entity)) _em.AddBuffer<Wassup.Battle.Effects.CcEffect>(entity);
```
- 적 스폰과 동일 패턴(hot-path 구조변경 회피 위해 사전 부착).
- `EffectSpawner.ApplyCc`/`CcApplySystem` 가 `GetBuffer<CcEffect>` 전제 → defender 도 안전.
- **순서 의존(MED4)**: `CcEffect` 버퍼는 **`ApplyActiveDcEffectsTo`(BattleBridge.cs:3641) 이전**에 부착해야 한다
  (IncomingDamage 붙이는 상단 ~3499). unit 4 의 placement Sleep 적용(`EffectSpawner.ApplyCc`→`GetBuffer`)이 이걸
  전제로 함 → 3641 이후에 붙이면 배치 시 throw. **unit 4 는 unit 2 에 의존.**

## 완료 기준
- [ ] 컴파일 클린.
- [ ] 배치된 defender 가 `CcEffect` 버퍼 보유(테스트에서 ApplyCc 성공).
- [ ] 기존 defender 동작 회귀 없음(빈 버퍼 = inert).
