# 3 — 테스트 절차 문서화

## 목적

두 lane 체계와 판정 규칙을 한 곳에 모은다. 지금 절차 지식은 lessons 01 처방 ·
spec README 사전 실패 절 · 세션 관행에 흩어져 있고, TRD 의 테스트 정책은 Phase
시절 문구("Phase 당 EditMode 1개 + PlayMode 1개")라 현행과 어긋난다.

## 변경 대상

- 신설: `docs/reference/test-procedure.md` — 상황별 실행 매트릭스 · lane 정의 ·
  판정 규칙(에디터 실행 원칙, 기지 실패 취급) · 새 테스트를 어느 lane 에 둘지 · 유지보수 규율
- `CLAUDE.md` 참조 문서 표에 1줄 추가
- `docs/TRD.md` 섹션 4.5/6 의 Phase 시절 문구를 현행으로 정정 (한 줄 수준)

## 구현

`docs/reference/test-procedure.md` 는 "실행"과 "작성" 두 절만 둔다. 이력·진단은
spec 폴더에 있으므로 복제하지 않고 링크한다. 기지 실패 목록도 복제하지 않는다 —
정본은 `docs/spec/README.md` 사전 실패 절이고 여기서는 가리키기만 한다(두 곳에
적으면 반드시 갈라진다).

## 완료 기준

- [x] `docs/reference/test-procedure.md` 가 4가지 상황의 실행 명령과 예상 시간을 제시
- [x] 새 테스트의 lane 판별 기준이 한 문장으로 적혀 있다 (실에셋 로드 여부)
- [x] CLAUDE.md 참조표에서 도달 가능
- [x] TRD 4.5 의 Phase 시절 문구를 정정하고 test-procedure.md 를 정본으로 지목
- [x] 기지 실패 목록을 복제하지 않고 docs/spec/README.md 를 가리킨다

2026-08-16 작성 완료.
