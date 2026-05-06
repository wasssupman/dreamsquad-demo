# 2 — Card Decoration (2-Layer Visual)

## 목적

`DraftCardFanView.CreateCard()` 에서 등급(Rarity)과 슬롯(DraftSlotType)을 2-layer로 카드에 표시한다.  
테두리 = 등급 색상, 상단 배너 = 슬롯 색상.

## 변경 대상

- `Assets/_Project/Scripts/UI/Draft/DraftCardFanView.cs`

## 구현

### 색상 상수 (static helper)

```csharp
private static Color RarityBorderColor(DefenderRarity r) => r switch
{
    DefenderRarity.Common => new Color(0.55f, 0.55f, 0.55f),
    DefenderRarity.Rare   => new Color(0.35f, 0.61f, 0.97f),
    DefenderRarity.Epic   => new Color(1.00f, 0.55f, 0.26f),
    DefenderRarity.Ego    => new Color(0.80f, 0.27f, 1.00f),
    _                     => Color.white,
};

private static Color SlotBannerColor(DraftSlotType t) => t switch
{
    DraftSlotType.Basic      => new Color(0.29f, 0.48f, 0.78f),
    DraftSlotType.Meta       => new Color(0.79f, 0.64f, 0.15f),
    DraftSlotType.Collection => new Color(0.18f, 0.62f, 0.38f),
    DraftSlotType.Ego        => new Color(0.61f, 0.18f, 0.96f),
    _                        => Color.gray,
};

private static string SlotLabel(DraftSlotType t) => t switch
{
    DraftSlotType.Basic      => "BASIC",
    DraftSlotType.Meta       => "META",
    DraftSlotType.Collection => "COLLECT",
    DraftSlotType.Ego        => "EGO",
    _                        => "",
};
```

### CreateCard() 레이아웃 변경

기존 카드 GO(Image)의 색상을 **등급 테두리 색**으로 설정. 그 위에 내부 배경(inner bg) Image를 추가해 border 효과를 만든다. 배너는 기존 Swatch 영역을 슬롯 색으로 교체.

```csharp
// 1. 카드 외곽 Image → rarity 테두리 색
go.GetComponent<Image>().color = RarityBorderColor(unit.rarity);

// 2. Inner background (테두리 두께 4px)
var innerBg = new GameObject("InnerBg", typeof(RectTransform), typeof(Image));
innerBg.transform.SetParent(go.transform, false);
var innerRt = (RectTransform)innerBg.transform;
innerRt.anchorMin = Vector2.zero;
innerRt.anchorMax = Vector2.one;
innerRt.offsetMin = new Vector2(4f, 4f);
innerRt.offsetMax = new Vector2(-4f, -4f);
innerBg.GetComponent<Image>().color = new Color(0.11f, 0.11f, 0.14f, 1f);
innerBg.GetComponent<Image>().raycastTarget = false;
innerBg.transform.SetSiblingIndex(0);  // 맨 뒤(테두리 뒤에 숨기지 않게 1번 레이어)

// 3. 배너 — 기존 Swatch를 슬롯 배너로 교체
//    SlotType은 DraftSession에서 조회
var slotType = session != null ? session.GetSlotType(unit) : DraftSlotType.Collection;
// Swatch color → slot banner color
swatch.color = SlotBannerColor(slotType);

// 4. 배너 레이블 (기존 Swatch 위에 TMP 추가)
var bannerLabel = new GameObject("BannerLabel", typeof(RectTransform));
bannerLabel.transform.SetParent(swatchGo.transform, false);
var blRt = (RectTransform)bannerLabel.transform;
blRt.anchorMin = Vector2.zero; blRt.anchorMax = Vector2.one;
blRt.offsetMin = Vector2.zero; blRt.offsetMax = Vector2.zero;
var blTmp = bannerLabel.AddComponent<TextMeshProUGUI>();
blTmp.text = SlotLabel(slotType);
blTmp.fontSize = 16;
blTmp.color = slotType == DraftSlotType.Meta ? Color.black : Color.white;
blTmp.alignment = TextAlignmentOptions.Center;
blTmp.fontStyle = FontStyles.Bold;
blTmp.raycastTarget = false;
```

### 시그니처 변경 요약

```csharp
// Build(): session 파라미터 추가
public void Build(IReadOnlyList<DefenderUnitData> pool, DraftSession session)

// CreateCard(): session 파라미터 추가 (Build에서 전달)
private DraftCardView CreateCard(DefenderUnitData unit, int index, DraftSession session)
```

`DraftView.RunFlow()` 의 호출부 수정:
```csharp
fan.Build(controller.Session.Pool, controller.Session);
```

`Build()` 내부에서 `CreateCard(unit, i, session)` 으로 전달.

## 완료 기준

- [ ] 컴파일 오류 없음
- [ ] PlayMode: 카드 10장 표시 시 각 카드 테두리가 등급 색상으로 표시됨
- [ ] PlayMode: 상단 배너가 슬롯 타입 색상 + 레이블로 표시됨
- [ ] Basic 3장 = 파란 배너 "BASIC", Meta 2장 = 골드 배너 "META", Ego 1장 = 보라 배너 "EGO", Collection 4장 = 초록 배너 "COLLECT"
