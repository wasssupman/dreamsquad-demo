# Implementation Review Loop

## 원칙

이번 작업은 큰 기능을 한 번에 완성하지 않는다.  
문서 -> 작은 구현 -> 시각 확인 -> critic review -> 수정 순서로 반복한다.

## 리뷰 단위

각 구현은 아래 단위로 끊는다.

### Step 1. BoardVisualPlan Builder

- region grouping
- mask 계산
- shape 분류

리뷰 포인트:

- source of truth 분리 여부
- `MapView` 로직 유출 여부
- deterministic 보장 여부
- 같은 seed 재생성 일치 여부

### Step 2. Zone Transition Rendering

- `Walk`
- `Place`
- `Env`
- edge/corner 표현

리뷰 포인트:

- zone readability
- 셀 단위 패치감 감소 여부
- 검은 outline 과도 여부
- `Walk` / `Place` hard cut 감소 여부

### Step 3. Decor Placement Rewrite

- `BoardVisualPlan` 입력 기반 anchor 배치
- prop density / spacing 조정

리뷰 포인트:

- 중앙 과밀 / 외곽 과소 문제
- 큰 프랍, 작은 프랍 역할 분리
- gameplay 방해 여부
- `Walk` 침범 없음

### Step 4. Theme Asset Pass

- forest 우선
- 필요한 transition/decor asset 보강

리뷰 포인트:

- 구조 문제를 asset 으로 가리고 있지 않은지
- asset 품질이 구조적 결함을 대체하려 하지 않는지

## 각 단계 산출물

반드시 남길 것:

- spec 문서 반영
- 구현 파일 목록
- 테스트 또는 검증 방법
- 스크린샷 또는 MCP 시각 확인 결과
- critic review 요약

## Done 기준

어떤 단계든 아래가 없으면 완료 처리하지 않는다.

- 코드/문서가 같은 방향을 가리킬 것
- 시각 결과 확인이 있을 것
- critic 리뷰에서 치명 리스크가 해소됐을 것
- 기계적 검증 항목이 있을 것
