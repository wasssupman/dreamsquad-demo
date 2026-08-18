# 6 — 포탈 프랍 배선 (선택)

## 목적

맵 저작 포탈: `PortalMarker` 프랍 한 쌍을 놓으면 그 두 셀이 텔레포트로 연결된다. 사용자가 디오라마 비전에서 명시한 프랍 역할 예시("데코, 포탈 등")의 첫 신규 역할. **v1 필수 아님** — units 0~5 완료 후 착수 여부를 사용자와 확인한다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Core/MapStage/PortalMarker.cs` — `int linkId` (같은 linkId 두 개 = 한 쌍)
- `DioramaMapBuilder.cs` — 포탈 쌍 수집 (형식 검증: linkId 당 정확히 2개, 열린 셀 위)
- `BattleBridge.cs` — 전투 시작 시 포탈 쌍 → `PortalLink` 엔티티 생성

## 구현

`GeneratedMap` 에는 포탈 필드가 없다(README 계약 2 — 구조체 무변경). 빌더가 포탈 쌍 목록을 **별도 산출물**로 반환하고, 브리지가 전투 시작 시 기존 스킬 포탈 경로(`ApplyPortal` — `EffectSpawner` 의 `PortalLink` 캐리어)를 재사용해 엔티티를 만든다. `MovementSystem` 의 텔레포트 소비는 무변경. 스킬 포탈과의 차이는 수명뿐 — 맵 포탈은 매치 수명(만료 없음), 정리는 `DestroyBattleEntities` 의 기존 타입 기반 파괴에 자동 포함되는지 확인.

시각은 스테이지 프리팹에 저작된 프랍 그 자체 + 필요 시 기존 포탈 VFX 재사용 — 신규 뷰 시스템 없음.

## 완료 기준

- [ ] 빌더 형식 검증 EditMode 테스트 (쌍 불일치·차단 셀 위 검출)
- [ ] 파일럿 맵에 포탈 쌍 배치 → 적이 입구 진입 시 출구로 텔레포트 (에디터 Play)
- [ ] 매치 재시작 시 중복 생성/잔존 없음
