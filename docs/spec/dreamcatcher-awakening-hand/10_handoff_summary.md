# 10 — Handoff Summary (완료 2026-07-10)

## Commit

spec `26ee19dc` → units 0~8: `c52f7c9e`(config/보상필드) `0b6000fd`(각성 런타임) `6ae9b0a3`(Active 데이터) `7019e928`(순환큐) `50b7ce90`(컨트롤러+3중1 dormant) `e41ddf37`(게이지 UI) `d4922d6c`(손패/플립/슬로모) `ddaf08f6`(스와이프) `a6b9dd2d`(Active 사용+SkillBar dormant).
rev 4 실플레이 피드백: `7a9aed09`(확정지연 제거) `c9d02e2e`+`784faf23`+`5a6146c9`(호버 = 스크린 픽킹 + 붉은 스파인 틴트 단일) `4ddb21d9`(네임 밴드) `05e78af6`(StS 화살표).
rev 5: `96c6ac3d`(Squad 호스트 바인딩). 데이터 동기화: `e8acc531`(기본덱) `c56a20d2`(카탈로그 16장+아트) `8dd6e621`(카탈로그 sync 테스트). 리팩토링: `f81040a4`+`4f094fef`(simplify 4각도). 원격 병합 `2db1531f`.

## Implemented

- 각성 재화: 적 처치(SO별 1~3)/아군 사망(4) → 게이지(상한 100, config). 우하단 게이지 버튼 UI.
- CR식 순환 손패: 세이브덱 10 + 매판 Active 2(SkillLoadout 롤 재사용) = 12장 시드 셔플, 손패 = front 5.
- 사용: 손패 토글(플립 연출) → 드래그. Unit/Squad/Active-유닛대상 = StS 화살표 + 호버 유닛 붉은 틴트 + 유닛 드롭. Active 타일 = 범위 프리뷰, Portal = 2탭. touchup 즉시 커밋(성공 시에만 차감·순환), 취소 = 손패 복귀/ESC/토글/phase 이탈.
- 순환: Active 사용 → 맨 뒤. Unit·Squad → 호스트 부착(아웃풀) → 호스트 사망 시 회수(+Squad 는 효과 철회 = stackId 중립화 재적용). 부착 캡 3 합산.
- 슬로모: 손패 열림 동안 Battle 도메인 lease(0.3x). 구 3중1·SkillBar·bridge.skillRuntime dormant.

## Key Files

- `Core/Dreamcatcher/DreamcatcherHandController.cs` · `DreamcatcherCycleDeck.cs`
- `UI/Dreamcatcher/DreamcatcherHandView.cs` · `DreamcatcherCardDragSlot.cs`(AimMode 단일 분류) · `DreamcatcherTargetArrow.cs` · `AwakeningGaugeView.cs`
- `Bridge/BattleBridge.cs`: 각성 릴레이(1783/1963 드레인) · TryScreenToCell/TryGetDefenderAt/TryPickDefenderAtScreen/SetDefenderHoverHighlight(~2570) · ApplyDreamcatcherCardHosted/Revoke(~2510)
- `Battle/Units/AwakeningReward.cs` + `EnemyKilledEvent.awakeningReward` + `DamageApplicationSystem` 스탬프
- `Data/Dreamcatcher/AwakeningConfig.{cs,asset}` · `Presentation/SpineUnitView.cs`(TryGetScreenRect/SetHoverHighlight)

## Verified

- 컴파일 클린 · EditMode 614/614 그린(CycleDeck 7·CatalogSync 3 포함; 기존 리더보드 실패는 `a69d07e9` 로 해소) · ecs-review(unit 1) CRITICAL/HIGH 0 · 설계 critic REVISE → C1/H1/H2 폐색 · simplify 4각도 반영.
- 사용자 Play 종합 확인 2026-07-10 "플레이 감각 좋음". 콕콕바늘 dc 발사는 세션 로그로 검증(마크스맨 flat 20 × 3회 — 시인성만 낮음, 미해결 옵션 아래).

## Notes (되돌리면 안 되는 의도)

- Active 는 SkillRuntime 쿨다운·CostRuntime 미사용 — **bridge 의 skillRuntime 씬 배선 해제(fileID:0)가 이를 지탱**한다. 재배선하면 순환 재사용이 쿨다운에 막힌다(critic C1).
- 유닛 타겟 검출은 스크린 렉트 픽킹 1차 — 보드평면 셀 조회는 틸트 빌보드 몸체 포인팅을 놓친다(근본 원인 문서화, 계약 10).
- Squad 철회는 중립화(항등원) 트릭 — 두 번째 철회 수요가 생기면 Effects 에 진짜 remove 프리미티브로 심화(altitude 리뷰 단서).
- 확정 지연(pending)·타일 하이라이트·Squad anywhere-touchup 은 **사용자 결정으로 폐기** — 재도입 금지.
- 새 카드 = SO + art + **카탈로그 등록**까지가 한 세트 — `DreamcatcherCatalogSyncTests` 가 강제.

## Follow-up

- 콕콕바늘 등 dc 투사체 시인성(강조/발동 연출/스태거) — 사용자 결정 대기.
- Android 실기기 터치 스와이프 확인(에디터 마우스 기준 완료 인정).
- 리플레이/슬로모 재현 실측(G2), 밸런스 계수 튜닝(G1·G3: 사망 지분 30%+), 손패 유효성(G4).
- simplify 스킵: 스크린→셀 잔여 4사본 통합 · SkillBar 조준 상태기계 공용화 · UiLayer.EnsureOverlayCanvas.
- 구 3중1/SkillBar 코드 완전 삭제 cleanup · 부착 카드 유닛 위 뱃지 시각화 · offer/pick 로그의 손패 모델 대체.
- profile.json 세이브덱은 dev 편의로 유닛 4장 교체됨(백업 `.bak`) — 실사용자 마이그레이션 아님.
