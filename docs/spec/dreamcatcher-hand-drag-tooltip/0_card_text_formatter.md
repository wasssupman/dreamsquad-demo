# 0 — 카드 성능 텍스트 공용 포맷터 추출

## 목적

덱빌더 팝업의 카드 본문 조립 로직(`DreamcatcherDeckBuilderView.PopupBody`, L488~523)을
순수 static 함수로 추출해 인게임 툴팁(unit 1)과 공유한다. 호출처가 2개가 되므로
추출이 정당하며(제약 8), plain 입력 → string 출력이라 EditMode 테스트 대상(제약 10).

## 변경 대상

- 신설: `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardText.cs`
- 수정: `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckBuilderView.cs` — `PopupBody` 본문을 추출본 호출로 대체
- 신설: `Assets/_Project/Tests/EditMode/DreamcatcherCardTextTests.cs`

## 구현

- `public static class DreamcatcherCardText` (namespace `Wassup.UI`), 메서드
  `public static string Body(DreamcatcherCard card)`.
- 기존 `PopupBody` 로직을 그대로 이동: axis 헤더(Squad 전용) + 타입 라벨 +
  effects[] 라인(`Attack  <color=#8BE28B>+X%</color>` 등, 음수는 `#E28B8B`) +
  authored `description` 블록(`#D4DAE8`).
- **`CardBuffKind` 매핑은 exhaustive switch 로**: 기존 삼항 체인은 `DamageVsCc` 를
  누락해 "Cost Rate" 로 오표기한다(리뷰 적발, L499~503 fall-through). 전 kind 명시 매핑
  + `DamageVsCc` → `Damage vs CC`. 이 한 건만 기존 출력과 달라지는 **의도된 버그 픽스**다.
- **Active 카드 지원 추가**: 기존 typeLabel 은 Unit/Squad 이분법이라 Active 입력 시
  SQUAD 로 폴백한다. 덱빌더는 Active 를 노출하지 않으므로(카탈로그 제외, per-match
  전용) 현재는 잠재 결함이지만, 인게임 손패는 Active 를 포함하므로 공용화 전에
  `CardType.Active` → 전용 라벨(`ACTIVE`, 색은 기존 팔레트 톤에 맞춰 구현 재량) 분기를
  추가한다. 본문은 authored `description` 만 사용 — `SkillData.cooldownSec` 은 레거시
  `SkillRuntime` 경로 전용이라 각성 손패에서 소비되지 않으므로 자동 수치 표기는
  오정보(2026-07-14 확인). 수치 전달은 description 책임(card-description spec 계약).
- `DreamcatcherDeckBuilderView.PopupBody` 는 삭제하고 호출부(L334)가
  `DreamcatcherCardText.Body(card)` 를 직접 호출.
- ScriptableObject 인스턴스는 테스트에서 `ScriptableObject.CreateInstance<DreamcatcherCard>()`
  로 생성해 필드 주입.

## 완료 기준

- compile 클린 (`refresh_unity` 후 console 에러 0).
- EditMode 테스트 통과 (SO 는 `CreateInstance` 생성, TearDown 에서 `DestroyImmediate` 정리):
  - Squad 카드(axis + effects 2개, 양/음 혼합) 출력이 기존 `PopupBody` 포맷과 동일 문자열
    (기대값을 리터럴로 고정 — 이것이 덱빌더 회귀 가드).
  - Unit 카드(effects 없음 + description) — 타입 라벨 + description 만.
  - Active 카드 — `ACTIVE` 라벨 포함, SQUAD 미포함.
  - `DamageVsCc` effect — `Damage vs CC` 표기, `Cost Rate` 미포함.
  - description 비어있으면 description 블록 미출력.
- 덱빌더 회귀는 문자열 동일성 테스트로 갈음(포맷터는 순수 함수, 호출처 단일·표시 경로 불변).
  `DamageVsCc` 만 의도된 표기 변경.

- 확인 2026-07-14 (사용자 통과 확인) — 커밋 82c770ba
