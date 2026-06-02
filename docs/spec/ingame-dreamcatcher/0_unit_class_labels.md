# 0 — 유닛 class 라벨

## 목적

유닛에 타겟팅 축이 될 `role`(class) 라벨을 부여한다. 드림캐쳐 ranger/guardian 축 + 향후 스쿼드 특성의 토대.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Data/DefenderClass.cs`
- 수정 `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — `role` 필드
- 15개 `Defender_*.asset` — role 백필 (UnityMCP execute_code)

## 구현

```csharp
namespace Wassup.Data
{
    public enum DefenderClass { None, Ranger, Guardian, Bruiser, Caster, Support }
}
```

`DefenderUnitData` 에 `id` 인근:
```csharp
public DefenderClass role = DefenderClass.None;
```

백필 배정 (id → role):
- **Ranger**: scout, ranger, archer, marksman, sniper, piercer, cannon
- **Guardian**: guardian, bastion
- **Bruiser**: bruiser
- **Caster**: fire_caster, ice_caster, poison_caster, blocking_caster
- **Support**: healer

execute_code: 각 DefenderUnitData 를 위 맵으로 role 설정 + SetDirty + SaveAssets. (id 기준 매칭; 누락 id 는 경고.)

## 완료 기준

- compile + read_console clean.
- 15유닛 모두 role 비-None, 위 배정과 일치 (런타임 점검: ById("ranger").role==Ranger 등).
- 기존 드래프트/전투/스쿼드 회귀 없음(추가 필드).
