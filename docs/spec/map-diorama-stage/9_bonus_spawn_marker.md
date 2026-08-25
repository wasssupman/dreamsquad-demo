# 9 — BonusSpawnMarker: 보너스 포탈 칸의 스테이지 저작 (제안 2026-08-25)

## 목적

main 의 `bonus-wave-pull` 이 맵 저작 축을 하나 추가했다 — `GeneratedMap.bonusSpawns`(포탈 칸 정확히 2개 또는 0개).
3차 병합(`277121d8`)으로 소비 코드(`BattleBridge.BonusWave`)는 들어왔지만 채우는 쪽은 은퇴한 `MapDocument.bonusSpawns` 뿐이라
스테이지 맵에서는 **미저작 경로(bonus-wave-pull 계약 8)** 로 보너스 버튼이 뜨지 않는다. 라이브 Duel 이 main 과 기능 동등해지도록
스테이지 파이프라인에 등가 마커를 추가한다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Core/MapStage/BonusSpawnMarker.cs` — 선언만(로직 0), 기즈모 라벨 `B`
- `Assets/_Project/Scripts/Core/MapStage/MapStageScanner.cs` — 마커 수집 → `StageScan.bonusSpawns`
- `Assets/_Project/Scripts/Data/MapStage/DioramaMapBuilder.cs` — `Validate`: tiles 합성 뒤 `BonusSpawnAuthoringRules.Validate` **재사용**(규칙 단일 소유자 계약 승계 — 복제 금지) · `Assemble`: `NativeArray<int2> bonusSpawns` 채움(구 `MapDocumentBuilder` 와 같은 형태)
- `Assets/_Project/Editor/MapStageEditors.cs` — 스냅 버튼 인스펙터 등록
- 프리팹 `MapStage_Duel`(라이브)·`MapStage_DuelClassic` — 포탈 2칸 저작. main 저작값 (11,2)/(11,7)은 23×10 좌표라 그대로 못 쓴다 — 우리 Duel 지형에서 «두 다리 위 열린 칸» 으로 재저작
- 테스트 `DioramaMapBuilderTests` — 0개 통과 · 1개/3개 거부 · 중복 거부 · 차단 셀 거부 · 격리 셀 거부 · 2개 정상 조립
- `docs/reference/map-stage-authoring.md` — 구성 요소 표에 행 추가

## 구현

1. `BonusSpawnMarker` 는 `RouteMarker` 와 같은 꼴(위치→셀 양자화, 필드 없음).
2. 스캐너는 계층 순서로 수집한다 — 개수 계약(2)은 빌더가 검사하므로 스캐너는 세지 않는다.
3. `Validate` 는 스폰·골 검사 **뒤**에 `BonusSpawnAuthoringRules.Validate(cells, w, h, tiles, goals, errors)` 를 부른다. 합성 tiles(열림=Walk/차단=Deco)를 넘기면 ⓐ 통행 가능·ⓑ 골 도달 규칙이 그대로 성립한다.
4. `GeneratedMap.bonusSpawns` 는 0개여도 생성해 둔다(`MapDocumentBuilder` 와 동형) — 소비 측 가드(`IsCreated && Length > 0`)가 미저작으로 읽는다. 셀 순서는 골과 같은 (y, x) 사전순(저작 순서 비의존).

## 완료 기준

- [ ] compile + EditMode 두 lane (위 규칙 케이스 6종 포함) 무회귀
- [ ] `StagePoolBuildabilityTests` 통과 (Assemble 경유로 라이브·dev 전 스테이지 자동 커버)
- [ ] Duel 스테이지 Play: 조건 충족 시 보너스 버튼 등장 → 포탈 2개 열림 → 보너스 적 10기 스폰 (main 의 8_duel_authoring_and_play 와 같은 축)
- [ ] 저작 가이드 갱신
