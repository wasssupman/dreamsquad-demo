# 16 — 스탯 2열 컬럼 폭 고정 (수치 우측 정렬)

## 목적

2열 스탯에서 **좌측 컬럼 수치의 우측 끝이 행마다 어긋난다**. 컬럼 폭을 내용과 무관하게 고정해 수치를 한 줄에 세운다.

## 원인 (Play 실측, cardRoot 좌측 기준 x)

| 행 | 좌측 셀 폭 | 좌측 값 우측 끝 |
|---|---|---|
| 데미지 15 | 278 | **300** |
| 사거리 1 | 249 | **271** |
| 각성보상 4 | 409 | **431** |

우측 컬럼은 전부 631(카드 우측 끝)로 이미 맞아 있었다 — 문제는 좌측 컬럼뿐.

`StatCell` 은 `HorizontalLayoutGroup` 을 갖는데, **`LayoutGroup` 은 `ILayoutElement` 이기도 하다.** 그래서 자기 `preferredWidth` 를 **자식 텍스트 폭의 합**으로 보고한다. 행 HLG 는 각 칸에 그 content-dependent preferred 를 먼저 주고 남은 폭만 균등 분배하므로, 글자 수(`"데미지"+"15"` vs `"각성보상"+"4"`)에 따라 **컬럼 경계가 행마다 움직인다.**

row2 가 409 로 유독 넓은 건 짝인 `Spacer` 가 `preferredWidth = 0` 이라 남은 폭을 `StatCell` 이 독식하기 때문이다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/SquadUnitDetailView.cs` — `MakeStatCell()`, `MakeSpacer()`, `MakeHalfWidth()` 신설

## 구현

```csharp
private static void MakeHalfWidth(GameObject go)
{
    var le = go.AddComponent<LayoutElement>();
    le.preferredWidth = 0f;   // HLG 의 content-dependent preferred 를 덮는다
    le.flexibleWidth = 1f;    // 남은 폭을 균등 분배
}
```

- `MakeStatCell` 과 `MakeSpacer` **양쪽** 에 적용한다. 스페이서를 빼면 홀수 행(각성보상)에서 짝 셀이 남은 폭을 다 먹어 다시 어긋난다.
- `preferredWidth = 0` 이 핵심 — `LayoutElement` 를 붙이기만 하고 preferred 를 안 덮으면 HLG 의 보고값이 그대로 살아 증상이 남는다.
- 텍스트의 `TextAlignmentOptions.Right` 는 이미 있었다. **정렬 속성이 아니라 칸 폭이 문제였다.**

## 완료 기준

- compile 클린.
- Play 실측: 셀 폭 전부 **282**, 좌측 컬럼 값 우측 끝 전부 **304**, 우측 컬럼 전부 **631**.
- 값 자릿수가 다른 유닛(guardian `15/800/1/2.0s/4`, artillery `60/350/3/3.5s/4`) 사이를 오가도 컬럼이 흔들리지 않는다.
- 카드 높이 예산 572/572 불변(폭만 건드림).
