# dreamcatcher-card-effect-summary

> 상태: critic review 및 구현 보정 완료, 제품 검토 대기 (2026-07-22)

## 목표

드림캐쳐 카드의 설명을 authored 문장에 의존하지 않고, 카드 SO의 구조화된 효과 데이터에서
공통 템플릿으로 생성한다. 덱빌더, 덱 페이지, 유닛 인스펙트, 인게임 손패 툴팁은 같은
포맷터를 사용하며 `BodyCompact`는 블록 간격만 줄인다.

## 검증 질문

"카드 수치가 SO의 현재 값과 항상 일치하고, 플레이어가 트리거·대상·효과·조건을 한 번에
읽을 수 있는가?"

## 문서 목록

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | contract | `0_formatter_contract.md` | 출력 문법, 숫자 형식, 효과 매핑 |
| 1 | migration/test | `1_data_migration_and_tests.md` | 카드 데이터 전환, fallback, 검증 범위 |

## 연결 문서

- 기존 설명 필드: `docs/spec/dreamcatcher-card-description/`
- 공용 소비처와 툴팁: `docs/spec/dreamcatcher-hand-drag-tooltip/`
- 카드 타입: `docs/spec/dreamcatcher-card-taxonomy/`
- 트리거 정의: `docs/spec/dreamcatcher-unit-trigger/`

이 문서의 계약이 위 문서의 자동 설명/fallback 관련 문장과 충돌하면 이 문서를 우선한다.
기존 문서는 authored description 도입 및 공용 포맷터 추출의 이력/참조 문서로 남긴다.

## Feature-wide 계약

1. 텍스트 입력은 `DreamcatcherCard`의 `effects`, `mechanics`, `attackMods`, `skill`이다.
2. 기본 문법은 `[트리거/대상] → [효과] → [지속시간·조건·대가]`이다.
3. 수치는 카드 SO의 현재 값을 사용한다. ID별 문장 분기나 수치 하드코딩은 금지한다.
4. 유효한 수치는 invariant `0.##`로 표시해 후행 0을 제거한다. 퍼센트는 부호를 포함하고,
   배율은 `xN`, 시간은 `N초`, 범위/횟수/스택은 `N` 형식을 사용한다.
5. `description`은 SO inspection/export를 위한 평문 요약 mirror로 유지한다.
   지원되는 카드의 UI 출력은 구조화 데이터가 source of truth이며 `description`을 별도로 덧붙이지 않는다.
   구조화 요약을 지원하지 않는 카드에서만 `description`을 fallback으로 사용한다.
6. `Body`와 `BodyCompact`는 동일한 조립 경로를 공유한다. 소비처별 라벨 복제는 금지한다.
7. 새 트리거·페이로드·스킬 효과는 먼저 이 문서의 매핑과 테스트를 추가한 뒤 에셋에 사용한다.

## 후속 후보

- 지원되지 않은 enum 조합을 에디터 validation에서 경고하고 카드별 누락을 차단한다.
- 효과 라인마다 target을 반복하지 않고 한 번만 표시하는 레이아웃을 UX 검토한다.
- 한국어 문구를 별도 로컬라이제이션 테이블로 분리할 필요가 생기면 이 spec을 확장한다.

## 파이프라인 커버리지

N/A — 새 플레이 오브젝트나 생성→렌더 파이프라인은 추가하지 않는다. 순수 문자열 조립과
ScriptableObject authoring만 다룬다.
