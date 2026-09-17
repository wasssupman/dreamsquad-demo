# 0 · `IdleBreakCycle` — 순수 구조체 추출 (기본 루프 + 주기 컷)

## 목적

`SpriteUnitView` 안에 흩어진 대기 컷 상태(`_idleCycling`·`_idleResting`·`_idleTimer`·`_idleIndex`)와 전이 규칙을
백엔드 중립 구조체로 빼서 두 뷰가 공유한다. **틀이 바뀐다**(사용자 2026-09-17): 「0프레임 정지 쉼」을 폐기하고 기본 idle 루프가 항상 돌며,
N초마다 그 외 컷을 한 바퀴 끼운다. 스프라이트 동작도 이에 맞게 바뀐다(hidy: idle1 루프 ↔ idle2 한 바퀴).

## 변경 대상

- 신규 `Assets/_Project/Scripts/Presentation/IdleBreakCycle.cs`
- 신규 `Assets/_Project/Tests/EditMode/IdleBreakCycleTests.cs`
- 수정 `Presentation/SpriteUnitView.cs` — 필드 4개 → `IdleBreakCycle _idle` 하나, `TickIdleCycle`/`EnterIdleRest` 가 구조체를 구동

## 구현

```csharp
public struct IdleBreakCycle
{
    public bool Active;      // 순환 중인가(idle 자리 + 컷 저작). false 면 Tick 은 아무것도 안 한다.
    public bool Looping;     // true = 기본 루프 중(다음 컷까지 대기), false = 컷 재생 중
    public float Timer;      // 남은 초(배틀 시간)
    public int LastIndex;    // 직전에 튼 컷 인덱스(-1 = 없음) — 연속 회피용

    public void Stop();                        // Active = false (원샷·walk·오버라이드가 끼어들 때)
    public void BeginLoop(float interval);     // Active = Looping = true, Timer = max(0, interval)
    public void BeginBreak(float duration);    // Looping = false, Timer = duration
    public bool Tick(float dt);                // Active 면 Timer -= dt; 반환 = 이번 틱에 전이해야 하는가(Timer <= 0)
    public int PickBreak(int breakCount, float roll); // UnitAnimationChoice.ChooseNext(breakCount, LastIndex, roll); LastIndex 갱신 — 풀 = 컷만
}
```

뷰의 소비 형태(두 뷰 동일):
```
if (!_idle.Tick(dt)) return;
if (_idle.Looping) { int i = _idle.PickBreak(breakCount, Random.value); float d = DurationOf(i);
                     if (d > 0) { _idle.BeginBreak(d); PlayBreak(i); return; } }
_idle.BeginLoop(interval); PlayBaseLoop();
```
`DurationOf`·`PlayBreak`·`PlayBaseLoop`·`interval` 만 백엔드가 다르다. interval 0 = 컷 끝나자마자 다음 컷(연속).

## 완료 기준

- EditMode `IdleBreakCycleTests`: 루프→컷→루프 전이 · interval 0 은 다음 틱 전이 · `Stop` 뒤 Tick 무동작 · `PickBreak` 연속 회피(컷 ≥ 2) · 음수 interval → 0.
- `SpriteUnitView`: hidy 프로브에서 `idle1 루프 → (N초) → idle2 한 바퀴 → idle1 루프` — 0프레임 정지 없음. `UnitSpriteMotionSet.idleRestGap` → `idleBreakInterval` 개명(FormerlySerializedAs), 툴팁·문서(sprite-unit-backend 6) 갱신.
- EditMode 전체 초록.
