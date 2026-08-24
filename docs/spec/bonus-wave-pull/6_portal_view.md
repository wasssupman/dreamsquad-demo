# 6 — 전투 중 포탈 뷰

## 목적

보너스 포탈이 **열리고 닫히는 것**을 보여준다. 기존 스폰 지점 포탈은 맵 빌드 시 정적으로
배치되지만, 이건 전투 중에 나타났다 사라진다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- BattleScene — SerializeField 배선

## 구현

1. `SpawnPortal_Red.prefab` 을 브리지가 **직접 Instantiate** 한다.
   선례는 거점 뷰다 — `SpawnStructureViews` / `ClearStructureViews`(리스트 등재 + teardown 공유).

   프랍 파이프라인(`MapThemeData.spawnStructureProp`)을 태우지 않아도 **잃을 것이 없다**:
   그 `PropData` 는 `billboardMode: None` · `visualScale: 1` · `visualOffset: 0` 이다(확인함).
   이 확인 결과를 코드 주석에 남긴다 — 안 적으면 다음 사람이 「프랍 파이프라인을 태워야 하나」
   에서 또 멈춘다.

2. 타이밍은 스케줄러(unit 4)가 소유한다. 뷰는 「열어라 / 닫아라」만 받는다.
   - `portalAppearDelaySec` 후 등장
   - 마지막 스폰 + `portalLingerSec` 후 퇴장

3. **teardown 등재 필수** — `_bonusPortalViews` 리스트 + `ClearBonusPortalViews()` 를
   `TeardownCurrentBattle` 에 넣는다. 빠뜨리면 재시작 시 옛 포탈이 보드에 남는다
   (사직서·픽업 뷰 사고와 동형).

4. 좌표 변환은 `BoardSpace` 경유 — 평면 tilemap 보드라 sim-Y 를 그대로 쓰면 안 된다.

## 완료 기준

- [x] 컴파일 에러 0
- [x] 버튼 → 1초 뒤 포탈 2개가 저작된 칸에 등장
- [x] 마지막 스폰 후 지정 시간에 퇴장
- [x] 판 재시작 후 포탈 GameObject 0개
- [x] 같은 판에서 보너스 당기기를 2회 해도 포탈이 두 벌 남지 않는다(계약 13)

**확인 2026-08-24** — PlayMode 재시작 잔존 0. `TeardownCurrentBattle` 등재까지(리뷰 H-1 — 로비 복귀 경로).
