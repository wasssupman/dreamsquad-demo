# 5 · NxM 격자 자동 슬라이스

## 목적

통 시트 PNG 1장을 **균일 격자(가로 N × 세로 M)** 로 자르는 단계를 오소링 유틸 안으로 들인다.

unit 3 이 만든 「통 시트에서 채우기」 는 *이미 잘린* 시트만 받는다. 즉 지금 워크플로에는
「Sprite Editor 를 열고 Grid By Cell Count 로 자른다」 는 손작업이 한 칸 끼어 있고,
그 칸이 이 spec 에서 유일하게 자동화되지 않은 곳이다. 이 유닛이 그 칸을 없앤다.

**슬라이스 시점은 바뀌지 않는다** — 여전히 임포트 시점이고(확정된 결정 안 B), 런타임은
`Sprite.Create` 를 하지 않는다. 자동화되는 것은 「누가 격자를 입력하는가」뿐이다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Presentation/FlipbookSheetGrid.cs` (셀 사각형 순수 함수)
- 신규 `Assets/_Project/Tests/EditMode/FlipbookSheetGridTests.cs`
- 수정 `Assets/_Project/Editor/SpriteFlipbookDataEditor.cs` (격자 입력 + 「자르고 채우기」 버튼)

## 구현

인스펙터의 「통 시트에서 채우기」 블록에 **가로 칸 수 N · 세로 칸 수 M** 입력과
「N×M 로 자르고 채우기」 버튼을 더한다. 기존 「프레임 채우기」 버튼(이미 잘린 시트용)은 그대로 둔다.

버튼 동작:

1. `SpriteDataProviderFactories` → `ISpriteEditorDataProvider` → `ITextureDataProvider` 로
   **원본 해상도**를 읽는다(읽기 전용 — 아직 임포터를 건드리지 않는다).
2. `FlipbookSheetGrid.TryCellSize` 로 셀 크기를 구한다. **나누어떨어지지 않으면 중단**한다.
3. `SpriteRect` N×M 개를 만든다. 이름은 Unity 슬라이서와 같은 `{텍스처명}_{i}`, 피벗 중앙.
4. 여기서부터 쓰기 — `textureType = Sprite` · `spriteImportMode = Multiple` 을 먼저 확정하고
   프로바이더를 **다시 열어** `SetSpriteRects` → `Apply` → `ImportAsset(ForceUpdate)`.
5. 이어서 unit 3 의 `FillFromSheet` 를 그대로 호출해 `frames` 를 채운다.

실패 판정이 전부 쓰기 앞에 오는 이유: 중간에 끊기면 임포터만 바뀌고 `frames` 는 옛 프레임을
가리킨 채 남는다. 프로바이더를 다시 여는 이유: 프로바이더는 `Init` 시점의 직렬화 상태를 들고
있어서, 연 뒤에 바꾼 모드가 `Apply` 에 되덮일 여지가 있다.

### 이 유닛의 함정 3개

- **좌표계 뒤집힘.** 사람은 시트를 왼쪽 «위»부터 읽는데 텍스처 원점은 왼쪽 «아래»다. 이 변환이
  틀리면 행 단위로 뒤집힌 채 각 행 안에서는 순서가 맞아 **애니메이션이 대충 돌긴 하는** 형태로
  조용히 어긋난다. 그래서 순수 함수로 빼서 테스트한다(unit 3 의 정렬과 같은 이유).
- **원본 해상도 ≠ `Texture2D.width`.** `maxTextureSize` 로 축소 임포트된 시트에서 임포트된 크기로
  셀을 계산하면 rect 가 전부 절반이 된다. 슬라이스 좌표는 원본 기준이므로
  `ITextureDataProvider.GetTextureActualWidthAndHeight` 를 쓴다 — 폴백하지 않고, 못 얻으면 중단한다.
- **나머지를 내림으로 삼키지 않는다.** `1024 / 5` 를 진행시키면 마지막 열에 잘린 띠가 남고 그 띠가
  프레임으로 섞여 들어간다. 나누어떨어짐은 실패 조건이지 경고가 아니다.

`textureType` 까지 건드리는 이유: 이 프로젝트는 3D/URP 라 새로 넣은 PNG 가 `Default` 로 임포트된다.
그 상태로 `Multiple` 만 켜면 rect 는 들어가는데 **서브스프라이트 에셋이 생성되지 않아**
「왜 프레임이 0개냐」로 보인다.

### 다시 자를 때

이름이 같은 기존 `SpriteRect` 의 `spriteID`(GUID)를 물려준다. 새로 뽑으면 그 서브스프라이트를
참조하던 곳(다른 `frames` 배열·프리팹)이 전부 Missing 이 된다. 격자를 줄여 **사라진** 프레임의
참조는 물려줄 대상이 없어 끊긴다 — 이건 데이터가 실제로 없어진 것이라 정상이다.

## 완료 기준

- N×M 을 입력하고 버튼을 누르면 `Sprite Mode = Single` 인 PNG 도 잘리고 `frames` 가 N×M 개로 채워진다.
- 프레임 순서가 **왼→오, 위→아래** 다 — `FlipbookSheetGridTests` 로 고정(행 뒤집힘·두 자리 인덱스).
- 나누어떨어지지 않는 입력(예: 1024×1024 를 5×4)은 배열도 임포터도 건드리지 않고 사유를 알린다.
- 같은 격자로 다시 잘라도 기존 프레임 참조가 Missing 이 되지 않는다.
- 컷 모드(개별 스프라이트 수동 할당)와 unit 3 의 「프레임 채우기」 경로는 그대로 동작한다.

---

2026-09-11 확인 — EditMode 2805건(신규 11건, 실패는 기록된 선행 2건뿐) · 행 뒤집기 mutation 3건 빨감 ·
라이브 에디터 e2e: `Default/None` 생 PNG → `Sprite/Multiple` 자동 전환 · 4×3 프로브 12프레임 순서 일치 ·
재슬라이스 후 localFileId 12개 동일(참조 보존) · 실사용 시트(`bucy_alpha.png` 6×6 = 36프레임) 재생 확인.
