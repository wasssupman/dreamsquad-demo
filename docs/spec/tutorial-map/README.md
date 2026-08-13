# tutorial-map — 적 유형을 가르치는 10웨이브 맵

> ## 목표 3줄
>
> 1. **적 유형 14종을 10웨이브에 걸쳐 하나씩 소개한다** — 무엇이 위험한지 배우는 것이 이 판의 목적이다.
> 2. **웨이브 편성이 결정적이다** — 「웨이브 7 은 비행」이 매번 같아야 가르칠 수 있다.
> 3. **지형은 가르치는 데 방해하지 않는다** — 직선 복도 하나. 길찾기·갈래를 배우는 판이 아니다.

상태: **구현 완료 2026-08-13** · 잔여 = 사용자 Play 체감

## 왜 생성 웨이브를 쓰지 않나

라이브 덱은 `waveConceptPool` 에서 **무작위 추첨**한다(`waveSeed` 로 고정되긴 하지만 편성은 풀 추첨 결과다). 「웨이브 3 = 탱커」를 보장할 수 없고, `minWaveNumber` 게이트 때문에 비행·엘리트는 초반에 아예 못 나온다.

그래서 **저작 플랜**(`WavePlanAsset`)을 쓴다. `wave-authoring-test-mode` 가 만든 도구이고, `WavePatternGenerator.FromPlanAsset` 이 런타임 플랜으로 변환한다. 저작 플랜은 **`minWaveNumber` 게이트를 받지 않으므로**(`AttackUnitData.cs:43`) 웨이브 7 에 스키머(게이트 8)를 놓을 수 있다.

## 배선 — 플랜은 맵과 한 몸이다

`MapDocumentPool.Entry` 에 `plan` 필드를 추가했다. 인카운터가 `(문서, 덱, 플랜?)` 이 된다.

**왜 덱이 아니라 엔트리인가**: 튜토리얼 10웨이브는 **그 맵의 지형·배치를 전제로** 짜였다. 덱에 달면 다른 맵에 그 덱을 붙이는 순간 플랜이 따라가 무의미해진다. 엔트리는 문서와 덱을 이미 한 쌍으로 쥔 유일한 자리다.

우선순위: **테스트 모드 플랜 > 맵 인카운터 플랜 > 덱의 생성 웨이브**. 테스트 모드가 이기는 이유는 그쪽이 「지금 이 플랜을 보겠다」는 명시 지시라서다.

⚠ **이 배선이 기존 버그를 하나 드러냈다.** `StartBattle` 의 로그가 `_authoredPlan.displayName` 을 직접 읽어, 플랜이 인카운터에서 오면 `_usingAuthoredPlan == true` 인데 `_authoredPlan == null` 이라 **NRE 로 판이 죽었다.** 활성 플랜의 단일 출처(`_activePlan`)를 만들어 고쳤다 — 소스가 둘이 된 순간 「_authoredPlan 이 곧 활성 플랜」이 거짓이 됐다.

## 맵 — 15×11, 직선 복도 + 중앙 소광장

```
y10 ###############      # 테두리(Deco)
y 9 #PPPPPPPPPPPPP#      P 배치 가능(Place) 98칸
y 6 #PPPPP...PPPPP#      · 복도(Walk)
y 5 S.............G      S 스폰(0,5) → G 골(14,5)
y 4 #PPPPP...PPPPP#      중앙 소광장 x=6~8, y=4~6
y 1 #PPPPPPPPPPPPP#
y 0 ###############
```

- **복도 1줄**이라 적이 한 줄로 오고 무엇이 오는지가 또렷하다. 갈래·강·거점 없음
- **중앙 소광장**만 3×3 으로 넓혀 뭉침·분리가 눈에 보이게 한다
- 배치 98칸으로 넉넉 — 배치 실패로 학습이 막히지 않게

## 10웨이브 교습 순서

| W | 길이 | 편성 | 가르치는 것 |
|---|---|---|---|
| 1 | 20s | Basic ×4 | 적은 스폰에서 마음으로 걸어온다 |
| 2 | 20s | Basic ×3 + Swift ×3 | 속도차 — 빠른 적을 놓친다 |
| 3 | 22s | Tanker ×2 + Basic ×2 | 체력 벽 — 화력이 모자라면 안 죽는다 |
| 4 | 22s | Rootcaster ×3 | 원거리 — 멈춰서 방어선을 깎는다 |
| 5 | 24s | Runner ×8 | 물량 — 처리량이 모자라면 새어나간다 |
| 6 | 24s | Sniper ×2 + Needler ×3 | 사거리 장·단 — 누가 먼저 닿나 |
| 7 | 24s | **Skimmer ×3** | **비행 — 대공이 없으면 못 잡는다** |
| 8 | 26s | Heartseeker ×2 + Debuffer ×2 | 거점 직행 + 약화 |
| 9 | 28s | Kindler ×3 + **Slime ×1** | 화염 스택 + 엘리트 분열 |
| 10 | 30s | **Dragon ×1** + Vanguard ×4 | 비행 엘리트 + 호위 — 총복습 |

**총 240초 · 라이브 로스터 14종 전부 등장.** 단순→복합, 지상→공중, 일반→엘리트 순으로 쌓는다. `timerDurationSec: 0` 이라 **시간 압박이 없다**(전 웨이브 처리 후 승리).

## 변경 대상

- **신규** `Assets/_Project/Data/Maps/MapDocument_Tutorial.asset`
- **신규** `Assets/_Project/Scripts/Data/WavePlans/WavePlan_Tutorial.asset`
- **신규** `Assets/_Project/Scripts/Data/Decks/Deck_Tutorial.asset` — 플랜이 웨이브를 이기므로 이 덱은 폴백 + 누수 한도·마음 체력 공급원
- `Assets/_Project/Scripts/Data/MapGrid/MapDocumentPool.cs` — `Entry.plan`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `_encounterPlan`·`_activePlan` + 우선순위
- `MapDocumentPool.asset` — dev 슬롯 **12**

## 완료 기준

- **EditMode** — `TutorialEntry_TeachesEveryLiveEnemyTypeInTenWaves`: 플랜 배선 존재 · 10웨이브 · `timerDurationSec 0` · **라이브 로스터 전종 등장**(로스터에 적을 추가하면 이 단언이 튜토리얼 갱신을 요구한다) · 그룹 스폰 시각이 웨이브 길이 안 → 2,411 중 실패 4(전부 `map-rework` 폭 계약 대기, 무관)
- **PlayMode** — `TutorialDevSlot_UsesAuthoredPlan_NotGeneratedWaves`: 판별기는 **컨셉 라벨의 부재**다. 저작 플랜은 컨셉을 만들지 않으므로 라벨이 비고, 폴백이 일어나면 `Deck_Tutorial`(Serpent 복제)의 컨셉이 돌아 라벨이 채워진다
- **무회귀** — 공성 3맵·레인 경로 2맵 라이브 5/5
- **Play 체감 (사용자 확인 — 미완)** — **이 spec 의 검증 질문**
  - 웨이브마다 「새로 배운 것」이 하나씩 있는가
  - 웨이브 7(비행)에서 대공이 없어 막히는 경험이 학습으로 읽히는가, 아니면 부당하게 느껴지는가
  - 10웨이브 240초가 지루하지 않은가

## 후속 후보

- **진입 경로** [S] — 지금은 dev 슬롯 12 뿐이다. 실제 튜토리얼 플로우(첫 실행 감지·아웃게임 버튼)는 별건.
- **웨이브별 안내 문구** [M] — 지금은 편성만으로 가르친다. 「이 적은 날아온다」 같은 텍스트/툴팁은 UI 작업.
- **배치 강제·힌트** [M] — 대공을 안 뽑았으면 웨이브 7 을 못 막는다. 권장 덱을 주거나 힌트를 띄우는 건 별건.
- **로스터 확장 시 유지** [S] — EditMode 단언이 요구하지만, 어느 웨이브에 넣을지는 저작 판단이다.
