# 0 · 방어유닛 AI 로직 — `Wassup.UnitAi`

## 목적
오늘 `AttackSystem`·브리지에 흩어진 방어유닛 판정을 순수 함수로 옮긴다. **동작 무변** — 진리표가 곧 오늘의 규칙.

## 변경 대상
- 신규 `Assets/_Project/Scripts/UnitAi/DefenderAi.cs` — `DefenderAiState`·`DefenderAttackPolicy`·`DefenderAiInput`·`DefenderAi`
- 신규 `Tests/EditMode/DefenderAiTests.cs`

## 구현
```csharp
public enum DefenderAiState : byte { Ready = 0, Sustaining = 1, Engaging = 2, Locked = 3, Deploying = 4 }  // 큰 값이 우선
public enum DefenderAttackPolicy : byte { Target = 0, Bomb = 1, Summon = 2 }
public struct DefenderAiInput { bool deploying, actionLocked, swinging; DefenderAttackPolicy policy; bool summonAlive; }
public static class DefenderAi
{
    // Deploying > Locked > Engaging(스윙 중) > Sustaining(Summon 정책 + 소환물 생존) > Ready. 중간 두 랭크는 공통 술어층(UnitActionPhase).
    public static DefenderAiState Resolve(in DefenderAiInput i);
    // 오늘의 START 게이트 그대로: Ready 에서 쿨 준비. Summon 정책은 Sustaining 에서도 «시도»한다 — 소환물이 살아 있으면
    // AttackSystem 이 스폰을 건너뛰고 쿨을 리셋만 하는데(재소환 대기 = 남은 쿨), 그 리셋이 없으면 사망 즉시 재소환이 되어 동작이 바뀐다.
    public static bool CanStartAttack(DefenderAiState state, bool cooldownReady)
        => cooldownReady && (state == Ready || state == Sustaining);
}
```

## 완료 기준
- EditMode 진리표: 우선순위 5랭크 전 조합 · Summon 정책 + summonAlive → Sustaining, Target/Bomb 정책은 summonAlive 무시 · `CanStartAttack` 경계(Engaging/Locked/Deploying 은 false, Sustaining 은 true).
- 컴파일 0. 소비자 없음(동작 무변).
