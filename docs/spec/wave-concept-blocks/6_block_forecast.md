# 6. 블록 전환 예고 — 컨셉을 미리 읽게 한다

## 목적

컨셉이 화면에 이름을 갖게 하고, **블록이 바뀔 때 미리 알린다.**

근거는 시간이다. 3분에 10~16웨이브면 웨이브당 12~18초이고 리드인은 2초다. 게다가 **전멸시 즉시 다음 웨이브**라서 잘 막으면 더 짧아진다 — 잘하는 플레이어가 더 바빠진다. 블록이 3웨이브라 컨셉 전환이 판당 4번뿐이므로, 그 4번을 미리 알려주면 12초 웨이브에서도 «읽고 → 대응하는» 루프가 성립한다. 예고가 없으면 컨셉은 «웨이브가 랜덤하게 힘들어졌다»로 느껴진다.

**매 웨이브 라벨이 아니라 블록 전환에만** 띄운다. 3웨이브 내내 같은 문구가 떠 있으면 정보가 아니라 장식이다.

## 변경 대상

- `Assets/_Project/Scripts/Data/GeneratedWavePlan.cs` — `GeneratedWave` 에 `conceptLabel`
- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — 조립 시 `concept.displayName` 기입
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 읽기 전용 프로퍼티 2개
- `Assets/_Project/Scripts/UI/Draft/WavePatternStripView.cs` — 블록 첫 행에 컨셉 라벨 (`WavePatternStripView.cs:444~455` 의 그룹 행 렌더 옆)
- `Assets/_Project/Scripts/UI/NextWaveDock.cs` — 블록 전환 예고 한 줄

## 구현

**뷰에 SO 를 넘기지 않는다.** `GeneratedWave.conceptLabel` 은 plain `string` 이다 — `SpawnGuideForecast` 가 `waypointPathIndex`·`traversalLayers` 를 plain 값으로만 넘긴 것과 같은 판단(`waypoint-flight-enemy` unit 7). 뷰가 `WaveConceptData` 를 참조하면 프레젠테이션이 저작 데이터에 묶인다.

**`BattleBridge` 읽기 전용 창구 2개** (기존 `NextWaveNumber`·`NextWaveSecondsRemaining` 와 같은 자리, `BattleBridge.cs:1889~1901`):

```
NextWaveConceptLabel  → 다음에 큐잉될 웨이브의 conceptLabel ("" = 없음)
NextWaveStartsBlock   → 다음 웨이브가 블록의 첫 웨이브인가 (bool)
```

`NextWaveStartsBlock` 을 브리지가 계산하는 이유: 블록 경계는 `_nextWaveIndex / conceptHoldWaves` 로 정해지는데 도크가 그것을 다시 계산하면 두 곳이 갈린다. 값 하나를 브리지가 소유한다. 도크는 `NextWaveStartsBlock == true` 일 때만 라벨 줄을 켠다.

**브리핑 스트립**은 웨이브당 한 행을 그린다. 블록의 **첫 행에만** 컨셉 라벨을 붙이고 나머지 두 행은 지금처럼 `유닛명 ×수량` 만 그린다 — 3웨이브가 시각적으로 한 묶음으로 읽힌다. 스트립은 앞 12장만 그리므로(웨이브 100은 명목) 블록 4개가 보인다.

**문구는 SO 의 `displayName` 이 그대로 쓰인다.** 코드에 문자열을 두지 않는다(제약 6).

## 완료 기준

- **Play 육안 (사용자 확인)**
  - 스쿼드 준비/브리핑 스트립에서 3웨이브가 한 묶음으로 읽히고 블록마다 다른 라벨이 보인다
  - 판 중 블록 전환 직전에만 도크에 다음 컨셉이 뜨고, 블록 안의 2·3번째 웨이브에서는 뜨지 않는다
  - 라벨을 보고 실제로 대응을 바꿀 시간이 있는가 — **이 unit 의 검증 질문**
- **스크린샷** — 브리핑 스트립 1장, 블록 전환 순간의 도크 1장. Play 중 MCP 뮤테이션이 막히므로 캔버스를 ScreenSpaceCamera 로 전환해 촬영한다(메모리: 오버레이 UI Play 검증).
- **EditMode** — 「평소」 블록의 `conceptLabel` 이 그 컨셉의 `displayName` 과 일치 · 컨셉 풀이 비었을 때 `conceptLabel` 이 빈 문자열이고 도크가 라벨 줄을 켜지 않는다
- **콘솔 0 에러**
