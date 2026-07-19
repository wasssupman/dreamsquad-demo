# 5 — 안내 카드 접힘 (읽힌 뒤 우상단 칩)

## 목적

배치 페이즈 기믹 안내 카드가 상단 중앙에 상시 떠서 보드 상단(적 경로)을 가린다. "룰을 읽는 순간"의 주목성은 유지하되, 읽힌 뒤에는 우상단 작은 칩으로 접혀 배치 중 맵 시야를 확보한다. (README 후속 후보 "등장 연출/배지" 계열의 배치-페이즈 한정 구현.)

## 변경 대상

- `Assets/_Project/Scripts/UI/GimmickGuideView.cs` — 칩 빌드 + Expanded/Collapsed 상태 전환.
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — `public event Action DragBegan;` 추가.
- `Assets/_Project/Scripts/UI/DefenderSelector.cs` — `[SerializeField] GimmickGuideView gimmickGuide` + Bind 호출(costDisplay Bind 패턴 미러).
- `Assets/_Project/Scenes/BattleScene.unity` — DefenderSelector 에 GimmickGuideView 참조 배선.

## 구현

1. **상태 모델**: Placement 진입 시 Expanded — 풀 카드가 **오른쪽 화면 밖에서 우상단 코너로 슬라이드-인** (rev 2026-07-19: 상단 중앙 → 우상단 도킹 + 등장 연출. 카드/칩이 같은 코너를 공유해 "카드가 그 자리에서 칩으로 접히는" 동선). 폭 640 카드 왼쪽 끝은 상단 중앙 배치 배너(±280)와 겹치지 않음. 아래 중 먼저 오는 트리거로 Collapsed 전환:
   - 첫 배치 상호작용 — 컨트롤러 `DragBegan`(신설) 또는 기존 `Armed` 이벤트.
   - 카드 하단 **"가즈아" 확인 버튼** 탭 (rev 2026-07-19 — 명시적 닫기. 버튼 배경만 raycast 대상, 카드 나머지는 입력 통과 유지).
   - `autoCollapseSeconds`(SerializeField, 기본 3초 — rev: 6초→3초) 경과. 타이머는 unscaled time(드래그 슬로우모 영향 배제).
2. **칩**: SafeAreaRoot 자식, 카드와 같은 우상단 코너(앵커 1,1, `cardCornerOffset ≈ (-40, -40)` 공용). 다크 플레이트+골드 보더(`UiRoundedSprite`) 재사용, 내용 = `특수룰 · {displayName}` 한 줄 TMP. **칩만 raycast 대상**(Button) — 탭 시 Expanded 복귀(같은 슬라이드-인) + 트리거 재무장(타이머 리셋, 다음 드래그/arm 에 다시 접힘). 카드 몸체는 `raycastTarget=false` 유지("가즈아" 버튼 제외).
   - 우상단은 Battle 페이즈 스코어 HUD 자리지만 이 뷰는 Placement 전용이라 충돌 없음.
3. **전환 연출**: 카드 페이드+스케일 아웃(~0.18s) → 칩 팝인(OutBack). PrimeTween `Sequence.Create(useUnscaledTime: true)` — 프로젝트 UI 표준(BossWarningView 선례), 수제 코루틴 금지(리뷰 반영 2026-07-19). 페이즈 이탈/disable 시 `_seq.Stop()` + 카드/칩 모두 숨김 + 상태 리셋(재진입 시 Expanded 부터, stale 타이머 재무장).
4. **DragBegan 발화 지점**: `BeginDrag` 의 가드(조준 잠금 early-return, null 체크) **통과 후** invoke — 실제 배치 세션이 시작될 때만 접힘.
5. **배선 seam**: 컨트롤러가 런타임 부착이므로 뷰가 직접 참조 불가. DefenderSelector 가 씬 참조(`gimmickGuide`)를 받아 컨트롤러 준비 후 `gimmickGuide.BindPlacementActivity(controller)` 호출. 뷰는 Bind 시 `DragBegan`/`Armed` 구독, `OnDisable` 에서 해제. 미할당이면 이벤트 트리거 없이 타이머만으로 동작(성능 저하 없는 폴백).
6. **불변 유지**: 페이즈 게이트·`AssignedGimmick==null` 미표시·sortingOrder 8·카드 입력 비차단은 현행 그대로. GimmickData 에 아이콘 필드 추가하지 않음(스코프 밖).

## 완료 기준

- [ ] 컴파일 통과, 콘솔 에러 0.
- [ ] Play: 배치 진입 → 카드가 오른쪽 화면 밖에서 우상단으로 슬라이드-인. 3초 방치 → 칩으로 접힘. (재진입 후) 즉시 드래그 시작 → 접힘. tap-to-place arm → 접힘. "가즈아" 버튼 탭 → 즉시 접힘.
- [ ] 카드가 상단 중앙 배치 배너/좌상단 메뉴버튼과 겹치지 않음.
- [ ] 칩 탭 → 풀 카드 복귀, 이후 드래그 시 다시 접힘.
- [ ] 접힌 상태에서 보드 상단(적 경로) 가림 없음, 칩이 배치 드래그 입력을 방해하지 않음.
- [ ] Battle 진입 시 카드/칩 모두 사라짐. 기믹 비활성 시 카드/칩 미표시.
- [ ] gimmickGuide 미배선 폴백: 타이머 접힘만으로 동작(에러 없음).

확인: 2026-07-19 사용자 Play 확인(슬라이드인·접힘·칩·가즈아), 커밋 `2d267bc0`. 코드 리뷰 5건 반영(PrimeTween 전환·타이머 재무장·StopAnim·트리거 단일화·머티리얼 공유).
