# 0 · 모션 세트 데이터 + 슬라이서 피벗

## 목적

유닛 하나의 스프라이트 모션 6개를 담는 SO 와, 빈 슬롯의 폴백을 정하는 순수 함수를 만든다.
유닛 SO 에는 그 세트를 가리키는 **필드 하나**만 붙인다. 소비자는 아직 0 — 이 단위만으로는
라이브 동작이 바뀌지 않는다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Data/UnitSpriteMotionSet.cs`
- 신규 `Assets/_Project/Tests/EditMode/UnitSpriteMotionSetTests.cs`
- 수정 `Data/ISpineUnitVisualData.cs` — `UnitSpriteMotionSet SpriteMotions { get; }` 1개 추가
- 수정 `Data/DefenderUnitData.cs` · `Data/AttackUnitData.cs` — 필드 `spriteMotions` **맨 뒤** 추가 + getter
- 수정 `Editor/UnitVisualDataValidator.cs` — 세트 + 파츠/슬롯틴트 동시 저작 경고
- 수정 `Editor/SpriteFlipbookDataEditor.cs` — `SliceAndFill` 에 피벗 인자(`SpriteAlignment`) + 인스펙터 팝업
- 수정 `docs/spec/sprite-flipbook-player/5_grid_slice_authoring.md` — 피벗 인자 한 줄(그 문서가 슬라이서의 정본)

## 구현

`UnitSpriteMotionSet : ScriptableObject` — `[SerializeField] SpriteFlipbookData idle, walk, attack, death, deploy, drag;`
`[SerializeField] bool sheetFacesRight;` (시트가 오른쪽을 보고 그려졌으면 체크. 오늘 `roy_*` 시트가 그렇다.)
getter 는 읽기 전용 프로퍼티. 배열/리스트를 노출하지 않는다.

**폴백은 세트 자신의 순수 메서드**로 둔다(입력 = 직렬화 참조, 출력 = 참조 하나 — 아키텍처 타입 무관):

| 메서드 | 규칙 | Spine 대응 |
|---|---|---|
| `ResolveLocomotion(bool moving)` | `moving && walk != null ? walk : idle` | `ResolveLocomotionAnimation` — walk 빈 문자열이면 idle 단일 루프 |
| `ResolveDeploy()` | `deploy ?? drag ?? attack ?? idle` | `PlayDeploy` 후보 순서 |
| `ResolveDrag()` | `drag ?? idle` | 드래그 프리뷰 `ResolveAnimation(drag, idle, attack)` |
| `Death` | `death` 그대로 — null 이면 호출측이 즉시 파괴 | `Kill` |
| `HasIdle` | `idle != null` — **false 면 세트 무효**(`TrySpawn` 이 false + 경고) | 필수 슬롯 |

`OnValidate` — 루프 정책 위반 경고(`FlipbookCharacterView.WarnIfLoopPolicyViolated` 와 같은 정신):
idle·walk·drag 는 `Loop` 여야, attack·death·deploy 는 아니어야 한다. 에셋 이름을 지목한다.

유닛 SO 두 곳 — `[Header("Sprite Backend (임시)")] public UnitSpriteMotionSet spriteMotions;` 맨 뒤,
getter `SpriteMotions => spriteMotions`. 직렬화 순서 보존(기존 에셋은 null).

검증기 — `CollectWarnings` 첫머리에서 `data.SpriteMotions != null && (hasParts || hasColors)` 이면
「스프라이트 세트가 있어 partSkins/slotColors 는 무시된다」 경고를 추가하고 **계속 진행**(기존 검증은 그대로).

슬라이서 — `SliceAndFill(data, sheet, columns, rows, SpriteAlignment alignment)`. `rect.alignment = alignment`,
`rect.pivot` 은 Center → (0.5,0.5) · BottomCenter → (0.5,0). 인스펙터에 `EnumPopup("피벗")`, 기본 **Center**
(확인용 프리팹 무회귀). 지원 값은 두 개뿐 — 다른 정렬은 유닛에 쓸 일이 없다.

## 완료 기준

- EditMode: `UnitSpriteMotionSetTests` — 폴백 4규칙 + `HasIdle` (6건 내외). 기존 총계에서 그만큼 는다.
- 컴파일 0 에러. 기존 `Defender_*`/`Enemy_*` 에셋 diff 0 (필드 맨 뒤 + 기본 null).
- 슬라이서 팝업에서 BottomCenter 로 `roy_idle` 를 재슬라이스하면 `Sprite.pivot.y == 0` 이고
  `Flipbook_roy_idle` 의 프레임 참조가 Missing 이 되지 않는다(GUID 보존).
- 검증기: 세트 + partSkins 동시 저작 시 경고 1줄, 세트만 있으면 경고 0.

---

2026-09-15 구현 · `162b3643` — EditMode 6건 초록 · 컴파일 0 에러 · 사용자 Play 확인 대기.
