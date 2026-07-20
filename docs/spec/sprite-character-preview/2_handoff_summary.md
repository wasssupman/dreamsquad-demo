# 2 · Handoff Summary

## Commit

| 해시 | 제목 |
|---|---|
| `6af72181` | feat(sprite-character-preview): 상태별 플립북 재생 컴포넌트 (unit 0) |
| `4fb2c020` | feat(sprite-character-preview): 상태 전환 인스펙터 + 템플릿 프리팹 (unit 1) |
| `34c4deae` | chore(sprite-character-preview): 확인용 테스트 시트 오소링 (idle 49프레임) |

## Implemented

- `FlipbookCharacterView` — 상태 5개(`Idle`/`Attack`/`Death`/`Deploy`/`Drag`)를 플립북 5개에 매핑.
- 원샷(`Attack`/`Deploy`) 완주 시 `Idle` 자동 복귀. `Death` 는 마지막 프레임 유지 + 파괴 없음.
- 선택 슬롯(`Deploy`/`Drag`)이 비면 데이터와 **상태를 함께** `Idle` 로 접는다.
- 루프 정책 위반(원샷 상태에 루프 시트) 감지 — `OnValidate` + 런타임 `Play` 2겹.
- Play 전용 인스펙터 상태 버튼 5개 + 현재 상태/`IsPlaying` 표시.
- 템플릿 프리팹 `SpriteCharacter` + 테스트 배리언트 `SpriteCharacter_Test`(idle 배선됨).
- 테스트 시트 `idle.png` 를 7×7=49프레임으로 슬라이스, `Flipbook_idle`(fps 24 · loop) 생성.

## Key Files

- `Assets/_Project/Scripts/Presentation/FlipbookCharacterView.cs`
- `Assets/_Project/Editor/FlipbookCharacterViewEditor.cs`
- `Assets/_Project/Prefabs/Characters/SpriteCharacter.prefab` (템플릿) · `SpriteCharacter_Test.prefab`
- `Assets/_Project/Data/Flipbook/Test/Flipbook_idle.asset`
- `Assets/_Project/Tests/EditMode/FlipbookCharacterViewTests.cs`

## Verified

- EditMode **1078건 중 1076 통과 · 0 실패 · 2 스킵**(기존 스킵 그대로). 신규 14건. 격리 리그 배치 실행.
- 프리팹 YAML 의 컴포넌트 GUID 3개 + 직렬화 값 대조.
- 오프스크린 렌더로 `idle` 프레임 전진(`idle_0 → 4 → … → 44`)과 발 높이 일정 확인.
- **미확인**: Play 중 버튼 동작, 맵 위 빌보드 틸트 육안, 프리팹 스케일 적정성.
  MCP 브리지가 세션 내내 끊겨 있어(`Unable to connect`) Play 모드 자율 검증 불가.

## Notes — 되돌리면 안 되는 것

- **`ReturnsToIdle` 은 `!ShouldLoop` 가 아니다.** `Death` 는 원샷이면서 복귀하지 않는
  유일한 상태다. 두 술어를 합치면 사망 캐릭터가 되살아난다.
- **폴백은 상태까지 접는다.** 데이터만 접고 `_current` 를 `Deploy` 로 남기면,
  루프하는 idle 이 돌아 `IsPlaying` 이 영원히 참 → 복귀가 영영 안 일어나고 상태가 갇힌다.
  **화면에는 idle 이 정상 재생돼 보여서 증상이 드러나지 않는다.**
- **재생기의 자가 tick 을 뷰가 가져오지 말 것.** 둘 다 tick 하면 프레임이 두 배로 진행한다.
  전이가 최대 1프레임 늦는 것은 의도된 대가다.
- **인스펙터 버튼의 Play 전용 게이트.** 에디트 모드에서 `Play(data)` 가 직렬화 필드에 써서
  씬/프리팹을 dirty 하게 만든다.
- **`disableRendererWhenFinished = false`.** 켜면 `Death` 완주 시 렌더러가 꺼져 마지막 프레임이 사라진다.
- **`playOnEnable = false`** (재생기). 재생 시작은 뷰의 `playIdleOnEnable` 이 소유한다.

## 시트 오소링 함정 (실측)

- **첫 임포트 직후 `LoadAllAssetRepresentationsAtPath` 는 덜 정착한 상태를 돌려준다.**
  49개 중 42개만 반환했고 meta 에는 49개가 다 있었다. 개수를 기대값과 대조하지 않으면
  프레임 몇 개가 조용히 빠진 채 애니메이션만 이상해진다. 한 번 더 실행하면 정상.
- **`com.unity.2d.sprite` 패키지가 이 프로젝트에 없다.** `UnityEditor.U2D.Sprites`
  (`SpriteDataProvider`) 를 못 쓴다 — 레거시 `TextureImporter.spritesheet` 경로를 써야 한다.
- **1080 ÷ 7 = 154.2857 로 정수가 아니다.** 캐릭터가 셀을 꽉 채워(깨끗한 셀의 머리 여백 1~3px)
  그려져 있어 프레임 상단에 윗 칸 신발이 1~5px 비친다. 알파 실측으로 경계를 최적화해도
  전 열이 비는 y 가 없고, bleed(최대 5px) > 머리 여백(최소 1px) 이라 **안전한 inset 이 없다.**
  슬라이스로 해결 불가 — 근본 해결은 **7의 배수 크기(1918×1078)로 내보내기** 또는 셀 간 투명 여백.
  확인용이므로 그대로 두기로 결정(2026-07-20 사용자).

## Follow-up

- **`attack.png` / `death.png` 재내보내기 필요** — 현재 파일은 `IEND` 청크가 없는 잘린 PNG 라
  임포트 자체가 안 된다. 커밋에서 제외했다. 받으면 `TempSliceTestSheets` 와 같은 절차로 슬라이스.
- **Play 육안 확인** — 맵 위 빌보드 틸트, 버튼 동작, 스케일(현재 PPU 256 · 프리팹 scale 1).
- **`idle.png` 내용 확인** — 총구 화염과 빔이 나가는 사격 동작이라 `idle` 이 맞는지 미확인.
- 전투 연동은 이 spec 범위 밖. 사전 조사 결과가 README 후속 후보에 정리돼 있다
  (`IUnitView` 추출 경로, 재현 불가한 Spine 의존 3가지, 페이싱 부호 역전 등).
