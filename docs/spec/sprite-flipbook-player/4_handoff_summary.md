# 4 · Handoff Summary

## Commit

| 해시 | 제목 |
|---|---|
| `11932992` | feat(sprite-flipbook-player): 프레임 선택 순수 함수 + 재생 데이터 SO (units 0~1) |
| `ff9ef18e` | feat(sprite-flipbook-player): SpriteRenderer 재생 컴포넌트 (unit 2) |
| `b54b9649` | feat(sprite-flipbook-player): 통 시트 오소링 (unit 3) |

관련(별건, 같은 세션): `660acc6d` fix(lobby-reaction) — 리액션 전역 락 영구 점유 차단.
이 spec 과 무관한 기존 버그 수정이라 커밋을 분리했다.

## Implemented

- 월드 `SpriteRenderer` 대상 재사용 플립북 재생기. 기존 로비(`Animator`)·배치 컷씬
  (`DeployCutscenePlayer`)은 **건드리지 않았다** — 새 독립 기능이다.
- `FlipbookMath` — 경과시간 → 프레임 인덱스 순수 함수. 원샷 hold / 루프 되감기 / 비유한 입력 차단.
- `SpriteFlipbookData` — 프레임·fps·루프 SO. 배열 대신 `FrameAt(i)`/`FrameCount` 만 노출.
- `SpriteFlipbookPlayer` — `TimeManager` 도메인 클럭으로 재생. `Play`/`Stop`/`Tick`/`IsPlaying`/`IsLooping`.
- `FlipbookFrameOrder` + 에디터 유틸 — 통 시트 서브스프라이트를 숫자 순으로 프레임 배열에 주입.
- 소스 2모드(컷/통)는 **오소링 경로의 차이일 뿐**이고 런타임 표면은 단일하다.

## Key Files

- `Assets/_Project/Scripts/Presentation/FlipbookMath.cs`
- `Assets/_Project/Scripts/Presentation/FlipbookFrameOrder.cs`
- `Assets/_Project/Scripts/Presentation/SpriteFlipbookPlayer.cs`
- `Assets/_Project/Scripts/Data/SpriteFlipbookData.cs`
- `Assets/_Project/Editor/SpriteFlipbookDataEditor.cs`
- `Assets/_Project/Tests/EditMode/{FlipbookMath,FlipbookFrameOrder,SpriteFlipbookPlayer}Tests.cs`

## Verified

- EditMode 전체 **1064건 중 1062 통과 · 0 실패 · 2 스킵** (신규 46건).
- 컴파일 0 에러 — 격리 리그(클린 체크아웃)와 실제 작업 에디터 양쪽.
- 슬로우모: 클럭 소스 스케일링·도메인 격리 실측(lease 0.25 → ScaleOf(Battle)=0.25, Interaction 1 유지,
  Time.timeScale=1 고정). 재생기 Update 의 dt 소비 경로는 코드 검토만 — Play 모드 실측 아님.
- 오프스크린 렌더로 Archer 컷씬 프레임 재생 확인: `001→005→010→015→020→024`,
  완주 시 `IsPlaying=false` + 마지막 프레임 유지.
- 4×3=12 프레임 시트를 실제 임포터로 슬라이스해 오소링 e2e 확인 — 주입 순서 `_0 … _11`.
- **Mutation 검증**: 완료 판정을 프레임 반영 앞으로 옮기면
  `OneShot_SingleTickPastEnd_StillRendersLastFrame` 만 실패한다(다른 12건은 통과).

## Notes — 되돌리면 안 되는 것

- **`float.IsFinite` 가드(elapsed·fps 둘 다).** `FloorToInt` 부호에 기대면 안 된다.
  float→int 캐스트가 x64 는 `int.MinValue`, **ARM64 는 saturate** 라 `+Inf` 가
  `int.MaxValue` 로 떨어져 음수 가드를 통과하고 루프가 임의 프레임에 영구 고착된다.
  타겟(Apple Silicon 에디터 + Android ARM64)에서만 터지므로 x64 에서 재현 안 된다.
- **"프레임 반영 → 완료 판정" 순서.** 뒤집으면 마지막 프레임이 한 번도 안 그려진다.
  잘게 tick 하는 테스트로는 판별되지 않는다 — 한 번의 tick 이 마지막 프레임 진입과
  완주를 동시에 건너뛰는 케이스가 이 계약을 지키는 유일한 지점이다.
- **`Duration` 의 NaN 차단.** 새면 `elapsed >= NaN` 이 영원히 false 라 원샷이 완주하지 못한다.
- **`IsLooping`.** 루프는 `IsPlaying` 이 영원히 참이라, 없으면 폴링 소비자가 영구 대기하며 샌다.
- **렌더러 `enabled` 는 플래그가 켜졌을 때만 재생기 소유.** `Play`/`Stop`/완주 세 경로가
  `SetRendererEnabled` 하나를 공유한다. `Stop` 을 빼면 취소된 원샷의 중간 프레임이 멈춰 남는다.
- **`FlipbookFrameOrder` 의 런타임 어셈블리 배치.** 에디터 전용 로직이지만 테스트 어셈블리가
  닿는 위치가 거기뿐이다. 에디터로 되돌리면 정렬 회귀 테스트가 사라진다.
- **`SaveAssetIfDirty`.** `SaveAssets()` 로 되돌리면 버튼 한 번이 무관한 dirty 에셋을 디스크에 박는다.
- **`SpriteFlipbookData` 의 `frames` 필드명.** 오소링 유틸이 `SerializedObject` 문자열로 접근한다.

## Follow-up

- **첫 실사용 소비자 없음.** 재생기만 있고 씬 배선은 아직 없다 — 첫 소비자가 생길 때
  `docs/reference/object-pipeline-map.md` 갱신 여부를 판단한다(README 파이프라인 커버리지 참조).
- `DeployCutscenePlayer` 를 이 재생기 위로 재작성(연출은 유지, 프레임 진행만 위임).
- UI `Image` 타겟 — 두 번째 구현체가 실제로 필요해질 때 렌더 타겟 추출.
- 로비 `Animator` 대체 — 리액션 길이 이중 진실과 상태 매직 스트링이 사라지지만 회귀 검증이 크다.
- 재생 완료/특정 프레임 콜백 — 첫 소비자가 실제로 요구할 때. 미리 만들지 않는다.
- 별건 확인: `DeployCutscenePlayer` 의 캔버스에 `CanvasScaler` 가 없어 컷씬 크기·여백이
  해상도 종속이다(이번 리뷰에서 발견, 이 spec 범위 밖).
