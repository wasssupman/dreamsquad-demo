# 2 — 무의식 등급 프레임

## 목적

덱빌더 카드 그리드 + 상세 팝업에서 무의식(Subconscious) 등급이 고유 프레임색을 갖는다.
category 우선 > 타입색.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckBuilderView.cs`
  - 색 상수 추가: `SubconsciousFrame`, `ArtFallbackSubconscious`
  - `CreateCardView`(프레임 Image + 아트폴백) / `ShowCardPopup`(팝업 아트폴백)

## 구현

색 상수(무의식 = 보랏빛 dream 톤, 기존 팔레트와 구분):
```csharp
private static readonly Color SubconsciousFrame       = new Color(0.34f, 0.18f, 0.48f, 1f);
private static readonly Color ArtFallbackSubconscious = new Color(0.46f, 0.28f, 0.62f, 1f);
```

프레임/폴백색 결정을 static 헬퍼로(중복 제거, CreateCardView·ShowCardPopup 공용):
```csharp
private static bool IsSubconscious(DreamcatcherCard c) => c != null && c.category == CardCategory.Subconscious;
private static Color FrameColorOf(DreamcatcherCard c)
    => IsSubconscious(c) ? SubconsciousFrame : (c != null && c.type == CardType.Unit ? UniqueFrame : NormalFrame);
private static Color ArtFallbackOf(DreamcatcherCard c)
    => IsSubconscious(c) ? ArtFallbackSubconscious : (c != null && c.type == CardType.Unit ? ArtFallbackUnique : ArtFallbackNormal);
```

- `CreateCardView`: `go.GetComponent<Image>().color = FrameColorOf(card);` 및 아트 미지정 시
  `artImg.color = ArtFallbackOf(card);` 로 교체(기존 `unitType ? ...` 대체).
- `ShowCardPopup`: `_popupArtFallback.color = ArtFallbackOf(card);` 로 교체.
- category 는 프레임 채색으로만 재활성 — 덱 규칙/타입 라벨은 그대로.

## 완료 기준

- [ ] 컴파일 클린.
- [ ] 덱빌더에서 느린 각성 카드가 보랏빛 프레임(타입 금/청과 구분).
- [ ] 다른 카드(Squad 청 / Unit 금)는 변화 없음.
- [ ] 팝업 아트폴백(아트 없는 카드)도 무의식이면 보랏빛.
