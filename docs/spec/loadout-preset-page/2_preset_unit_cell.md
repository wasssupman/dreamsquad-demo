# 2 — 읽기전용 유닛 셀 (PresetUnitCell)

## 목적

프리셋 아이템 1행에 쓰는, 스쿼드 페이지 셀과 같은 스타일의 **읽기전용** 유닛 셀. 탭/아웃라인/라벨/
편성 뱃지 없이 포트레이트 + 희귀도 프레임만 표시.

## 변경 대상

- 신규: `Assets/_Project/Scripts/UI/Outgame/PresetUnitCell.cs` (`Wassup.UI`)

## 구현

`SquadRosterBrowser.AddCell` 의 비주얼(프레임 배경 + inner bg + preserveAspect 포트레이트)을 참고하되,
Button/Badge/SelectionOverlay/Label 을 제거한 최소 셀.

```csharp
public class PresetUnitCell : MonoBehaviour
{
    // 부모 아래 셀 GameObject 를 만들어 컴포넌트를 붙이고 반환하는 팩토리.
    public static PresetUnitCell Create(RectTransform parent, Vector2 size, TMP_FontAsset font = null);

    // 유닛 표시. null 이면 빈 슬롯(포트레이트 숨김 + EmptySlot 색).
    public void Set(DefenderUnitData unit);
}
```

- 프레임 색 = `unit != null ? UnitRarityStyle.Frame(unit.rarity) : <빈 슬롯 톤>`.
- 포트레이트 = `unit.portrait`, `preserveAspect = true`, `raycastTarget = false`, 스프라이트 없으면 `enabled=false`.
- 모든 그래픽 `raycastTarget = false` (읽기전용, 스크롤 드래그 방해 금지).
- 색 톤은 값 복사로 맞춘다(두 원본 모두 `private static readonly` 라 심볼 참조 불가):
  빈 슬롯 톤 = `DreamcatcherDeckStrip.EmptySlot`(0.16,0.18,0.24), 셀 배경(inner bg) 톤 = `SquadRosterBrowser.CellBg`(0.12,0.13,0.17).
  (`SquadRosterBrowser` 는 `EmptySlot` 을 정의하지 않고 null 유닛 셀도 그리지 않으므로 빈 슬롯 선례는 덱 스트립 쪽.)
- 폰트 인자는 라벨이 없으므로 실제로는 불필요할 수 있음 — 시그니처 단순화 위해 생략 가능(구현 재량).

원칙:

- 읽기전용 뷰(계약 6). 상태/이벤트 없음 — `Set` 로 다시 칠하기만.
- 상속 없음(단일 MonoBehaviour). 스쿼드 셀과 코드 공유 대신 **비주얼 스타일**만 맞춘다(사용자 결정:
  프리셋 전용 읽기전용 셀).

## 완료 기준

- [ ] Unity 컴파일 무오류.
- [ ] (unit 3 통합 후) 유닛 있는 셀은 포트레이트+희귀도 프레임, 빈 셀은 회색 슬롯으로 표시.
- 확인 2026-07-20 (커밋 05c7c7b8): Play 렌더 확인 — 유닛 포트레이트 + 희귀도 프레임.
