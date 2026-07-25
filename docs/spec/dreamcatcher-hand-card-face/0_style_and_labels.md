# 0 — 스타일·라벨 단일 소스 + 본문 전용 포맷터

## 목적

손패 카드 면이 쓸 타입 색·무의식 테두리·대상 태그 라벨과, 헤더 줄 없는 본문 텍스트를
**순수 함수**로 확정한다. UI 배선 없이 컴파일 + EditMode 테스트만으로 완결.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/CardCategoryStyle.cs` — 손패용 함수 추가
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardText.cs` — 본문 전용 변형 추가,
  `AxisLabel` 접근 개방
- `Assets/_Project/Tests/EditMode/HandCardStyleTests.cs` — 신규

## 구현

`CardCategoryStyle` 에 추가 (기존 `Frame`/`ArtFallback`/`Label` 관례 그대로 static + 코드 상수):

```csharp
// 헤더 밴드 색: 타입 3색. Squad=블루(기존 Normal 계열 명도 업), Unit=골드(기존 Unique 계열),
// Active=청록 신규(보라는 무의식 테두리·각성 게이지가 점유라 회피).
public static Color HandHeader(CardType type);
// 본문 패널 색: 어두운 중립 단일색 (기존 손패 frame 색 0.10,0.08,0.18 계열).
public static Color HandBody();
// 카드 외곽 테두리: 무의식이면 SubconsciousFrame 보라, 아니면 어두운 중립.
public static Color HandBorder(DreamcatcherCard c);
// 대상 태그(역할 병기 — rev 1: 타입 색은 무명 코드라 칩이 대상+역할을 글자로 말한다):
// Squad=축 라벨+" 버프"(전체 버프/레인저 버프/가디언 버프/1코스트 버프),
// Unit=BountyMark 보유 시 "적 지정"(판별 = DreamcatcherCard.HasBountyMark — 조준 라우팅과
// 단일 소스) / 그 외 "아군 부착", Active=skill.effect 파생(타일 지정/타일 2개/아군 지정),
// skill null·미지원=폴백 "필드".
public static string TargetTag(DreamcatcherCard c);
```

`DreamcatcherCardText` 변경:

- `internal static string AxisLabel(CardTargetAxis)` 로 개방(현 private) — `TargetTag` 가 위임.
  축 라벨 이중 정의 금지.
- `public static string BodyLinesOnly(DreamcatcherCard card)` 추가: `Assemble` 의 라인 빌드
  (`BuildSummaryLines` + description 폴백)만 수행하고 **헤더 줄(축·타입) 없이** `"\n"` join 반환.
  타입/대상은 이제 카드 면의 색·태그가 담당하므로 본문에서 중복 제거.

Active 태그 매핑 (`SkillEffectType` 기준):

| effect | 태그 |
|---|---|
| Meteor / SlowField / Tornado | 타일 지정 |
| Portal | 타일 2개 |
| PowerSurge / RapidFire | 아군 지정 |
| 그 외 / skill null | 필드 |

## 완료 기준

- compile 클린 (`dotnet build` 검증 가능 — Unity 열려 있으면 콘솔 에러 0).
- EditMode `HandCardStyleTests` 통과:
  - 타입 3종 → `HandHeader` 색이 서로 다름 (상수 값 자체는 검증하지 않음 — 선택 로직만).
  - 무의식 카드 → `HandBorder` == `Frame`(Subconscious) 보라 / 일반 카드 → 중립.
  - `TargetTag`: Squad 축 4종(`… 버프` 접미 포함), Unit, Active 스킬 6종 + skill null 폴백.
  - `BodyLinesOnly`: 효과 라인만 반환(축·타입 헤더 문자열 미포함), effects 없는 Unit 카드는
    description 폴백, description 도 비면 빈 문자열.
- 기존 EditMode 스위트 무회귀.

확인 2026-07-25 — EditMode 전체 1297 통과/0 실패/2 스킵(신규 17케이스 포함), 커밋 `30c10e3f`.
