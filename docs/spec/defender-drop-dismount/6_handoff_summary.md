# 6 — handoff summary

## Commit

- `36faf95c` docs: spec 설계 승인분
- `ad886013` unit 0 — `KeyringSim.DismountPoint` 하마 궤적 순수 수학 + EditMode 6건
- `fe53bd45` unit 1 — 뷰 오버라이드 중립화(`SetDefenderViewOverride` 계열) + `DragSwaySettings` ⑩ 노브
- `ccd59103` unit 2 — 릴리스 커밋 직후 실유닛 하마 비행(핸드오프·안전망·클램프)
- `bf14f7fa` unit 3 — 스폰 연출 착지 프레임 이동, 활성화 시계 commit 기준 유지
- `de012a2b` unit 4 — 고리·줄 잔류(반동 벙음→분리 스냅→페이드)
- `cd02fa3e` unit 5 — `DropDismountTest` 계약 회귀 가드
- `35bb5642` fix — 착지 앵커 sim/view 혼동 텔레포트 수정(ToView 출력 미러) + 앵커≡렌더좌표 단정

## Implemented

- 실드래그 릴리스: 고스트 자리에서 반동(0.12s, 잔여 스윙 속도 Hermite 흡수) → camUp 아치 솟음 → 타일 수직 스틱 착지 (총 0.45s, unscaled)
- 커밋(점유·코스트·이벤트)은 릴리스 프레임 그대로 — dismount 는 순수 뷰
- 드롭 창 ⊆ pending 창(deploymentDuration 클램프): 공중 유닛은 공격·피격·재배치 진입 불가
- 스폰 연출(링·PlayDeploy·placementVfx)은 착지 프레임, 활성화는 commit+deploymentDuration 불변
- 고리+줄이 릴리스 자리에 잔류 → 분리 스냅 → per-renderer 색 페이드(머티리얼 무오염)
- facing 유닛 병행(aim 과 동시 진행, 연출은 aim 경로 현행 유지)
- 안전망: 프레임별 binding abandon + OnDisable/OnDestroy 즉시 완결 + 잔류물 하드캡 자멸

## Key Files

- `Assets/_Project/Scripts/UI/KeyringSim.cs` — `DismountPoint`
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — `StartDropDismount`/`RunDropDismount`/`KeyringRemnant`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `TryGetDefenderRestViewPos`(주의점 참조)
- `Assets/_Project/Data/Config/DragSwaySettings.asset` — ⑩ 드롭 노브(라이브 튜닝)
- `Assets/_Project/Tests/PlayMode/DropDismountTest.cs` · `Tests/EditMode/KeyringSimTests.cs`

## Verified

- EditMode 1547 전건 green(신규 6건 TDD red→green)
- `DropDismountTest` green: 핸드오프 팝 <0.05 world · 활성화 = commit+0.45s(−0.05~+0.25 창) · 탭 게이트 · 세션 독립 · **착지 앵커 ≡ 렌더 좌표**
- 전체 PlayMode 57건: 이 feature 신규 실패 0건(실패 11건 전부 기존 baseline 부분집합, 2026-07-28 기록)
- Play 육안(사용자): 2s 관찰 모드로 텔레포트 결함 발견→수정 후 정상 확인

## Notes (되돌리면 안 되는 의도)

- **`TryGetDefenderRestViewPos` 는 피드의 출력(ToView + offset)을 미러한다** — 입력(sim)을 반환하면 화면 이탈→착지 텔레포트 재발(35bb5642 의 버그). `ApplyRenderPosition` 공식이 바뀌면 같이 바꿀 것.
- 시간 이징 선형 — Out* 는 끝속도 0 으로 스틱 착지를 물러지게 함. 착지 임팩트는 기하(끝접선)가 만든다.
- `dropTotalSeconds` 는 deploymentDuration 클램프 하에 있다 — 0.45 넘게 튜닝해도 늘어나지 않음(의도). 늘리려면 deploymentDuration 과 함께.
- `minArcHeight` 는 제어점 높이 semantics(실제 apex ≈ 0.4×).
- 잔류 페이드는 per-renderer 색만 — 공유 머티리얼 알파 건드리지 말 것(다음 세션 줄 투명화).

## Follow-up

- 착지 임팩트(스쿼시·먼지)를 탭·재배치 착지와 공유 모듈로 통일 (README 후속 후보)
- 착지/줄 텐션 사운드
- 스타일 셰이더가 버텍스 색 무시 시 페이드 미표시(우아한 열화) — 스타일 교체 시 확인
