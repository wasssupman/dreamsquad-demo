# 1 — 카드 면 재구성 (투톤 face 스프라이트 + 레이아웃 교체)

## 목적

손패 카드 면을 아트 → "타입색 헤더(이름+태그+코스트) / 어두운 본문(설명)" 구조로 교체한다.
크럼플 딜인·드래그·커밋·포커스·툴팁은 무회귀.

## 변경 대상

- `Assets/_Project/Scripts/UI/UiRoundedSprite.cs` — `MakeCardFace` 추가
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` —
  `EnsureSlots` / `BindCard` / `BindEmpty` / `RestoreSlotHome` / `StartDeal`(텍스트 페이드 목록)

## 구현

### face 스프라이트 (UiRoundedSprite.MakeCardFace)

```csharp
// 풀렉트(비 9-slice) 투톤 카드 면: 라운드 코너 + border 테두리 + 상단 headerFrac 만큼 headerFill,
// 나머지 bodyFill. UiCardFaceMesh 가 outer UV 를 풀 스트레치하므로 카드 비율(172×200)로 굽는다.
public static Sprite MakeCardFace(int width, int height, float radius, float border,
    Color headerFill, Color bodyFill, Color borderColor, float headerFrac);
```

- 색 조합은 타입 3 × 테두리 2(중립/무의식 보라) = 최대 6종 — `DreamcatcherHandView` 가
  `(type, subconscious)` 키로 캐시해 재생성 방지.
- 헤더/본문 경계는 하드 엣지(밴드 구분이 목적) + 1px AA 허용.

### 슬롯 구조 변경 (EnsureSlots)

- `Art`(UiCardFaceMesh) → 이름을 `Face` 로, 인셋 6px → **0**(면 전체가 카드). `preserveAspect` 유지.
  root `frame` Image 는 바인딩 시 투명 — 빈 슬롯 표시로만 남는다.
- `nameTag` 하단 밴드 → **헤더 이름**으로 이동: 헤더 밴드 영역(상단 ~64px) 중앙, 배경 Image 제거
  (face 헤더색이 배경), 좌우 인셋 ~46px(코스트 배지·태그 칩 회피), 오토사이즈 14~20. `nameGroup`
  페이드 패턴 유지.
- **태그 칩 신규**: 우상단, `UiRoundedSprite.Make` 라운드 칩(헤더색보다 어두운 틴트 + 흰 텍스트
  ~13pt, `CardCategoryStyle.TargetTag`). `CanvasGroup` 페이드 동참.
- **본문 TMP 신규**: 헤더 아래~하단 패딩 10px, 좌상 정렬, `labelFont`(Jua), 오토사이즈 13~17,
  `DreamcatcherCardText.BodyLinesOnly`. `CanvasGroup` 페이드 동참.
- 코스트 배지 무변경.
- `cardOverlap` 기본값 54 → **16** (5장×172=860 < 패널 980 — 겹침은 감성 목적만 남긴다).
  ⚠ SerializeField 라 코드 기본값 변경은 **씬에 이미 직렬화된 54를 못 이긴다** —
  BattleScene 의 `DreamcatcherHandView` 컴포넌트 값도 16 으로 갱신(UnityMCP)하고 같은 커밋에 포함.

### 바인딩 (BindCard / BindEmpty / RestoreSlotHome / StartDeal)

- `BindCard`: `face.sprite = 캐시된 MakeCardFace(...)`, `face.color = Color.white`.
  `card.art`/`skill.uiTint` 참조 제거. 이름·태그·본문 텍스트 기입.
- `BindEmpty`: face 비활성 + root frame 만 기존 빈 슬롯 표시.
- `RestoreSlotHome`/`StartDeal`: 신규 태그·본문 `CanvasGroup` 을 기존 `nameGroup`/`costGroup`
  페이드 목록에 추가(펴짐 완료 후 페이드-인, 복원 시 alpha 1).

## 완료 기준

- compile 클린.
- 에디터 Play: 손패 오픈 시 3타입 카드가 각각 블루/골드/청록 헤더 + 태그 + 이름 + 본문으로 표시,
  무의식 카드는 보라 테두리. 크럼플 딜인·press-lift·드래그·커밋·툴팁 정상.
- 기존 PlayMode 손패 테스트 무회귀.
