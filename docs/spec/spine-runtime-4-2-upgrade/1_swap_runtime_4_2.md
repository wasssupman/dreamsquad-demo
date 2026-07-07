# Unit 1 — 런타임 3.8 → 4.2 스왑

## 목적

spine-unity 런타임을 4.2 로 교체한다. 삭제와 임포트를 **한 커밋**으로 묶어 컴파일 브레이크 상태가 히스토리에 남지 않게 한다. Unity 6000.4 + 4.2 조합의 성립 여부를 이 unit 에서 확정한다.

## 변경 대상

- 삭제: `Assets/Spine/`, `Assets/Spine Examples/`, `Assets/Editor/SpineSettings.asset` (3.8 설정, 4.2 가 재생성)
- 추가: `spine-unity-4.2-2026-05-29.unitypackage` (또는 그 이후 4.2 빌드) 임포트 → 새 `Assets/Spine/`, `Assets/Spine Examples/`
- 수정: `docs/reference/lessons/03-rendering-assets.md` — "3.8 고정 / 4.x 절대 금지" 항목을 새 기준으로 재작성
- (조건부) `Assets/_Project/Scripts/Presentation/SkeletonFlipXModifier.cs` — 4.2 API 변경 시에만

## 구현

1. Unity Editor 를 닫은 상태에서(또는 씬 언로드 후) `Assets/Spine`, `Assets/Spine Examples`, `SpineSettings.asset` 삭제.
2. 공식 다운로드 페이지에서 4.2 unitypackage 를 받아 임포트: https://ko.esotericsoftware.com/spine-unity-download
3. 컴파일 검증 — 특히:
   - `spine-unity` / `spine-csharp` asmdef 이름 동일 여부 (`Wassup.Runtime.asmdef` 참조 유지)
   - `SkeletonDataModifierAsset` 존속 (`SkeletonFlipXModifier` 컴파일)
   - `Skeleton.GetColor()/SetColor()` 확장, `Skeleton.A`, `Skeleton.ScaleX` 존속
4. 컴파일 실패 항목이 있으면 4.2 API 로 최소 수정 (공개 계약 유지). `Assets/Spine` 런타임 코드는 절대 수정하지 않는다.
5. BattleScene 로드 + Play 스모크: 콘솔 에러 0 확인 (스켈레톤 데이터는 이미 없으므로 스파인 로드 경로는 조용해야 정상).
6. lessons 03 재작성: 새 기준 = "런타임 4.2 고정, export 는 Spine Editor 4.2.xx 만, `.skel.bytes`/`.atlas.txt`, ASCII 파일명(NFC/NFD 함정 유지), 3.8 시대 복구 절차는 히스토리 참조로 격하".

## 완료 기준

- [x] `Assets/Spine/version.txt` 또는 `package.json` 이 4.2 를 보고 (4.2.120, `spine-unity-4.2-2026-05-29.unitypackage`)
- [x] 컴파일 에러 0 (Unity 6000.4.3f1 에서 — 공식 지원 상한 초과 리스크 해소. `SkeletonDataModifierAsset`·`GetColor/SetColor` 존속, 코드 무수정 컴파일)
- [x] BattleScene 로드 콘솔 에러 0 (배치 스모크 rootCount=14. Play 진입은 배치 한계로 씬 로드 검증 대체)
- [x] lessons 03 갱신 완료 (4.2 고정 규칙 + 배치 -importPackage abort 발견사항)
- [x] 실패 시 계획 미사용 (성공)

확인 2026-07-07. 발견사항: 컴파일 에러 상태에서 배치 `-importPackage` 가 abort → unitypackage 를 tar.gz 직접 추출(GUID/meta 보존)로 우회. `Assets/Editor/SpineSettings.asset` 은 4.2 가 재생성.
