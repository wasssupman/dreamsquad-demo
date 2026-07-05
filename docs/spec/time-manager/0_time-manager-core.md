# 0 — TimeManager 코어 (Mono)

## 목적

도메인별 시간 스케일을 소유·중재하는 순수 MonoBehaviour 싱글턴. ECS 의존 없음. 다른 모든 작업 단위가 이 API 를 소비한다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Core/Time/TimeManager.cs`
- 신규 `Assets/_Project/Scripts/Core/Time/TimeDomain.cs`

## 구현

```csharp
public enum TimeDomain { Battle, Interaction }   // 확장 = 멤버 추가
```

`TimeManager` (의도된 예외적 싱글턴):

- `static TimeManager Instance` — 씬 부트스트랩 또는 `RuntimeInitializeOnLoadMethod` 로 보장. GameManager 와 동일한 생성 관례를 따른다(구현 시 GameManager 패턴 확인해 맞춤).
- 내부 상태: 도메인별 요청 리스트. 각 요청 = `(int id, int generation, float scale, int priority)`.
- `TimeLease Request(TimeDomain domain, float scale, int priority = 0)`
  - 새 요청 push, 유효 스케일 재계산, 바뀌었으면 `ScaleChanged(domain, newScale)` 발화. `TimeLease` 반환.
- `float ScaleOf(TimeDomain domain)` — 활성 요청 중 (priority desc, 동률 scale asc) 승자. 없으면 `1f`.
- `float DeltaTime(TimeDomain domain)` — `Time.unscaledDeltaTime * ScaleOf(domain)`.
- `event Action<TimeDomain, float> ScaleChanged`.
- 내부 `Release(int id, int generation)` — id+generation 일치할 때만 제거(멱등). 제거로 스케일 바뀌면 ScaleChanged 발화.

`TimeLease` (멱등):

```csharp
public readonly struct TimeLease
{
    // id + generation + TimeManager 참조 보유
    public void Dispose();   // Release(id, generation) 위임. 이미 해제/복사본이면 no-op
}
```

- `readonly struct` 유지하되 **generation 토큰**으로 이중 dispose·복사본 오release 방지. Release 시 해당 슬롯 generation++.

## 완료 기준

- [ ] 컴파일 통과 (ECS 참조 0, UnityEngine 만 의존).
- [ ] EditMode 단위 테스트 `Tests/EditMode/TimeManagerTests.cs`:
  - 요청 없음 → `ScaleOf == 1`.
  - Request(Battle,0.2) → 0.2. 그 위 Request(Battle,0,pri100) → 0. 후자 Dispose → 0.2 복귀. 전자 Dispose → 1.
  - 동일 우선순위 두 요청 → 더 낮은 scale 이 이김.
  - Lease 이중 Dispose → 두 번째 no-op(다른 활성 요청에 영향 없음).
  - 복사된 lease 로 Dispose 후 원본 Dispose → 한 번만 해제.
- [ ] `ScaleChanged` 가 유효 스케일이 **실제로 바뀔 때만** 발화.
