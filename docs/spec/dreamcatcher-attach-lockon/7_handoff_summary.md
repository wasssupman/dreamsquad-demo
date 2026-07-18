# 7 — Handoff Summary

## Commit

- **`b487ac42`** feat(dreamcatcher-attach-lockon): 부착 조준 포커스 락온 + 오프셋 콜아웃
- 후속 docs 커밋: README 상태 라인 + 이 handoff.
- **rev(커밋 후 사용자 Play 피드백 2건)**: ① 살찌운 제물(적 표식)을 **유닛 위**로 가리키면 "유닛 불가" 빨강 무효(기존엔 적만 픽해서 유닛 위=무반응) — `SetAimEnemyMark` 로 슬롯이 유효성 결정, 화살표/리티클/콜아웃 공유. ② 화살표(`DreamcatcherTargetArrow`)가 config 를 `Configure` 스냅샷 대신 **매 SetPath 라이브 읽기**(arrowHeadSize·색 Play 중 반영, 드래그 프레임당 1회·무할당).
- **rev(포커스 유닛 틴트 · F 블록)**: 락온 defender **몸체(Spine)에 상태색 곱셈 틴트**(valid=시안/invalid=붉음) 재도입 — 계약 #6 진화(제거→dim 맥락서 상태색 복귀). `DreamcatcherFocusPresenter.UpdateFocusTint`/`SetFocusTint` 소유, 유닛 전환 시에만 on/off(락온 유닛 valid 불변), End/OnDisable 원복. AttachAim/DefenderCast 만(EnemyMark 제외). 노브=`DreamcatcherFocusConfig` F. **`SpineUnitView.FlashRoutine` 1줄 가드**(hover 활성 시 `_savedTint` 복귀)로 flash×hover stray-tint 레이스 차단(잠재 버그 동반 수정). 코드리뷰 APPROVE(회귀 위험 낮음).

## Implemented

- **단일 facade `DreamcatcherFocusPresenter`** — dim(전장 감광) + 유효 base-ring + 락온 리티클 + 오프셋 콜아웃 + 확정 펄스. 애셋-프리 UI 쿼드, canvasRoot 직속 + `SetSiblingIndex` 로 레이어 강제, `Update` 가 `SetAim` 상태를 매프레임 적용(움직이는 대상 추종).
- **오프셋 콜아웃**(핵심) — 아이콘+이름+부착수 X/3 를 락온 유닛 **위(손끝 밖)** 에. pick=손가락 밑이라 정체를 손가락 밖으로 뺀 것이 이 feature 의 근거.
- **화살표 3-상태** — idle=흰 / 부착가능=하늘 / 부착불가=붉 + 삼각 화살촉(절차 스프라이트) 확대 + 아웃라인. 락온 시 끝점을 대상 중심으로 blend.
- **유효성 = 부착캡 AND 기여여부** — `bridge.WouldDreamcatcherCardApply` → 순수 `DreamcatcherAttachEval`. 통통구슬(ProjectileBounce)=투사체 유닛만, 끝을 보는 눈/HeavyStrike=데미지 output 필요. UI↔커밋 일치.
- **적 표식(살찌운 제물)** 도 리티클/콜아웃(카드 정체+표식상태)+끝점 당김. `IsEnemyMarked` 로 유효성.
- 정체 히스테리시스(밀집 플리커 차단) · 생명주기 하드클리어(Close/ForceClose/OnDisable/OnPhaseChanged) · **CanvasScaler scaleFactor 환산**(리티클 arm·콜아웃 clamp — 비-1080p 실기기).

## Key Files

- `Scripts/UI/Dreamcatcher/DreamcatcherFocusPresenter.cs` (★ 프레젠터 facade), `DreamcatcherFocusConfig.cs`(SO), `DreamcatcherTargetArrow.cs`(3-상태·삼각촉)
- `Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs`(BeginFocus·SetAim·IsHoverAttachable·히스테리시스), `DreamcatcherHandView.cs`(소유/배선/하드클리어)
- `Scripts/Core/Dreamcatcher/DreamcatcherAttachEval.cs`(순수 preflight) + `Tests/EditMode/DreamcatcherAttachEvalTests.cs`
- `Scripts/Bridge/BattleBridge.cs`(읽기전용 3: 열거/렉트/데이터), `BattleBridge.Dreamcatcher.cs`(IsEnemyMarked)
- `Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs`(public 3: CanAttachMore/AttachCountOf/MaxAttachPerUnit)
- `Data/Dreamcatcher/DreamcatcherFocusConfig.asset`(guid 4c0ee755, 전 노브), 씬 배선 = BattleScene `DreamcatcherHandView.focusConfig`

## Verified

- 컴파일 0 에러. 프레젠터 구성 스모크(CanvasScaler 환경) OK.
- `DreamcatcherAttachEval` 9/9 케이스 execute_code 검증(EditMode 러너는 사용자 Play 중이라 미실행 — Play 종료 시 러너 가능).
- 사용자 Play 확인: 통통구슬 궁수(가능)/가디언(불가·빨강), 부착수 X/3 표시.
- 투트랙 리뷰: ecs=APPROVE(읽기전용 확인), code=REQUEST CHANGES → H1(좌표계)+정리 반영.

## Notes (되돌리면 안 됨)

- **콜아웃 = 손가락 밖 정체 주신호** — pick 이 손가락 밑이라 유닛 위 신호는 가림. 콜아웃 없으면 전제 붕괴.
- **빨강 전체 틴트 제거** — Spine 곱셈 틴트라 "밝힘" 불가. 리티클+콜아웃으로 대체.
- **preflight ↔ apply 동기화 계약** — `DreamcatcherAttachEval` 은 `ApplyDreamcatcherCardToUnit` 의 유닛-종속 게이트만 미러. 새 유닛-게이트 kind 추가 시 eval+테스트 갱신.
- **BattleScene 은 focusConfig 1줄만 커밋** — 병행 세션 씬 작업/재직렬화 노이즈는 워킹트리에 남겨둠(사용자 소유).
- CanvasScaler scaleFactor 환산은 비-1080p 필수 — 임의 제거 금지.

## Follow-up

- **실기기(1440p/태블릿) 리티클 밀착 육안 검증** — H1 수정 실증(에디터 1080p 로는 마스킹).
- EditMode `DreamcatcherAttachEvalTests` 러너 실행(Play 종료 후).
- 후속 후보(README): pick↔base-ring 열거 완전 공유(현재 프레임당 2회 순회, 보드 규모 수용) · 적 base-ring · dim 데새춰 post-process · 리티클/콜아웃 드림-웹 테마.
- 색감/머리 크기/dim 세기 등 최종 튜닝은 `DreamcatcherFocusConfig.asset` 라이브.
