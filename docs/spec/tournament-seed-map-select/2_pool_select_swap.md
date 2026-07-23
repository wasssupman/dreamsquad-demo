# 2. 맵풀 인덱스 선택 교체

## 목적

`BuildMapForBattle` 의 풀 인덱스 계산을 로컬 랜덤에서 tournament seed 결정론으로 교체한다. 시드 부재 = 0번.

## 변경 대상

- `Assets/_Project/Scripts/Data/MapGrid/MapPoolSelect.cs` — 순수 함수 추가
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `BuildMapForBattle` 풀 분기 (≈888)
- `Assets/_Project/Tests/EditMode/` — `MapPoolSelect` 테스트 (기존 파일 있으면 이어 쓰기)

## 구현

1. `MapPoolSelect` 에 순수 함수 추가 (제약 10 — 아키텍처 무참조, sim-critical 선택 로직):
   ```csharp
   /// 서버 토너먼트 시드(uint64) → [0, count). 같은 (seed, count) → 같은 인덱스.
   public static int SelectIndexFromTournamentSeed(ulong seed, int count)
   {
       if (count <= 1) return 0;
       return (int)(seed % (ulong)count);
   }
   ```
2. `BattleBridge.BuildMapForBattle` 풀 분기 교체:
   ```csharp
   int poolIndex =
       fixedMapSeed != 0            ? MapPoolSelect.SelectIndex(seed, mapPool.Count)   // 디버그 오버라이드 유지
     : TournamentMatchReporter.HasTournamentSeed
           ? MapPoolSelect.SelectIndexFromTournamentSeed(TournamentMatchReporter.TournamentSeed, mapPool.Count)
     : 0;                                                                              // 문제(시드 부재) → 0번
   ```
   `TournamentMatchReporter` 정적 read 는 BattleBridge 선례 있음(`BeginMatch` 호출 :458). ECS 무관 — Mono 선택 로직.
3. `Build(seed, …)` 인자·`_resolvedDeck` 페어링·`IsUsableDocument` 폴백 등 나머지 분기는 그대로.
4. 맵 선택 로그 1줄 추가(어느 소스로 몇 번을 골랐는지): `[BattleBridge] map pool index={i} (source=tournament|debug|fallback0)` — 라이브 검증용.

## 완료 기준

- [x] EditMode: `SelectIndexFromTournamentSeed` — 실측 시드 9128566303723636648 % 5 == 3, count 1/0→0, ulong.MaxValue, 같은 입력=같은 출력 (4 테스트 green)
- [x] (사용자 Play) 로그인 상태 로비 입장 → 콘솔 `source=tournament`, 같은 토너먼트에서 재입장 시 같은 인덱스
- [x] (사용자 Play) 게스트/직접 Play → `source=fallback0`, 0번 맵
- [x] (사용자 Play) `fixedMapSeed != 0` → 기존 로컬 선택 유지(`source=debug`)
- [x] compile 0 error, EditMode green

확인 2026-07-23 — testrig 배치 EditMode 1293 중 1291 green(신규 6 테스트 전부 포함). 사용자 Play 확인 완료 (커밋 d0bdb85d).
