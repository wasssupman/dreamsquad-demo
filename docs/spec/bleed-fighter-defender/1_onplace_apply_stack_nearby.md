# 1 — 배치 스킬: OnPlaceEffectType.ApplyStackNearby 변종

## 목적

"등장 난도질" — 배치 순간 반경 내 적 전원에게 스택을 도포하는 on-place 변종을 신설한다.
`BindNearby`(주변 CC) 분기의 미러 + unit 0 에서 검증한 스택 큐 재사용.

## 변경 대상

- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — `OnPlaceEffectType.ApplyStackNearby` enum 멤버(맨 뒤 추가) + `onPlaceStackKind` 필드(StackKind)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — on-place 실행 체인(`OnPlaceEffectType` if/else, 3488 부근)에 분기 1개
- `Assets/_Project/Tests/EditMode/` — 기존 on-place 테스트 파일에 케이스 추가(파일 위치는 BindNearby 케이스가 있는 곳을 따른다)

## 구현

1. enum 멤버는 **맨 뒤에 추가** (기존 에셋 int 직렬화 보존 — CameraDirectionConfig 선례와 같은 원칙).
2. 분기 로직: `onPlaceRange` 내 적 엔티티 수집(BindNearby 와 같은 공간 질의 재사용) →
   각 대상에 `StackModifierApplyEvent { kind = onPlaceStackKind, countDelta = onPlaceMagnitude,
   maxStack = 5, perAppDuration = onPlaceDuration }` enqueue. 신규 시스템/채널 없음.
3. 스택 종류·수·반경·지속 전부 SO 필드 — 하드코딩 금지(계약 4).
4. EditMode 케이스: 반경 내 적만 이벤트를 받는다 / 반경 밖 제외 / 적용 스택 kind·count 일치.

## 완료 기준

- [ ] compile clean + 신규 EditMode 케이스 green + 기존 on-place 테스트 무회귀
- [ ] 기존 유닛 에셋 바이트 무변경 (enum 뒤 추가 + 신규 필드 기본값 = 무형 롤아웃)
