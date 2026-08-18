# 1 — 튜토리얼 맵/웨이브를 랜덤 풀에서 떼어낸다

## 목적

"지정된 칸에 놓아보세요"(unit 6)가 성립하려면 지형이 매번 같아야 한다. 튜토리얼
전용 맵과 저작 웨이브는 **이미 만들어져 있고**, 지금은 랜덤 풀 안에 섞여 있다.

## 변경 대상

- `Assets/_Project/Scripts/Data/MapGrid/MapDocumentPool.cs`
- `Assets/_Project/Data/Maps/MapDocumentPool.asset`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (맵 선택 우선순위)

## 구현

**현재 상태**: `MapDocument_Tutorial` + `WavePlan_Tutorial` 이 `entries` 의 13번째
엔트리로 들어 있다. 선택은 `seed % Count` 라 **일반 매치에서 튜토리얼 맵이 뽑힌다** —
저작 10웨이브가 토너먼트 판에 그대로 나온다는 뜻이라 그 자체로 결함이다.

**엔트리를 전용 슬롯으로 옮긴다.** `MapDocumentPool` 에
`[SerializeField] private Entry tutorialEntry;` + `public Entry TutorialEntry` 를 더하고
`entries` 에서 그 줄을 뺀다. `devEntries` 에 두지 않는다 — 그건 `DevMapOverride` 로만
들어가는 dev 슬롯이고, 튜토리얼은 프로덕션 경로다.

**선택 우선순위**에 한 단을 끼운다:

```
fixedMapSeed (디버그)  >  튜토리얼 판  >  토너먼트 시드  >  랜덤
```

「튜토리얼 판인가」는 unit 0 의 판정을 그대로 쓴다.

**⚠ 파급 — 토너먼트 맵 로테이션이 바뀐다.** `Count` 가 13 → 12 로 줄어 같은 서버
시드가 다른 맵을 고른다. 세이브 호환 문제는 아니지만(시드는 매치마다 서버가 준다)
「같은 시드 = 같은 맵」을 기준으로 잡아둔 기록은 이 커밋 전후로 갈린다. 로테이션에서
튜토리얼 맵이 빠지는 것이 이 변경의 **의도**다.

## 완료 기준

- compile 통과 · `MapDocumentPool.Count == 12`.
- 튜토리얼 판 진입 → `MapDocument_Tutorial` 지형 + `WavePlan_Tutorial` 웨이브.
- 튜토리얼을 끝낸 계정으로 재진입 → 풀에서 뽑힌 일반 맵, 튜토리얼 맵은 나오지 않는다.
- `MapDocumentPoolDevEntriesTests` 가 계속 초록(dev 슬롯 계약 불변).
