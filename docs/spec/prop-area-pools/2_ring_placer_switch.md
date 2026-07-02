# 2 — 원경 placer 를 distantRingProps 로 이관

## 목적

원경 링 배치를 `tileProps` + `excludeFromDistantRing` 필터 → `distantRingProps` 직접 순회로 바꾼다. weight 는 `WeightedProp.weight`. 이 단위 완료 시 근경/원경이 완전히 독립된 두 리스트로 구동된다 → 검증 질문 해소.

## 변경 대상

- `Assets/_Project/Scripts/Core/TilemapMapView.cs` (`InstantiateRingProps`, `RingWeight`)

## 구현

### InstantiateRingProps (`:403`)

- null 가드(`:406`) `theme.tileProps` → `theme.distantRingProps`.
- totalW 누적(`:413~417`): `theme.distantRingProps` 순회. 항목 = `WeightedProp`.
  - `entry == null || entry.prop == null || entry.prop.prefab == null` 이면 skip.
  - `excludeFromDistantRing` 필터 제거 (리스트 소속 자체가 opt-in).
  - `totalW += Mathf.Max(0f, entry.weight)` (`RingWeight` 헬퍼 대체).
- 룰렛 선택 루프(`:441~447`): `theme.distantRingProps` 순회. `roll -= Mathf.Max(0f, entry.weight)`, prop = `entry.prop`.

### RingWeight 헬퍼

`:210~212` 는 이 단위에서 **호출부가 사라지므로** 제거해도 되지만, `distantRingWeight` 필드 제거는 unit 3 소관이다. 이 단위에서는 `RingWeight` 호출을 없애고 헬퍼는 unit 3 에서 필드와 함께 삭제 (컴파일 안전). `RingDistance`(falloff)는 유지.

## 완료 기준

- compile 성공.
- Play→게임뷰 스크린샷 검증(에디터 포커스 필요):
  - `playAreaProps` 에만 있는 프랍(예: mushroom)이 **원경에 안 나타남**.
  - `distantRingProps` 에만 있는 프랍이 **플레이 영역에 안 나타남**.
  - 전체 배경 자연스러움 육안 확인 (memory: `feedback_background_screenshot_verify`).
- 스크린샷은 `Assets/Screenshots/` 에 내가 만든 파일명으로 저장 (통삭제 금지 폴더 — memory: `project_screenshots_scratch_folder`).
