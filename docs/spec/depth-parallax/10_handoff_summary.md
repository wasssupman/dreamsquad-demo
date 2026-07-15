# 10 — Handoff Summary (depth-parallax)

> 구현 u0~u8 완료 + u9 자율 파트(튜닝·배선·프리뷰) 완료. **미커밋.** 남은 것: 사용자 실기기/Play 체감.

## Commit

- `5cbee1b4` feat(defender-deploy-cutscene): Guardian 컷신 프레임 49장 추가
- `de2275ee` feat(depth-parallax): 뎁스맵 패럴랙스 모듈 + 배치 컷신 통합

main 로컬 커밋(push 안 함). 사전 존재 dirty(Mobile_RPAsset/fonts/probuilder/LiberationSans)는 의도적으로
미포함 — 파일 단위 격리 스테이징([[project_git_needs_sandbox_disabled]]: 쓰기 git 은 `dangerouslyDisableSandbox:true`).

## Implemented

- **모듈 `Wassup.DepthParallax`** (in-repo asmdef, `references: []`, 게임코드 무의존, 폴더 복사 이식):
  `DepthParallaxSettings`(제네릭 SO), `DepthParallaxMath`(순수 UvOffset+SpringStep), `DepthParallax_UI`
  셰이더(3 큐: 뎁스 패럴랙스·클립공간 사다리꼴·하이라이트, 전부 `_Tilt` 게이트), `DepthParallaxView`
  (정적 카드용 컴포넌트), Editor `DepthMapBaker` + `Tools~/depth_bake.py`(DA-V2 Small, Apache).
- **컷신 통합**: `DeployCutscenePlayer` 에 틸트 스프링(플레이어 소유, staleness watchdog)·per-instance
  머티리얼·뎁스 lockstep 스왑·5-arg `Play(color,depth,fps,scale,offset)` 오버로드 추가.
- **입력**: `DefenderDragPlacementController` 가 스와이프 속도→정규화 틸트를 매 프레임 `SetTilt` 피드
  (tiltGain 컨트롤러 단독 소유, 로컬 `swipeDt`). `DefenderUnitData.deployCutsceneDepth[]`(길이 1=정적
  공유/N=프레임별)+`deployCutsceneTiltGain`.
- **배선**: `Wassup.Runtime.asmdef` → 모듈 참조(단방향). `DefenderSelector` 가 `DepthParallaxSettings`
  를 플레이어에 주입(null-safe; 미할당이면 플레이어 fallback 기본값).
- **자산**: 컷신 3유닛 전부 정적 뎁스 1장(`DepthMapBaker` R8 임포트) → 각 SO `deployCutsceneDepth` 할당.
  Guardian(90×90 자동 bake), Archer(320×180 자동 bake), **Ranger(640×360, 사용자 제공 `Ranger_003-depth`)**.
  `DepthParallaxSettings.asset` 생성.
- **튜닝**: 기본값 amp 0.035 / persp 0.05 / highlight 0.12 / **tiltDamping 19(임계감쇠 — 낮으면 이미지 출렁)**.
  스와이프 정규화는 `DragSwaySettings.deployCutsceneSwipeRefSpeed(1400)/Smoothing(0.5)`.
- **뎁스 후처리**: `depth_bake.py --contrast`(0.5 힌지 기준 near/far 벌림). Ranger 는 사용자 뎁스에
  threshold-pivot 톤다운(thresh 0.4·contrast 2.2 → 배경 억제·실루엣 선명) 적용. 원본 백업은 scratchpad.

## Key Files

- `Assets/_Project/Modules/DepthParallax/**` (모듈 전체)
- `Assets/_Project/Scripts/UI/DeployCutscenePlayer.cs` (틸트 스프링+뎁스 스왑+SetSettings)
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` (스와이프→틸트)
- `Assets/_Project/Scripts/UI/DefenderSelector.cs` (settings 주입)
- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` / `DragSwaySettings.cs` (필드)
- `Assets/_Project/Data/DepthParallaxSettings.asset`, `.../Cutscene/Guardian/Depth/Guardian_depth.png`

## Verified

- EditMode `DepthParallaxMathTests` 6/6 pass.
- rest no-op: 오프스크린 렌더 diff tilt=0 vs `UI/Default` = **0px**; tilt≠0 = 16286/16384px 변화.
- 셰이더 컴파일 + `isSupported=True`. 전 웨이브 컴파일 클린(에러 0).
- 실 Guardian 아트 + 실 뎁스 tilt 프리뷰(rest/0.5/1.0/-1.0) 정상 — 트라페조이드 회전감 + 뎁스 패럴랙스.
- `depth_bake.py` 실동작(49프레임→대표 프레임 단일 뎁스). `DepthMapBaker` R8/linear/no-mip/uncompressed 임포트.

## Notes (되돌리면 안 되는 의도)

- `_DepthSign` 은 `(depth-_DepthCenter)` 뺄셈 **후** 전체 항에 곱(raw 에 먼저 곱하면 힌지 범위밖→극성 깨짐).
- Cue B 클립공간 delta 에 `_Persp` 재곱 금지((p-orig)이 이미 스케일).
- rest no-op 불변식: 모든 큐 `_Tilt` 게이트, `_Time` sheen 금지, 최종 UV 클램프 금지.
- 틸트 스프링은 플레이어 소유 + staleness watchdog → 드래그 중간 종료해도 컷신 독립 완주 + 틸트 자동 0.
- per-instance 머티리얼(런타임 스왑 금지, Set* 로만 구동), `OnDestroy` Dispose.
- 모듈은 소비처 타입 무참조(단방향). DA-V2 Base/Large/Giant·Depth Pro(CC-BY-NC/ASCL) 사용 금지.

## Follow-up (남은 확인/후보)

- **u9 사용자**: 실제 드래그 스와이프 체감(틸트 방향/스프링/중간 드롭 완주), Android 실기기 프레임/발열.
- **라이브 튜닝 활성화(선택)**: `DefenderSelector` 의 `depthParallaxSettings` 필드에 `.asset` 할당
  (씬 저장 = WIP 베이크 함정 [[feedback_scene_save_bakes_wip]] 주의 — 스냅샷 격리 권장).
- **드림캐쳐 카드 적용**: `DepthParallaxView` 배선(README 후속 후보).
- **뎁스 품질 핸드터치(선택)**: 셀아트 약점(뜬 소품/무기) 페인트오버 — 현재 자동 bake 로 충분해 보이나 실기기 확인 후 판단.
