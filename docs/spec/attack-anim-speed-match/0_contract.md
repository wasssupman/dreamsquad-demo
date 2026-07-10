# 0 — 계약: 이벤트에 공격 간격 필드 추가

## 목적

공격 간격(공격속도 필드에서 파생된 sim 값)을 뷰로 전달할 통로를 연다. **별도 튜닝 SO 없음** — 애니 배율은 이 간격에서 직접 도출한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/UnitAttackVisualEvent.cs`

## 구현

```csharp
public struct UnitAttackVisualEvent
{
    public Entity attacker;
    public float3 targetWorld;
    public float attackInterval; // = cooldownDuration / attackSpeedMul. 0 이하 = 폴백(변조 안 함).
}
```

## 불변 법칙 (SoT)

- **유닛 SO 의 공격속도(`cooldownDuration`, "1=1초 1번")가 source of truth.** 애니 재생속도의 논리는 오직 이 수치(+`attackSpeedMul` 버프)에서 파생된 `attackInterval` 로 구성된다.
- 뷰는 이 숫자만 읽는다. 시뮬 rate/데미지는 불변.

## 완료 기준

- compile 성공, `read_console` 에러 0.
- 동작 변화 없음(필드만).
