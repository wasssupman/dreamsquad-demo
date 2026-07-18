# 6 — 유닛 설명 필드 (desc, 시트-동기)

## 목적

유닛 설명을 시트에서 관리 가능한 **plain SO 필드 `desc`** 로 만든다(체력·공격력과 완전 동일 구조·동작). "지금것을 디폴트로" = 현재 자동 요약문(`UnitKitSummary.Build`)을 각 Defender SO의 `desc`에 **실제 값으로 시드**. 이후 desc는 시트에서 편집. (unit 7 = DTO/시트 왕복.)

## 변경 대상

- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — `public string desc;`(displayName 다음).
- `Assets/_Project/Scripts/Data/UnitKitSummary.cs` — `Describe(u)` 접근자(desc 있으면 desc, 없으면 Build — 방어적 폴백).
- `Assets/_Project/Scripts/UI/Outgame/SquadUnitDetailView.cs` — 설명문에 `Build` → `Describe`.
- `Assets/_Project/Tests/EditMode/UnitKitSummaryTests.cs` — Describe 테스트.
- **데이터 시드**: 전 `Assets/_Project/Data/Defenders/*.asset` 의 `desc` = `Build(so)` 로 1회 마이그레이션(에디터 액션). 자산 clean 확인 후.

## 구현

- `desc`는 nullable 아님(문자열, displayName 동형). 부분갱신 규약상 빈 셀=유지.
- `UnitKitSummary.Describe(DefenderUnitData u)` = `(u!=null && !string.IsNullOrEmpty(u.desc)) ? u.desc : Build(u)`. 상세뷰·기타 표시처가 이걸 단일 진입점으로 사용. `Build`는 시드/폴백 생성기로 유지.
- 시드: 각 Defender SO `desc = Build(so)`; SetDirty + SaveAssets. desc 필드 신설로 어차피 전 asset 재직렬화(`desc:` 라인 추가)되므로 값까지 함께 기입.

## 완료 기준

- [ ] 컴파일 클린. `UnitKitSummaryTests` (기존 10 + Describe 신규) 통과.
- [ ] 18개 Defender asset `desc`에 현재 요약문 시드됨(예: archer `desc` = "레인저 · 원거리형. 배치 시 주변 속박."). 상세뷰가 desc를 표시.
- [ ] `desc` 편집 시(Inspector/이후 시트) 상세뷰에 반영. 하드코딩 수치 0.
