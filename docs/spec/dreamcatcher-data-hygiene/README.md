# Dreamcatcher Data Hygiene — 카드 데이터 오인/중복 필드 정리

> 상태: **unit 0~3 구현·검증 완료 2026-07-22 / 시트 재시드 대기** — 카드 표시명을 축약했고, 다음 import에서 되돌아가지 않도록 원격 `DcCards` 시트 재시드가 필요하다. 인계는 `2_handoff_summary.md`.
> 계기: dreamcatcher-sheet-sync 왕복 검증(완료) 후 시트 데이터 점검에서 발견한 오인·중복 필드.

## 목표

기획 시트/카드 SO 에서 **값을 중복 보유해 드리프트를 유발하거나, 소비하지 않는데도 기본값으로 찍혀 오인을 부르는 필드**를 정리한다. 값의 source-of-truth 를 단일화(effects.percent / mechanics 필드)하고, 표시용 수치는 템플릿이 authoritative 값에서 뽑아 쓴다.

## 검증 질문

- 카드 id/displayName 만 보고 "이 수치가 진짜냐?"를 되묻지 않아도 되는가? (수치는 오직 effects/mechanics 필드에만 존재)
- 기획자가 DcMechanics 시트에서 "이 셀이 이 카드에 의미 있나?"를 매트릭스 없이 셀만 보고 판단할 수 있는가?

## 작업 단위 목록

| 번호 | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 데이터+네이밍 | `0_card_id_name_scheme.md` | id 수치 제거(슬러그 통일) + displayName 한글 통일 네이밍 규칙 + 티어 규칙. **네이밍 표 사용자 확정 필요** |
| 1 | 구현(exporter) | `1_mechanics_discriminator_blank_export.md` | DcMechanics 판별자 필드(ccKind/stackKind/buffStat)를 소비하는 payload 행에만 export, 나머지 blank |
| 3 | 표시명 축약 | `3_abbreviated_display_names.md` | id/GUID/효과 데이터는 유지하고 전 카드 displayName을 효과 핵심어 중심의 짧은 이름으로 통일 |

## Feature-wide 계약

- **수치는 데이터 필드에만**: 카드 magnitude/percent 는 effects[]·mechanics 에만 존재. id/displayName 은 수치 무관 안정 식별자/이름.
- **표시 수치는 템플릿 경유**: 덱빌더 상세는 이미 `effects[].percent` 로 "{stat} +{percent}%" 자동 라인 렌더(`DreamcatcherDeckBuilderView.cs:499-506`). displayName 에서 수치를 빼도 UI 에 %는 유지된다.
- **id 는 안정 슬러그**: `{axis}_{stat}` 형(`all_atk`, `ranger_hp`). 매그니튜드 숫자 금지. 같은 (stat,axis) 의 티어 변형이 생기면 `_2`,`_3` 접미(매그니튜드가 아닌 서수).
- **displayName 은 축약형**: 핵심 대상·동작만 남기고 수치/조건은 설명 템플릿에서 표시한다. 동일 축은 `딜`/`속`/`체`/`이속`처럼 같은 약어를 쓴다.
- **id 파급은 GUID 무관**: 카탈로그·기본덱·기프트config 는 전부 GUID 참조라 id 리네임에 안 깨짐. id 문자열 의존은 ①시트(DcCards.id + 자식탭 cardId) ②저장된 덱 `DeckSave.cardIds`(dev-disposable) ③테스트 하드코딩뿐.
- **시트 재시드 필수**: SO id 를 바꾸면 다음 import 매칭이 깨지므로, 리네임 후 Export → 시트 갱신(운영 규칙 3). 시트 붙여넣기는 사용자 수작업.
- **판별자 blank 은 무손실**: import 는 partial-update(blank=기존값 유지)라 non-consuming 행을 비워도 SO 값 안 바뀜.

## 파이프라인 커버리지

플레이 오브젝트 신설/생성→렌더 경로 변경 **없음** — 기존 카드 SO 의 문자열 필드값 + exporter 만 변경. `docs/reference/object-pipeline-map.md` 대조 불요(N/A: 파이프라인 정거장 무변경).

## 후속 후보 (이번 스코프 밖)

- **description 수치 드리프트** [M] · farewell "100"↔실제 500, meteor "40"↔200 등 사람 텍스트에 박힌 수치가 authoritative 값과 어긋남. 토큰 치환(`{magnitude}`) 또는 문안에서 수치 제거 — 카드 수만큼 문안 재작성이라 별도 스코프.
- **`triggerPeriod` vs `triggerPeriodSeconds` 이름 겹침** [S] · 전자=N번째 공격(횟수)/후자=N초 주기(시간). `triggerPeriodSeconds` 는 현재 드림캐쳐 카드 전량 0(dead, PeriodicTimer 카드 없음). 이름 명확화 또는 dead 필드 제거 검토.
- **sub_* 카드 네이밍 통일** [S] · sub_deepsleep(EffectiveHealth)/sub_dreamhaste(AttackSpeed) 는 Subconscious 플레이버명이라 이번 통일 규칙에서 제외. 규칙에 편입할지 후속 결정.
- **시트 챗봇 rename 모드** [S] · upsert(키=id)는 id 변경을 표현 못 함(옛/새 id를 별개 행으로 보고 옛 행 orphan). unit 0 재시드 때 실제로 겪음 — id 바꾸는 변경은 해당 탭 "전량 교체" 프롬프트로 가야 함. 재사용 가능한 replace-mode export 프롬프트 옵션 검토.
