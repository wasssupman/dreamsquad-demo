# 0 — CcKind.Sleep + action-lock 순수 판정

## 목적
Sleep CC 종류 추가 + "행동 불가" 여부를 판정하는 순수 함수(Sleep‖Stun) 정의.

## 변경 대상
- `Assets/_Project/Scripts/Battle/Effects/CcEffect.cs` — `CcKind` 에 `Sleep` append
- 신규 `Assets/_Project/Scripts/Battle/Effects/CcActionLock.cs` — 순수 static 헬퍼
- 신규 `Assets/_Project/Tests/EditMode/CcActionLockTests.cs`

## 구현
```csharp
public enum CcKind : byte { Slow = 0, Impulse = 1, DoT = 2, Stun = 3, Sleep = 4 } // append
```
```csharp
// 행동불가(공격+이동 정지) 판정. Sleep/Stun 공용. Burst 호환 순수 함수.
public static class CcActionLock
{
    public static bool IsLock(CcKind k) => k == CcKind.Stun || k == CcKind.Sleep;
    public static bool IsLocked(in DynamicBuffer<CcEffect> buf)
    {
        for (int i = 0; i < buf.Length; i++) if (IsLock(buf[i].kind)) return true;
        return false;
    }
}
```

## 완료 기준
- [ ] 컴파일 클린. `CcKind.Sleep`=4 grep 확인.
- [ ] EditMode: `IsLock(Sleep)`·`IsLock(Stun)` true, `Slow/Impulse/DoT` false.
- [ ] 기존 CcKind int 값 보존(append-only).
