# Handoff — unit-parts-appearance (unit 0~4 완료, 2026-07-07)

## Commit

- `d2f85916` 계획 수립 / `4e845b0c` critic 2-lane 리뷰 반영
- `0afd6588` unit 0 — 데이터 계약 (ISpineUnitVisualData + SpineSlotColor + 필드)
- `16c41fc2` unit 1 — SpineCombinedSkinCache (합성/캐시/공용 Apply, 프리뷰 경로 일원화)
- `ea0985b7` unit 2 — UnitVisualDataValidator (HelpBox, 색 키잉 슬롯 동적 탐지)
- `01e8b9be` unit 3 — LayerLabPresetImporter (프리팹 1차/프리셋 보조, 배타 resolve, 색 확장)
- `8b20d2e6` unit 4 — Defender 16종 시안 (프로그램 생성, 전 조합 유일·무경고)
- `fc333507` unit 6 — Enemy 7종 임시 휴먼 외형 (기어 0 + 원색 틴트, 몬스터 리소스 전 stopgap)
- `d7ef2d58` fix(facing) — 적이 이동 방향과 반대로 보던 문제(단일 리그 공유로 facing 규칙 통합). 사용자 시각 확인 통과(2026-07-07)

## Implemented

- 파츠 조합 외형 파이프라인 전체: 데이터(partSkins/slotColors) → 합성 런타임(캐시) → validator → Layer Lab 임포트 도구 → 16종 시안 적용
- 스폰/드래그 프리뷰가 `SpineCombinedSkinCache.Apply` 단일 경로 공유 (기존 중복 제거)
- 캐시 키 = SkeletonData 인스턴스 (도메인 리로드 OFF + 에셋 리로드 안전), comboKey 는 문서 순 join
- 배치 스모크 하네스(`SpineUpgradeSmoke.SpinePipelineSmoke`)가 합성·캐시·validator·resolve·16종 시안을 상시 검증

## Key Files

- `Assets/_Project/Scripts/Presentation/SpineCombinedSkinCache.cs` — 런타임 중심
- `Assets/_Project/Editor/UnitVisualDataValidator.cs`, `LayerLabPresetImporter.cs` — 에디터 도구
- `docs/spec/unit-parts-appearance/` — 각 unit 하단 검증 기록

## Verified

- 격리 리그 배치 스모크 PASS ×5 (unit 별 1회 이상): 합성 6→9 어태치, 캐시 히트, validator 기대 경고 5건 정확, resolve 착용15/미착용14 + 색 9슬롯, 16종 unique·무경고
- **미수행(에디터/실기기)**: ① 16종이 배치/전투/프리뷰에서 눈으로 구분되는지 ② [SpineSkin] 드롭다운·"가져오기" 버튼 GUI 확인 ③ 데모 씬 SavePrefab → 가져오기 실왕복 ④ Android 실기기

## Notes

- **아트 정식 교체 절차**: Demo_Casual Play → 조립 → SavePrefab(좌클릭) → Play 종료 → Defender 데이터 인스펙터 하단 "Layer Lab 외형 가져오기" → 프리팹 지정 → 버튼. 프리셋 슬롯은 우클릭=저장(세션 한정, 가져오기 시 영속화됨)
- gear_right 는 c_8/c_9 결번(38종) — 인덱스 연속 가정 금지. validator 가 즉시 검출한 실사례
- 본체 스킨은 `skin/skin_1` (`_c_` 인픽스 없음), eye 슬롯은 애니 색 키잉이라 틴트 불가
- SpineUpgradeSmoke 는 임시가 아닌 상시 하네스로 승격 (헤더 주석 갱신). 에디터 열림 상태에선 격리 리그로 실행

## Follow-up

- 에디터/실기기 시각 확인 (위 미수행 4건) — 통과 시 spec 상태를 "완료"로
- 시드 랜덤 외형 생성기 (README 후속 후보 — resolve/조합 데이터 구조 그대로 재사용 가능)
- Enemy 몬스터형 리소스 수급 시 동일 파이프라인 확장
