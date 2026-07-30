# 5 — 배선 · 검증 · 문서 정합

## 목적

Play e2e 로 검증 질문 3개에 답하고, `PresetApply` 삭제로 stale 해진 문서 포인터를 정정한다.

## 변경 대상

- 씬 배선: **신규 `SerializeField` 0 예상.** 메뉴 컨트롤러는 `_historyPanelView` 를 이미 들고 있고, 패널은 카탈로그 3종을 이미 받으며, `NoticePopup` 은 자기 부트스트랩이다. 실제로 0인지 확인하는 것이 이 unit 의 일부다
- `docs/spec/tournament-history-deck-view/README.md` 계약 12 — `PresetApply.WriteToProfile` 포인터
- `docs/spec/README.md` Follow-up Backlog → 토너먼트 덱 정보 → "프리셋 적용 기능" 항목
- 신규: `6_handoff_summary.md`

## 구현

**문서 정정** — 두 곳이 `PresetApply.WriteToProfile(profile, unitIds, cardIds)` 를 "붙일 자리" 로 가리키는데 그 메서드는 `authored-preset-removal` 에서 삭제됐다. 시그니처만 갈아끼우지 말고 **뜻이 바뀐 것**을 적는다: 옛 헬퍼는 확정 편성을 덮어썼고, 지금은 새 프리셋 + 미저장 작업본이다. 백로그 항목은 이 spec 으로 이관하고 남은 판단 세 개 중 실제로 남은 것만 남긴다 — (a) 드림스톤은 이제 `SquadPreset.stoneIds` 로 통합돼 함께 적용되므로 **해소**, (b) 미해석 id 는 계약 5 로 **해소**, (c) 저장 시점은 계약 1 로 **해소**.

**Play e2e** (실서버 계정 필요 — 히스토리에 남의 참가가 있어야 한다):

1. 로비 → 히스토리 → 남의 행 덱보기 → 스쿼드 탭 적용 → 스쿼드 페이지에 `"{이름}의 덱"` + 유닛·스톤 채워짐 + dirty. `[저장]` → 목록 셀 썸네일이 채워지고 dirty 꺼짐
2. 같은 행 드림캐쳐 탭 적용 → 드림캐쳐 페이지에 새 덱 프리셋 + 카드 채워짐 + dirty. `[되돌리기]` → 빈 덱, dirty 꺼짐
3. 내 행 덱보기 → 버튼 없음(기존 동작 무회귀)
4. `deckInfo` 없는 참가자 → "덱 정보가 없습니다" + 버튼 비활성
5. 적용 → 이동 → 곧바로 다른 프리셋으로 전환 시도 → dirty 경고 팝업이 뜬다(작업본 규율이 그대로 산다)
6. 콘솔 에러 0

**제외 안내 실측이 어려운 경우**: 실데이터에 미해석 id 가 없으면 EditMode 로만 고정하고 그 사실을 handoff 에 적는다. 실측을 못 한 것을 검증했다고 쓰지 않는다.

## 완료 기준

- [x] EditMode 전체 그린 (1,736 pass / 2 existing ignored, 신규 46 pass)
- [ ] Play e2e 1~6 통과 · 콘솔 에러 0
  - 2026-07-31: 명시적 live E2E를 추가하고 실행했으나 현재 로그인 계정의 히스토리에 적용 가능한 외부 참가자 덱이 없어 적용 단계 진입 불가. 자동화 가능한 코어·팝업·라우팅·페이지 픽업은 신규 46개 테스트로 고정했다.
- [x] 신규 씬 배선이 실제로 0인지 확인 — 런타임 빌더와 기존 패널 참조만 사용
- [x] 문서 포인터 2곳 정정 + 백로그 항목 이관
- [x] `docs/reference/object-pipeline-map.md` 확인 — 플레이 오브젝트 변경이 없어 갱신 불요
- [x] `6_handoff_summary.md` 작성 (Commit / Implemented / Key Files / Verified / Notes / Follow-up)
