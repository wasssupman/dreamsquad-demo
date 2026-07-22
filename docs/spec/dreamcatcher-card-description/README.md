# dreamcatcher-card-description

> 상태: 구현 완료 2026-07-10 (compile 클린 · EditMode 15/15 · PlayMode 8/8; UI 육안 검증 후속)
>
> **현재 텍스트 계약:** 구조화 수치 formatter는 `docs/spec/dreamcatcher-card-effect-summary/`
> 가 우선한다. 이 spec의 `description`은 구조화 요약을 만들 수 없는 카드의 이행기 fallback이다.

## 목표

드림캐쳐 카드 SO 에 authored `description`(효과/메커니즘 설명 텍스트) 필드를 추가하고,
**아웃게임 덱빌더 상세 팝업**에서 이를 표시한다. 현재 팝업은 `card.effects`(스탯 버프)만
렌더하므로 Unit 카드(콕콕 바늘·가시 갑옷·작별 선물·통통 구슬·마지막 불꽃)와 Active 카드가
효과 텍스트 빈칸으로 뜨는 문제를 해소한다.

## 검증 질문

"덱빌더에서 아무 카드나 눌렀을 때, 스탯이든 메커니즘이든 액티브든 **무슨 카드인지 읽을 수 있는가?**"

## 배경 (조사 결과)

- **활성 표시 표면 2곳**: ① 아웃게임 덱빌더 상세 팝업(`DreamcatcherDeckBuilderView.ShowCardPopup`
  → `PopupBody()`), ② 인게임 각성 손패(`DreamcatcherHandView`, 아트+네임밴드+코스트만).
- **dormant(제외)**: `DreamcatcherSelectionView`(3장 선택 모달) + `DreamcatcherController` —
  awakening-hand rev 4 §11 에서 dormant. 표시 대상 아님.
- **현재 공백**: `PopupBody()`/`Summary()` 는 `mechanics[]`·`attackMods[]`·`skill`(Active) 을
  무시 → 카탈로그 16장 중 Unit 5장이 팝업 본문 빈칸. (Active 6장은 팝업 비노출 = 카탈로그 밖.)

## 결정 (2026-07-10 사용자 확정)

1. **필드 구성**: `description` 단일 (`[TextArea] string`). `SkillData.description` 관례와 동일.
2. **표시 스코프**: 이번 spec = 아웃게임 덱빌더 팝업만. 인게임 손패 롱프레스 peek = 후속 후보.
3. **팝업 본문**: 기존 자동 수치라인(effects[]) 유지 + 그 아래 authored `description` 블록 추가.

## 작업 단위

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | code | `0_so_field.md` | `DreamcatcherCard.description` 필드 추가 (append-only) |
| 1 | code | `1_deck_builder_popup.md` | `PopupBody()` 에 description 블록 렌더 (빈 값 graceful) |
| 2 | data | `2_card_text_authoring.md` | 카드 에셋에 description 텍스트 authoring |

## Feature-wide 계약

1. **append-only 직렬화**: `description` 은 SO 필드 끝에 추가한다(현재 마지막 `skill` 뒤).
   기존 22개 카드 에셋은 null/빈 문자열로 역직렬화 → inert.
2. **하드코딩 금지**: 표시 텍스트는 전부 SO `description` 에서 온다. 뷰는 문자열을 렌더만.
3. **구조화 formatter가 SoT**: effects[]·mechanics[]·attackMods[]·skill의 현재 값으로
   수치와 메커니즘 라인을 생성한다. `description`은 구조화 요약이 없는 카드에서만 fallback이다.
4. **graceful 빈 값**: `description` 이 비어있으면 팝업은 description 블록을 생략(기존 레이아웃 유지).
5. **표시 표면은 덱빌더 팝업 1곳**: 인게임 손패·기타 표면 변경 없음(이번 spec 범위 밖).

## 후속 후보 (이번 spec 범위 밖)

- **인게임 손패 롱프레스/홀드 peek**: 슬로모 중 소형 부채꼴 카드에서 홀드 시 description 표시.
  호버 감지·peek 패널 인터랙션 배선 필요 → 별도 spec.
- **Active 카드 팝업 노출**: Active 는 카탈로그(덱빌더) 밖이라 현재 팝업 미노출. 손패 peek
  후속과 함께 고려.
- ~~**자동 메커니즘 텍스트 생성**~~ → `docs/spec/dreamcatcher-card-effect-summary/` 로
  승격. 지원 매핑이 없는 새 enum 조합은 새 spec의 후속 후보로 관리한다.
