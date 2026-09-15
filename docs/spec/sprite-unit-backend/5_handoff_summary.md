# 5 · Handoff Summary

## Commit

| 해시 | 제목 |
|---|---|
| `162b3643` | feat(sprite-unit-backend): unit 0 — UnitSpriteMotionSet SO + 유닛 SO 필드 1개 + 슬라이서 피벗 인자 |
| `05b630ef` | refactor(sprite-unit-backend): unit 1 — UnitView 추상 베이스 추출 + 풀·시그니처 전환 (라이브 무변) |
| `46f6e11f` | feat(sprite-unit-backend): unit 2a — SpriteUnitView 골격 + TrySpawn 백엔드 선택 |
| `9bc5e83b` | feat(sprite-unit-backend): unit 2b — SpriteUnitView 반응 |
| `ca769aba` | feat(sprite-unit-backend): unit 3 — 드래그 그림을 스프라이트로 |
| `bcfd2a60` | unit 4 — 시트 3장 발 피벗 재슬라이스 + `MotionSet_roy` + PlayMode 테스트 + 파이프라인 맵 |

## Implemented

- 유닛 SO 필드 하나(`spriteMotions`)로 백엔드 opt-in. 비면 Spine 경로 **무변경**. 되돌리기 = 필드 비우기.
- `UnitView : MonoBehaviour` 선언만 있는 추상 베이스(23 멤버). `SpineUnitPool` 값 타입이 이것.
  인터페이스가 아닌 이유 = Unity fake-null(critic C-1) — `!= null` 25곳이 그대로 살아야 했다.
- `SpriteUnitView` — 로코모션(walk↔idle 히스테리시스·걷기 배율) · 공격 압축(`Player.Speed = max(1, duration/period)`) ·
  배치 폴백 체인 · death 원샷 → 폴링 파괴 · 틴트 4축(`SpriteRenderer.color`) · 펀치/스쿼시/hop · 빌보드·블롭·궤적(구조물 경로) ·
  스크린 렉트(픽킹) · 정적 앵커(`FacingRight` 로 x 반전).
- facing 판정은 `UnitFacing.ShouldFlip`(순수) 하나 — Spine 도 이걸 호출한다. 시트 방향은 세트 `sheetFacesRight` 로 정규화.
- 백엔드 선택은 `SpineUnitPool.TrySpawn` 한 곳. 세트 있으나 idle 비면 경고 + 세트 무시.
- 드래그 그림(보드 실루엣·손끝 키링) 스프라이트 분기. 취소 알파는 `DragSession.flipbook`.
- 슬라이서 피벗 Center/BottomCenter. `roy_*` 3장은 BottomCenter 로 재슬라이스(GUID 보존).

## Key Files

- `Assets/_Project/Scripts/Data/UnitSpriteMotionSet.cs` · `Data/ISpineUnitVisualData.cs`(getter 1)
- `Assets/_Project/Scripts/Presentation/UnitView.cs` · `UnitFacing.cs` · `SpriteUnitView.cs` · `SpineUnitPool.cs`(TrySpawn)
- `Assets/_Project/Scripts/Presentation/SpriteFlipbookPlayer.cs` — `Speed`/`TimeDomain`/`Current`(유일한 재생기 확장)
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — `TryBuild*Sprite` 2개
- `Assets/_Project/Editor/SpriteFlipbookDataEditor.cs` — 피벗 인자
- `Assets/_Project/Data/Flipbook/MotionSet_roy.asset` + `Flipbook_roy_{idle,attack,drag}` · `Sprites/Unit/roy_*.png`(배경 제거본 · 원본은 `Sprites/Unit/Raw/`)
- 테스트: `Tests/EditMode/UnitSpriteMotionSetTests.cs`(6) · `UnitFacingTests.cs`(4) · `Tests/PlayMode/SpriteUnitBackendPlayTest.cs`(1 · 합성 세트)

## Verified

- EditMode 코어 lane **2697 · 실패 0 · 스킵 3(기지)** — 신규 10건 포함. 컴파일 0 에러(라이브 에디터).
- unit 1 의 「라이브 무변」: PlayMode 테스트 수정 0 (`CurrentAnimationName`·`gameObject` 가 베이스/MonoBehaviour).
- **미실행**: PlayMode lane(`SpriteUnitBackendPlayTest` 포함, ~8분·에디터 포커스 필요) · 사용자 Play 육안(대상 유닛 미정).
- 프리뷰 GO(`MapTest` 씬, 미저장)에서 attack 원샷 → idle 복귀 · drag 루프는 `Tick` 으로 확인.

## Notes — 되돌리면 안 되는 것

- **`UnitView` 는 반드시 `MonoBehaviour` 파생.** 인터페이스로 바꾸면 컴파일은 되고 파괴된 뷰가 풀 생존 판정을 통과한다
  (`SpriteUnitBackendPlayTest` 마지막 단언이 그 회귀 가드).
- **`SpineUnitView` 본문은 두 곳만 바뀌었다** — 상속/override · `UnitFacing` 호출. 그 외 diff 가 생기면 이 spec 밖이다.
- **원샷 폴백이 루프 시트로 떨어지면 원샷 취급하지 않는다** (`PlayDeploy` 의 `deploy.Loop` 분기 · `Kill` 의 `death.Loop`).
  안 그러면 `_oneShot` 폴링이 영영 안 끝나 유닛이 갇힌다(프리뷰 뷰가 같은 함정을 기록했다).
- **`UpdateWalkTimeScale` 의 `WalkAnimSpeedEnabled` 게이트는 Spine 과 같다** — 스타일 SO 미할당이면 `_moving` 이 영영 false 라
  walk 시트가 안 돈다. 이것은 현행 Spine 동작이지 스프라이트 결함이 아니다.
- **피벗은 발(BottomCenter)** — 뷰에서 오프셋 보정 금지. 중앙 피벗으로 잘린 옛 시트를 유닛에 꽂으면 반칸 뜬다.
- **PPU 128 고정**(2026-09-15 확정 — 프랍 256 의 절반). 크기 차이는 `spineVisualScale` 만.

## Follow-up

- ~~대상 유닛 결정~~ → **이쑤시개(`Defender_Slasher`)에 저작됨** `ea6da528`. Play 실측: 스폰·픽킹·공격 압축(Speed 2.22)·반전·반응·드래그 실루엣 전부 통과, 콘솔 0.
  (`spriteMotions` 는 시트 컬럼이 없어 임포트에 안 덮인다.)
- **PlayMode lane 1회 실행** + 사용자 Play 확인(배치 모션·공격 압축·픽킹·펀치·사망·퇴근 비행).
- `good` 에 walk/death/deploy 시트가 없다 — 지금은 폴백(death 즉시 파괴·deploy→drag).
- README 후속 후보 6건(풀·인터페이스 개명, idleVariants, 소환 오버라이드, 파츠 대안, Spine 은퇴).
