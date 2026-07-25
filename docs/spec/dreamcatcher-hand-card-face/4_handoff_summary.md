# Handoff — dreamcatcher-hand-card-face

## Commit

- `c1cbc33f` / `689f7409` — spec 작성 + rev 1(정보전달 리뷰: 카드 확대·태그 역할 병기·dim 가독)
- `30c10e3f` — unit 0: 스타일·라벨 단일 소스(`CardCategoryStyle` 손패 확장) + `BodyLinesOnly` + EditMode
- `911056ec` — unit 1: 카드 면 재구성(아트 제거 → 투톤 face + 텍스트, `MakeCardFace`)
- (마무리 커밋 — unit 2 픽스 + unit 3 브리핑 + 적 지정 태그, 이 파일과 같은 세션)
- 씬 `cardOverlap: 16` 은 타 세션 씬 커밋 `aba97b44` 에 포함됨

## Implemented

- 손패 카드(184×230, 겹침 16): 타입색 헤더 밴드(Squad 블루/Unit 골드/Active 청록) + 대상 태그 칩
  (`전체 버프`/`아군 부착`/`적 지정`/`타일 지정`…) + 이름(헤더) + 효과 본문(18~24pt, 화살표 강제
  줄바꿈) + 무의식 보라 테두리. 아트는 손패에서만 제거(덱빌더 무변경).
- 카드 면 = `UiRoundedSprite.MakeCardFace` 투톤 풀렉트 스프라이트 → `UiCardFaceMesh`(크럼플 보존),
  타입×무의식 캐시. dim = solid 유지 + face 틴트만(알파 dim 금지).
- 상단 드래그 툴팁 → **조작 브리핑**: 조작법(AimMode 별 고정) + 상태(실시간·색 코딩,
  `ShowDragBriefing`/`UpdateDragBriefingStatus`). 카드 설명 노출 제거.
- 적 지정 판별 `DreamcatcherCard.HasBountyMark()` 단일 소스화(조준 라우팅 + 태그 칩 공유).

## Key Files

- `Assets/_Project/Scripts/UI/Outgame/CardCategoryStyle.cs` — 타입 색/테두리/대상 태그
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` — 슬롯 레이아웃·face 캐시·브리핑 위젯
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` — 브리핑 문안(ControlsFor/StatusFor)
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardText.cs` — `BodyLinesOnly`(헤더 제외+화살표 줄바꿈)
- `Assets/_Project/Tests/EditMode/HandCardStyleTests.cs` — 19케이스

## Verified

- EditMode 전체 1297/0/2 (unit 0 시점) + 이후 `HandCardStyleTests` 19/19, 최종 전체 스위트는 마무리
  커밋 직전 재실행(결과는 커밋 메시지 참조). compile 0 에러.
- 사용자 실플레이 스크린샷 이터레이션 3회: 랩 유출/알파 dim 투명/폰트 크기/문안 수정 반영 후 종결.
- PlayMode 6건 실패는 본 spec 표면 밖(인증 서버·Gift 페이즈 플로우·씬 전환·덱 캐리인·CcEffect ECS)
  — 콘솔에 본 spec 코드발 예외 0. **별도 트래킹 필요(타 세션 in-flight 작업 대조).**

## Notes

- TMP 라벨은 랩/오버플로를 **명시 설정**(코드베이스 관례 — 기본값 신뢰 금지). 되돌리면 유출 재발.
- 카드 면 알파 dim 금지(solid 계약) — dim 신호는 face 틴트가 전담.
- `BodyCompact` 는 현재 프로덕션 호출처 0 이나 전용 테스트가 있는 공용 포맷터라 유지.
- 프로토 성격: 정식판은 원안(아트 카드) 복귀 전제 — 이 spec 은 테스터 즉시 이해가 목적(README 결정 6).

## Follow-up

- `docs/spec/README.md` Follow-up Backlog "손패 카드 시인성" 서브그룹 참조 (아웃게임 문법 통일 ·
  실기기 폰트 확인 · PlayMode 기존 실패 6건 트래킹 · 태그 아이콘화/색약).
