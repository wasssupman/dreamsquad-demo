# 4. 풀 asset 생성·배선 + 랜덤/패턴 실증

## 목적

앞 유닛의 조각(선택 로직·BattleBridge resolve·맵 2종·덱 2종)을 실 asset·씬 배선으로 합쳐, "매판 랜덤 맵 + 맵마다 다른 적 패턴"을 Play 로 실증한다.

## 변경 대상

- `Assets/_Project/Data/Maps/MapDocumentPool.asset` (신규)
- `Assets/_Project/Scenes/BattleScene.unity` — `BattleBridge.mapPool`, `fixedMapSeed`
- `Assets/_Project/Scripts/UI/Draft/WavePatternStripView.cs` (+ 씬) — 브리핑 스트립이 `ActiveDeck` 반영

## 구현

1. `MapDocumentPool.asset` 생성, 엔트리 2개:
   - `[0] = (MapDocument_ArkFunnel, WaveA)`
   - `[1] = (MapDocument_TwinLane, WaveB)`
2. `BattleBridge.mapPool` 에 풀 배선. 레거시 `mapDocument`/`deck` 는 폴백으로 남겨도 되나(안 뽑힘) 혼동 방지로 `mapDocument` 는 None 권장 — 단 `deck` 은 폴백 유지.
3. **`fixedMapSeed = 0`** (현재 20260719). 이게 매판 랜덤을 켜는 스위치. 비0으로 두면 인덱스가 핀돼 한 맵만 나온다.
4. **브리핑 스트립 → 후속 이관**: `WavePatternStripView` 는 draft 에서 표시되지만(`DraftView.RunFlow`) 자기 serialized `deck`(WaveA)로 프리뷰를 만든다. per-map 동기화는 draft-flow 플러밍이 필요하고 exact-match 는 waveSeed=0 에서 근사라, 핵심 기능과 분리해 후속 후보로 이관(README 참조). 이 unit 에서는 손대지 않음.
5. 씬 저장 함정 주의: BattleScene 은 언로드+디스크 클린 상태였으므로 **YAML 직접 배선**(mapPool 추가 + fixedMapSeed 0)으로 격리. 로드된 씬 WIP 베이크 위험 없음. 풀 *내용*은 SO 라 안전.

## 완료 기준

- [x] `MapDocumentPool.asset` 생성 + 엔트리 2개 배선 — readback Count=2 [0]ArkFunnel/WaveA [1]TwinLane/WaveB
- [x] `BattleBridge.mapPool` 배선 + `fixedMapSeed=0` (BattleScene YAML surgical diff 확인)
- [x] 선택 분포 검증(Play-free): matchSeed 1..2000 → idx0 984 : idx1 1016 (~50/50, 둘 다 도달). 강제값 `debugFixedMatchSeed=1`→ArkFunnel, `=2`→TwinLane
- [x] **Play 실증**: `debugFixedMatchSeed=1` → ArkFunnel(spawns=3)+WaveA, `=2` → TwinLane(spawns=2)+WaveB. 둘 다 게임뷰 렌더 확인(ArkFunnel 미로형 3스폰 / TwinLane Y자 합류 2스폰) + `ActiveDeck`/`_generatedMap` reflection 일치 + 콘솔 에러 0 + connectivity fallback 경고 없음
- [~] 브리핑 스트립 = 실전 덱 일치 → **후속 이관**(README follow-up)

확인 2026-07-22 (unit 4 — 풀 asset·씬 배선·선택 분포·Play 렌더 실증 완료). 스크린샷 Assets/Screenshots/twinlane_verify_{ark,twin}.png.
