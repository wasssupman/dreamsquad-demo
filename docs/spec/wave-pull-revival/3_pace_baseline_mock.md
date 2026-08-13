# 3 — 목표 페이스 (가짜 기준선)

## 목적

전투 중에 «지금 이 페이스면 진출권 안인가»를 보여준다. 이게 있어야 당김이 **압박**을 갖는다 — 안전하게 버티면 이기는 게임이면 당길 이유가 없고, 「70점 부족」이 떠 있으면 당김은 그 70점을 만들 유일한 수단이 된다.

**서버가 없으므로 값은 가짜다.** 10인 분포를 경기 중에 받아올 경로가 아예 없다(`TournamentApi` 는 시작·완료·랭킹 조회뿐). 임시 par 로 감각을 먼저 확인하고, 백엔드가 서면 출처 함수 하나만 바꾼다.

**그래서 화면 문구는 「진출 예상선」이 아니라 「목표 페이스」다.** 「진출 예상선」은 «같은 시드를 돈 10인 분포에서 나온 실제 컷»을 약속하는 말인데 지금 값은 저작 비율이다. 전투 중 피드백은 매초 들어오고 경기 후 랭킹은 판당 한 번이라, **거짓 컷을 믿고 당김을 멈춘 플레이어는 잘못된 습관을 훨씬 강하게 학습한다** — §5 가 결과 화면에 대해 진단한 문제가 전투 화면에서는 더 세게 작동한다. 압박은 그대로 두고 약속만 뺀다. 실제 분포가 붙으면 그때 이름을 올린다(PRD §7.1).

## 변경 대상

- `Assets/_Project/Scripts/Core/PaceBaseline.cs`(신규) — par 계산 순수 함수
- `Assets/_Project/Scripts/Data/AttackDeck.cs` — `paceParFraction`
- `Assets/_Project/Scripts/Data/Decks/Deck_*.asset` — 비율 저작
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 읽기 창구 + 매 프레임 HUD push
- `Assets/_Project/Scripts/UI/ScoreHudView.cs` — 예상선 줄
- 테스트: `Tests/EditMode/PaceBaselineTests.cs`(신규)

## 구현

**1. par 는 저작 곡선이 아니라 웨이브 플랜에서 나온다.**

```
par(t) = (기본 진행으로 t 까지 나왔을 적의 killScore 합) × paceParFraction
```

덱마다 `AnimationCurve` 를 손으로 그리는 안을 버린 이유: **맵·적 구성이 바뀔 때마다 곡선을 같이 고쳐야 하고, 안 고치면 조용히 거짓말이 된다.** 플랜에서 뽑으면 덱을 바꿔도 par 가 따라오고 저작은 비율 `float` 하나로 끝난다. 「기본 진행으로 나왔을 적의 92% 를 잡는 페이스」라는 문장으로 설명도 된다.

「기본 진행」의 시각은 `GeneratedWave.triggerTimeSec`(생성기의 명목 그리드 `i × maxWaveIntervalSec`)이다. 이벤트 구동 런타임은 이 값을 읽지 않지만, **«가만히 뒀다면» 이라는 par 의 정의에는 정확히 이 그리드가 맞다.**

**2. 계단이 아니라 경사.** 웨이브 창 안에서 이전 누적 → 다음 누적으로 선형 보간한다. 계단이면 20초마다 예상선이 툭 뛰어 「갑자기 30점 부족」이 되는데, 그 점프는 플레이어가 한 일과 무관하다.

**3. 축은 점수다, 처치 수가 아니다.** 계약 7 로 킬 가중치를 유지했으므로 PRD §7.1 의 「6체 부족」은 **「70점 부족」**이 된다. HUD 가 이미 점수를 카운트업하므로(`ScoreHudView.OnEnemyKilled`) 같은 축에 얹는 것이 맞다 — 두 축을 섞으면 「1,240점인데 6체 부족」이라는 읽을 수 없는 문장이 된다. par 도 같은 `killScore` 를 쓰므로 분열체(`killScore` 0)는 par 에도 0 이다.

**4. 출처는 함수 하나** (계약 9). 인터페이스도 추상 클래스도 만들지 않는다(제약 8 — 구현체가 하나다):

```csharp
public static bool TryExpectedScore(
    in GeneratedWavePlan plan, float elapsedSec, float parFraction, out int expected)
```

서버가 생기면 **이 함수 안만** 바꾼다. 표시 코드(`ScoreHudView.SetPaceBaseline`)는 건드리지 않는다.

**5. 저작이 0 이하면 «표시 안 함»이지 0 이 아니다.** `false` 를 내고 HUD 는 줄을 숨긴다. par 0 을 그리면 화면에 「+1,240점」이 떠서 **항상 앞서고 있다는 거짓말**이 된다 — 그럴듯해서 눈으로는 안 잡힌다.

**6. HUD 는 브리지를 참조하지 않는다.** 기존 방향(`scoreHud.SetLeakStatus` / `OnEnemyKilled` 를 브리지가 호출)을 그대로 따라 브리지가 매 프레임 push 한다. 씬 wiring 이 늘지 않는다.

**7. 새지 않게 막는다** (계약 9). 표시 전용이다:
- `BattleLogger` 에 넣지 않는다
- `ScoreMath.EncodeSubmission` 에 넣지 않는다
- 결과 화면에 넣지 않는다 — 거기엔 **진짜 순위**가 서버에서 온다. 가짜 예상선을 나란히 두면 어느 쪽이 진짜인지 구별할 수 없다

**8. 표시 문구** — 점수 플레이트·스트레스 배지 아래 한 줄. 줄을 껐다 켜지 않는다(앞섰다 뒤졌다 할 때마다 레이아웃이 튀면 읽을 수 없다). 숨기는 것은 par 자체가 없을 때뿐이다.

```
      1,240
  스트레스 2
  목표 페이스 1,310 · 70점 부족
```

## 완료 기준

- [ ] 컴파일 통과, 콘솔 에러/경고 0
- [ ] EditMode: 저작 비율이 0 이하면 «표시 안 함»이 나온다(0 이 아니라)
- [ ] EditMode: 플랜이 비어도 «표시 안 함»
- [ ] EditMode: 웨이브 창 안에서 경사로 오르고 전 구간 단조 비감소다
- [ ] EditMode: 판 시작 전(음수 t)·마지막 웨이브 이후(큰 t)에서 값이 튀지 않는다
- [ ] EditMode: `killScore` 가중치를 존중한다(탱커 3점을 1점으로 세지 않는다)
- [ ] 코드 검사: `PaceBaseline` 참조가 브리지 창구 + HUD 뿐이다 — **로거·`EncodeSubmission`·결과 화면에 없다**
- [ ] Play: 전투 내내 예상선 줄이 붙어 있고, 부족/여유가 바뀌어도 레이아웃이 안 튄다
- [ ] Play 체감: 「부족」이 떠 있을 때 당김 버튼에 손이 가는가 — 이 unit 의 검증 질문이다
