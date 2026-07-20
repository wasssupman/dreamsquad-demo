# 3 · 통 시트 오소링 (임포트 시 슬라이스)

## 목적

통 스프라이트 시트 1장에서 프레임 배열을 채우는 **에디터 경로**를 만든다.
확정된 결정(안 B)에 따라 슬라이스는 Unity 임포터가 하고, 이 유틸은 나온 서브스프라이트를
올바른 **순서로** SO 에 주입하는 일만 한다. 런타임에는 아무 것도 추가되지 않는다.

## 변경 대상

- 신규 `Assets/_Project/Editor/SpriteFlipbookDataEditor.cs`
- 신규 `Assets/_Project/Scripts/Presentation/FlipbookFrameOrder.cs` (정렬 순수 함수)
- 신규 `Assets/_Project/Tests/EditMode/FlipbookFrameOrderTests.cs`

## 구현

`[CustomEditor(typeof(SpriteFlipbookData))]`, namespace `Wassup.Editor`.
기본 인스펙터 아래에 "통 시트에서 채우기" 블록(텍스처 슬롯 + 버튼)을 붙인다.

동작:

1. 시트 텍스처의 `TextureImporter.spriteImportMode` 를 확인. `Multiple` 이 아니면 중단하고 사유를 알린다.
2. `AssetDatabase.LoadAllAssetRepresentationsAtPath` 로 서브스프라이트만 수집(텍스처 본체 제외).
3. **이름 끝 숫자 기준으로 정렬**한 뒤 `frames` 에 주입.
4. `SerializedObject` 로 쓰고 `SetDirty` + `SaveAssetIfDirty(data)` (아래 "저장 범위" 참조).

### 정렬이 이 유닛의 핵심

Unity 슬라이서는 `{텍스처명}_0`, `_1` … `_10` 으로 이름을 매긴다. 사전순 정렬은
`_1, _10, _11, _2 …` 가 되어 **프레임 순서가 조용히 뒤섞인다** — 컴파일도 통과하고 경고도 없이
애니메이션만 이상해지는 종류라 여기서 확정한다. 숫자 접미사를 정수로 파싱해 비교하고,
접미사가 없는 이름은 뒤로 보낸 뒤 ordinal 비교로 안정화한다.

이 로직은 `FlipbookFrameOrder` **순수 함수로 분리해 런타임 어셈블리에 둔다.** 에디터 전용 로직이지만
`Wassup.Tests.EditMode` 가 참조할 수 있는 위치가 거기뿐이고, spec 이 회귀 방지를 완료 기준으로
못박은 이상 테스트 불가능한 자리에 두면 그 기준을 지킬 수 없다 — **테스트 가능성이 배치를 결정했다.**
(초안은 Editor 클래스 private static 이었고, 2026-07-20 리뷰가 "핵심이라면서 테스트 0개"를 지적했다.)

### 저장 범위

`AssetDatabase.SaveAssets()` 를 쓰면 안 된다 — 버튼 한 번이 인스펙터에서 편집 중이던 **무관한 dirty
에셋 전부**를 디스크로 밀어낸다(씬 저장이 미저장 WIP 를 베이크하는 것과 같은 계열). `SaveAssetIfDirty(data)`
로 이 SO 만 저장한다. (2026-07-20 리뷰 적발)

`frames` 는 private 직렬화 필드라 `SerializedObject.FindProperty("frames")` 문자열로 접근한다.
필드명을 바꾸면 이 유틸이 조용히 깨지므로 unit 1 의 필드명은 계약으로 취급한다.

## 완료 기준

- 슬라이스된 시트를 지정하고 버튼을 누르면 `frames` 가 슬라이스 개수만큼 순서대로 채워진다.
- 10프레임 이상(두 자리 인덱스) 시트에서 순서가 어긋나지 않는다 — **`FlipbookFrameOrderTests` 로 고정.**
  최소 커버: 두 자리 진입 경계 · 0 패딩(`_001` 형식) · 접미사 없는 이름 · null/빈 이름 · 자릿수 오버플로.
- 버튼을 눌러도 대상 SO 외의 dirty 에셋이 디스크에 저장되지 않는다.
- `Sprite Mode = Single` 텍스처를 지정하면 배열을 건드리지 않고 사유를 알린다.
- 컷 모드(개별 스프라이트 수동 할당)는 이 유틸 없이 기본 인스펙터로 그대로 가능하다.
