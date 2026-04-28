# 0. PrimeTween asmdef 참조 + API smoke

## 목적

PrimeTween 은 이미 UPM 로컬 tgz (`Packages/manifest.json` 의 `com.kyrylokuzyk.primetween`, 실파일 `Assets/Plugins/PrimeTween/internal/com.kyrylokuzyk.primetween.tgz`) 로 설치돼 있다. 본 task 는 별도 임포트가 아니라 (1) 런타임 asmdef 가 PrimeTween 을 참조하는지 확인 + 추가, (2) 후속 task 4/5/7 에서 사용할 PrimeTween API 들을 한 번에 smoke 검증해 placeholder 가 아닌 확정된 API 시그니처를 spec 에 박는 것이다.

## 변경 대상

- 검증/수정: `Assets/_Project/Scripts/Wassup.Runtime.asmdef` (이 프로젝트의 유일한 런타임 asmdef. `Wassup.UI.asmdef` 는 존재하지 않음.)
- 신규(검증 후 즉시 삭제): `Assets/_Project/Scripts/UI/Draft/_PrimeTweenSmoke.cs`
- (필요 시) `Assets/_Project/Tests/EditMode/Wassup.Tests.EditMode.asmdef` 도 PrimeTween 참조 추가 — 단위 테스트가 PrimeTween 호출을 검증하지 않는다면 생략.

## 구현

1. PrimeTween 의 asmdef 이름 / GUID 확인:
   - Project 창에서 `Assets/Plugins/PrimeTween/Runtime/` 또는 패키지 트리 (`Packages/PrimeTween/Runtime/`) 의 asmdef 파일을 열어 `name` 필드 확인 (`PrimeTween.Runtime` 으로 추정).
2. `Wassup.Runtime.asmdef` 의 `references` 배열에 위 이름 (또는 GUID) 추가. 저장 후 Unity 가 자동 재컴파일.
3. `_PrimeTweenSmoke.cs` 작성. 후속 task 가 사용할 모든 API 를 1회씩 호출해 컴파일/런타임 검증:
   - `Tween.LocalPositionY(transform, 1f, 0.5f)` — 위치
   - `Tween.LocalRotation(transform, Quaternion.Euler(0,0,30), 0.3f)` — 회전
   - `Tween.Alpha(canvasGroup, 0f, 0.4f)` — CanvasGroup 페이드
   - RectTransform 위치/스케일 트윈: PrimeTween 의 정확한 메서드명을 확인. 후보:
     - `Tween.UIAnchoredPosition(rect, target, dur, ease)` 또는 `Tween.UIAnchoredPositionX/Y(rect, ...)`
     - `Tween.Scale(rect, Vector3.one, dur)` 또는 `Tween.ScaleX(rect, 1f, dur)`
   - `Sequence.Create()` + `.Chain(...)` / `.Group(...)` 패턴 한 번
4. smoke 가 컴파일/Play 검증되면, **본 spec 의 task 4, 5, 7 문서의 placeholder API 명을 실제 메서드명으로 1회 일괄 갱신** (이 단계가 critic 의 C2 해결 핵심).
5. smoke 검증 완료 후 `_PrimeTweenSmoke.cs` + `.meta` 즉시 삭제 (다음 task 로 이월하지 않는다).

## 완료 기준

- `Wassup.Runtime.asmdef` 의 `references` 에 PrimeTween 항목이 있고 저장됨.
- 임의 런타임 스크립트에서 `using PrimeTween;` 가 컴파일 성공.
- `_PrimeTweenSmoke` 컴포넌트가 빈 GameObject 에 붙어 Play 모드에서 위 4종 트윈이 실행 (Console 에러 없음).
- task 4/5/7 의 PrimeTween API 호출 예시가 모두 실제 메서드명 (placeholder 아님) 으로 갱신됨.
- `_PrimeTweenSmoke.cs` + `.meta` 삭제 완료.
- Console 에 컴파일 에러 / 경고 0.
