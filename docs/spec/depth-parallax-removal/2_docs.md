# 2 — 문서 갱신

## 목적

패럴랙스를 가리키는 문서 2곳을 현행화하고 이 spec 을 색인에 올린다.

## 변경 대상

1. `docs/reference/test-procedure.md:18` — 「`Wassup.DepthParallax.Tests` 는 모듈 로컬이라 전체 실행 때만 따라온다.」 줄 삭제
2. `docs/reference/lessons/03-rendering-assets.md:167` — 2026-07-15 셰이더 스트리핑 사고 사례(「배치 컷신 뎁스 패럴랙스」)는 **이력이라 유지**. 괄호로 「(패럴랙스 기능은 2026-10-01 제거 — `docs/spec/depth-parallax-removal/`)」 한 줄만 덧붙인다
3. `docs/spec/README.md` — 「진행 중 spec」 절에 `depth-parallax-removal/` 한 줄(승인 시점에 추가) → 종료 시 「시작점」 목록으로 옮기고 완료 일자 표기
4. 이 spec `README.md` 상단 상태 줄 → 「완료 YYYY-MM-DD」

## 구현

- 교훈 문서는 사고 재발 방지가 목적이라 사례를 지우지 않는다. 기능이 사라졌다는 사실만 붙인다.
- handoff summary 는 작성하지 않는다 — 삭제 spec 이라 README 의 의존성 표가 곧 인계 지도다.

## 완료 기준

- [x] `rg -i 'depthparallax|패럴랙스' docs` 결과가 이 spec 폴더와 `lessons/03` 의 이력 1줄뿐
- [x] 커밋(경로 지정): 문서 3개 + 이 spec 폴더

확인 2026-10-01 · 커밋 = 이 파일을 포함한 문서 커밋.
