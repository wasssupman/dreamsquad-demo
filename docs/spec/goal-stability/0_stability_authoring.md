# 0. 안정도 authoring — per-goal 최대 안정도 M

## 목적

골별 최대 안정도 M 을 맵 에셋에서 authoring 한다. 부재/불일치는 전부 0(현행 유지)으로 폴백해 기존 5맵이 무마이그레이션 통과한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/MapGrid/MapDocument.cs`
- `Assets/_Project/Scripts/Data/GeneratedMap.cs`
- `Assets/_Project/Scripts/Data/MapGrid/MapDocumentBuilder.cs`
- `Assets/_Project/Editor/MapPainterWindow.cs`
- `Assets/_Project/Tests/EditMode/MapGrid/MapDocumentRoundTripTests.cs`

## 구현

1. `MapDocument` 에 `[SerializeField] float[] goalMaxStability` 추가 — `goals` 와 index 정렬 병렬 배열. `SetFrom` 시그니처 확장, `OnValidate` 에서 길이를 `goals` 에 맞춰 보정(모자라면 0 패딩). **`goals` 가 비어 `[goal]` 폴백을 탈 때 안정도도 길이 1 로 정렬**되어야 한다.
2. `GeneratedMap` 에 `NativeArray<float> goalMaxStability` 추가 + `Dispose` 등록. `MapDocumentBuilder.ToGeneratedMap`/`ToMapDocument` 왕복 반영.
3. **소비 지점 폴백** (multi-goal 계약 B1 과 같은 결): `GeneratedMap` 생산자는 6개이고 대부분 새 배열을 세팅하지 않는다. 소비 지점(unit 1 의 골 스폰)에서 `goalMaxStability.IsCreated && Length == goals.Length ? 값 : 0` 으로 판정한다. `GeneratedMap.IsCreated` 에 새 배열을 **넣지 않는다**.
4. `MapPainterWindow`: 골 목록 옆 per-goal 안정도 입력 필드 + bake 시 `SetFrom` 전달. 음수 입력 거부(0 clamp).
5. 왕복 테스트 확장: 안정도 배열 보존, 길이 불일치 폴백, 기존 픽스처(배열 부재) 통과.

## 완료 기준

- [x] compile + `MapDocumentRoundTripTests` 확장분 포함 EditMode green (9/9 + 관련 스위트 28/28).
- [x] 기존 5맵 에셋 로드 시 전 골 M=0 (Play 현행 무변화) — 부재 폴백 테스트 + 실에셋 로드 스위트 통과.
- [x] MapPainter 에서 골별 M 입력→bake→재로드 왕복 보존 확인.

구현 노트: OnValidate 는 "0 패딩 보정" 대신 코드베이스 관례대로 **검증만**(에러 로그) 하고, 폴백은 소비 지점(ToGeneratedMap)에서 일괄 적용한다.

2026-08-04 사용자 확인 완료.
