# 14 — 폴백 아이콘이 카드에 잠기는 문제 (버그 픽스)

## 목적

스톤 모드 상세 패널에서 **드림스톤 아이콘이 텍스트 카드 아래에 깔려** 72% 가 가려진다. 아이콘을 카드 위 자유 영역으로 올린다.

## 원인 (Play 실측)

`DetailPanel`(653×1080) 자식 3개의 배치·렌더 순서:

| 자식 | y 범위 | siblingIndex |
|---|---|---|
| `SpineView` (앵커 `spineFeet 0.57`, pivot bottom) | 616 → 1756 | 0 |
| `PortraitFallback` (앵커 **0.5 = 패널 중앙**, 300×300) | **390 → 690** | 1 |
| `CardRoot` (앵커 `0.03 → cardHeight 0.56`) | **32 → 605** | 2 |

UGUI 는 나중 형제가 위에 그려진다. `CardRoot` 가 `PortraitFallback` 을 **215px / 300px (72%)** 덮는다.

`SpineView` 는 `spineFeet 0.57` 로 카드 상단(0.56) **바로 위에 서서** 이 문제를 피한다 — 즉 "콘텐츠는 카드 위에 선다"는 규칙이 이미 있었고, 폴백 이미지만 그 규칙 밖에 있었다.

**회귀 시점**: `1_unit_detail_view.md:33` 이 기록한 원래 카드 앵커는 `y 0~0.40`(상단 432)이라 겹침이 42px 로 미미했다. 2026-07-18 ui-polish 가 카드를 `0.03~0.56` 으로 키우면서 폴백을 삼켰다. units 11~13 과는 무관한 선행 결함이다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/SquadCharacterPage.cs` — `PortraitFallback` 앵커 y

## 구현

```csharp
float fallbackY = (cardHeight + 1f) * 0.5f;   // 카드 위 자유 영역의 중앙
```

- 리터럴 `0.78` 을 박지 않고 **`cardHeight` 에서 파생**시킨다. 카드를 다시 키워도 폴백이 따라 올라가 같은 방식으로 삼켜지지 않는다 — 이번 회귀가 정확히 "카드만 키우고 폴백은 그대로 둔" 데서 나왔기 때문.
- 크기(300×300)·형제 순서·`ShowStone` 로직은 불변. 좌표 한 줄만 바꾼다.

## 완료 기준

- compile 클린.
- Play(스톤 모드): 아이콘 y `692 → 992`, 카드 상단 `605` → **겹침 0px**, 카드 위 여유 88px. 아이콘 전체가 드러난다.
- 유닛 모드 무영향: 현재 유닛 17종 전부 `SpineSkeletonDataAsset` 을 가져 폴백 경로를 타지 않는다(`BindSpine` 이 Spine 을 쓰고 폴백은 비활성). 스켈레톤 없는 유닛이 생기면 같은 자유 영역에 뜬다.

## 후속 후보 (범위 밖)

- **드림스톤 아이콘 PNG 에 알파가 없다.** `Assets/_Project/Art/Dreamstones/Icons/*.png` 는 PNG **colortype 2 (RGB, 알파 채널 없음)** 이라 검은 배경이 소스에 구워져 있다. 그리드 셀에서는 셀 배경이 어두워 묻히지만, 상세 패널에서는 아이콘 뒤 검은 사각형으로 보인다. 임포터는 `alphaIsTransparency=True / alphaSource=FromInput` 이지만 입력에 알파가 없어 무의미하다. 아트 재출력(투명 배경) 또는 임포터 알파 생성이 필요 — **아트 에셋 이슈라 이 spec 밖**.
