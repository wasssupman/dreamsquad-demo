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
4. **브리핑 스트립 일치**: `WavePatternStripView` 가 정적 `deck` 대신 draft-stage prebuild 이후 `BattleBridge.ActiveDeck` 을 읽도록 훅. 스트립이 현재 flow 에서 표시되지 않으면 no-op(그 경우 후속 후보로 명시).
5. 씬 저장 함정 주의: BattleBridge 필드(mapPool ref, fixedMapSeed) 는 씬 직렬화 → 저장 시 확정. 풀 *내용*은 SO 라 안전. `docs/reference/lessons` 씬 위생 참조.

## 완료 기준

- [ ] `debugFixedMatchSeed` 를 여러 값으로 바꿔 실행 → 두 맵이 각각 등장(인덱스 0/1 분포 확인)
- [ ] `fixedMapSeed=0` 에서 매판(seed 미고정) 맵이 갈림 — 육안/로그 `seed=` 확인
- [ ] 각 맵이 자기 덱으로 구동: ArkFunnel=WaveA(3 레인), TwinLane=WaveB(2 레인) — 스폰 수만큼 레인 분배
- [ ] 브리핑 스트립 = 실전 덱 일치(또는 스트립 미표시 확인 후 후속 이관 기록)
- [ ] 각 맵 connectivity 통과(런타임 guard 로그에 fallback linear 경고 없음)
- [ ] 콘솔 에러 0
