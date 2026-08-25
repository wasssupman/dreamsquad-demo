# 6. Handoff — wave-pull-revival

## Commit

- `4b5cfeac` — units 0~5 (당김 복귀 + 겹침 상한·예고·묶음 변주·목표 페이스)
- `477d1409` — **unit 7** 원뎁스 당김 + HUD 색 통일 (예고 은퇴). Play 육안 확인 2026-08-23
- unit 3(목표 페이스)은 2026-08-20 은퇴 — 화면·`PaceBaseline`·`paceParFraction` 모두 제거됨

**spec 종료.** 아래 «Implemented» 는 units 0~5 시점 기록이라 unit 7 이 뒤집은 항목이 있다 — 표시된 곳을 보고, 최신 계약은 README 와 [7](7_one_depth_pull.md) 이 정본이다.

## Implemented

- **당김 복귀** — 도크 아래 행이 다시 버튼이다. `three-minute-survival` unit 2 가 껐던 플레이어 경로만 되돌렸고, `ForceNextWave` 기제는 그때도 살아 있었다.
- **기제/규칙 2층** — `ForceNextWave()`(상한 없음, 스모크의 판 진행 동력) / `TryPullNextWave()`(상한 검사 후 위임, **플레이어 경로는 이것뿐**). 덕분에 스모크 3종을 한 줄도 안 고쳤다.
- **겹침 상한** — `_pullsSinceClear`. 당길 때 +1, **전멸 진행에서만** 0. 타임아웃 진행은 리셋 아님. 덱 저작 `maxPullsPerClear`(라이브 3).
- ~~**다음 웨이브 구성 예고**~~ — **unit 7 에서 전량 은퇴**(말풍선·브리지 창구 모두). 좌하단 폭 예산(≤300)에서 «상시로 읽히는 예고»와 «원뎁스 조작»이 양립하지 않아 조작을 골랐다. 되살리려면 도크가 아니라 **자리를 옮기는 것**이 선행이다 — [7](7_one_depth_pull.md) 참조.
- **묶음 가운데 변주** — 컨셉 SO `variantSlots`. 블록 2번째 웨이브에 **삽입**(교체 아님). 입구는 블록 배정을 물려받아 **rng 소비가 늘지 않는다**. 4종 저작(벌떼→저격수 / 중장→벌떼 / 원거리→덩치 / 평소→덩치), 「공습」은 의도적으로 없음.
- ~~**목표 페이스(가짜 기준선)**~~ — **은퇴 2026-08-20.** 이하는 당시 기록: `PaceBaseline.TryExpectedScore`. par = 기본 진행으로 그때까지 나왔을 적의 `killScore` 합 × `paceParFraction`(0.92). 저작 곡선을 쓰지 않아 덱·맵이 바뀌어도 따라온다. **화면 문구는 「진출 예상선」이 아니다** — 그건 실제 10인 컷을 약속하는 말이고 지금 값은 가짜다.
- **튜토리얼 당김 안내 복원** — 배치·스트레스 뒤 스텝. 문구는 「점수를 위해 당겨라」가 아니라 무엇·대가·언제.
- **당김 기록** — `ForceNextWave` → `RecordWaveEvent("wave_forced")` → 직렬화가 **이미 이어져 있었다**. 코드 추가 없이 테스트로 고정. ⚠ 단 **로컬 로그까지**다(아래 Notes 8).

## Key Files

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 카운터·리셋 3지점·`TryPullNextWave`·읽기 창구 6개·`RefreshPaceHud`
- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — `ResolveBlockConcept` 가 변주 배열까지 내놓음, `InheritLanes`
- `Assets/_Project/Scripts/UI/NextWaveDock.cs` — **원뎁스 알약 버튼**(unit 7). 색 정본은 `ScoreHudView` 이고 **값은 `BattleScene.unity` 에 직렬화돼 있다** — C# 기본값만 바꾸면 화면이 안 바뀐다
- `Assets/_Project/Data/WaveConcepts/Concept_*.asset` — **변주 밸런스를 만지는 곳은 여기뿐**
- `Assets/_Project/Scripts/Data/Decks/Deck_*.asset` — `maxPullsPerClear`·`paceParFraction`·`waveGeneratorVersion 4`

## Verified

- 컴파일 에러 0. **내 변경에서 나온 경고 0** (기존 `enableWordWrapping`/`FindObjectOfType` 경고는 프로젝트 전역 레거시)
- **EditMode 2365개 / 실패 4개** — 4개는 전부 `MultiGoalPoolSeparationTests`(폭1 협곡)로 **기존 실패**다. 해당 `MapDocument_*.asset` 과 그 테스트 파일이 전부 HEAD 그대로고 이 spec 의 diff 에 없다(`map-rework` 가 폭1 협곡을 은퇴시킨 결과).
- **PlayMode / 실패 0** — `WavePullCapTest`(4) + `TallyFlowTest`·`EndlessModeSmokeTest`·`MovementIntegritySmokeTest`·`DraftFlowSmokeTest`·`FirstSessionTutorialSmokeTest`·`GoalStabilityTest`·`MapCrowdClearanceTest`
- **`StructureLivePlayTest` 3개 실패는 기존 실패다 — 스태시 대조로 증명했다.** 내 변경을 전부 걷어낸 HEAD 상태에서 같은 3개가 **같은 메시지로** 실패한다(적 마음 인접 배치 · 저작 문서 30×30 vs 26×18 · 거점 프랍 미표시). 위 EditMode 맵 실패와 같은 뿌리로 보인다.
- 계약 9 코드 검사: `PaceBaseline` 참조가 브리지 창구 + HUD 뿐. 로거·`ScoreMath`·`ResultScreen`·`Core/Api` 에 0건
- **투트랙 리뷰 완료** — ECS 트랙 **APPROVE**(게이트웨이·맥락 소유권·결정론·리셋 대칭 4축 이상 없음, 신규 채널 0 사실 확인). 일반 트랙 REQUEST CHANGES 2건은 아래대로 반영했다.
- **Play 육안 미완** — 아래 Follow-up

## Notes (되돌리면 안 되는 의도)

1. **`ForceNextWave` 에 상한을 걸지 말 것.** 스모크 3종이 이것을 판 진행 동력으로 연타한다(`TallyFlowTest` 20회·`EndlessModeSmokeTest` 40회). 상한은 `TryPullNextWave` 가 소유한다.
2. **타임아웃 진행(`capReached`)에서 예산을 리셋하지 말 것.** 리셋하면 «가만히 있기»가 당김 예산을 벌어준다.
3. **작성 플랜에는 상한이 없다.** 그 모드는 전멸 리셋 분기를 구조적으로 지나지 않아, 상한을 걸면 3회 뒤 **영구 잠김**이 된다. 특수 케이스가 아니라 「회복 사건이 없는 모드에는 상한도 없다」는 규칙이다.
4. **변주는 삽입이지 교체가 아니다.** 교체하면 가운데 웨이브에서 블록의 성격이 사라져 압력 상승이 끊기고, 스트립의 블록 라벨(`conceptLabel`)과도 어긋난다.
5. **변주 입구를 새로 뽑지 말 것.** `AssignLanes` 를 다시 부르면 rng 소비가 늘어 변주 미저작 덱까지 편성이 흔들린다(지금은 byte-identical 유지).
6. **예상선을 로그·제출값·결과 화면에 넣지 말 것.** 가짜 par 라 새는 순간 가짜 경쟁 기록이 진짜인 척 저장된다. `BattleLogPullEventTests` 가 이걸 지킨다.
7. **표시 캐시의 무효화 지점을 지우지 말 것.** `_nextWaveSummaryIndex`(브리지)와 도크의 `_lastWaveNum` 등은 매치 경계에서 -1/-2 로 돌아가야 이전 판 문구가 안 남는다.
8. **배틀 로그는 서버로 가지 않는다.** `SnapshotJson()` 은 프로덕션 호출자가 0곳이고, 서버로 가는 것은 `ReportResult(점수 int, 덱 정보)` 뿐이다. 로그의 종착지는 `EndSession()` 이 쓰는 `GameLogs/session-*.json` 이다. 「당김 시점 비교」(PRD §7.2)를 만들려면 **배틀 로그를 서버로 보내는 계약이 선행**이다 — unit 5 를 다 해도 그건 확보되지 않는다.
9. **「공습」에 변주를 저작하지 말 것.** 관례가 아니라 테스트가 막는다(`VariantSlots_NeverCrossAltitude`·`Airstrike_HasNoVariant`). 대공에 투자한 플레이어가 그 웨이브만 헛돌지 않게 하는 규칙이고, `SlotAltitude` 에 `Any` 를 두지 않은 것과 같은 성격의 구조적 방어다.
10. **잠금 문구를 「정리하면 다시」로 되돌리지 말 것.** 같은 플레이트 A행이 「다음 8초」를 띄우고 있어 «8초 뒤 풀린다»로 읽힌다. 해제 조건은 시각이 아니라 사건이라 「적을 정리해야 열림」이다.

## 리뷰에서 고친 것 (커밋 전)

| 지적 | 반영 |
|---|---|
| 예고 줄이 172px 에서 잘린다(씬이 `buttonContentPadding.z=86` 저작) | 텍스트 좌우 인셋을 아트 여백과 **분리**(`textInset`) + 3행 전부 자동 축소(하한 13) |
| Tally 4초 동안 가짜 par 가 최종 점수 옆에 얼어붙는다 | `ScoreHudView.OnPhaseChanged` 가 **Battle 이 아니면 줄을 끈다** |
| `PaceBaseline` 이 창을 찾은 뒤에도 100웨이브를 끝까지 돈다(매 프레임) | `break` |
| 「중장 가운데」 단언이 «변주가 실제로 들어왔나»를 안 본다 | 저작이 있는 컨셉 한정 `sawOther` 단언 추가 |
| 씬 저작 세로 패딩(`.y`/`.w`)이 버려졌다 | A행 상단·C행 하단에 적용 |
| `InheritLanes` 가 무지정(-1) 변주를 구체 lane 에 못박는다 | -1 은 -1 로 남긴다 |
| `Concept_Spread` 변주가 온보딩 웨이브 2를 바꾼다 | **저작 제거** — 첫 블록 전용이고 본 슬롯이 무필터라 「다른 성격이 낀다」가 성립하지 않는다 |
| **전멸 리셋의 긍정 케이스 테스트가 없다** | `필드를_비우면_예산이_돌아온다` 추가 — 없으면 `cleared` 리셋이 죽어도 전부 초록이었다 |
| 작성 플랜 면제에 테스트가 없다 | `작성_플랜에서는_상한이_적용되지_않는다` 추가 |
| 매 프레임 TMP 재생성 · 모순 주석 · 매 프레임 `FindAnyObjectByType` · `pace` 부분문자열 오탐 · `_pullPunchTween` 미정지 | 전부 반영 |

## Follow-up

- **Play 육안 확인** (이 spec 의 검증 질문): ①당김 버튼 앞에서 망설이는가 ②덱마다 당기는 시점이 다른가 ③「넘기고 다음에 당기자」가 나오는가 ④묶음 가운데 변주가 눈에 띄는가 ⑤「부족」이 떠 있을 때 버튼에 손이 가는가
- **당김이 판단거리인지 실측 (PRD §9 V1 대용, unit 2 뒤에)** — 같은 시드로 ⓐ무당김 / ⓑ열릴 때마다 당김 두 판의 최종 점수 비교. **ⓑ > ⓐ** 여야 성립. 초안의 기준(「상한까지 당겨도 전량 소화 안 됨」)은 100웨이브 명목 대비 실도달 10~16 이라 **구조적으로 항상 참**이어서 버렸다 — 통과해도 아무것도 배우지 못한다
- ⚠ **`paceParFraction` 0.92 는 방향조차 미지수다 — Play 에서 가장 먼저 볼 것.** 두 리뷰어가 **정반대** 진단을 내놨고 둘 다 근거가 있다:
  - par 가 **낮다**: `triggerTimeSec` 은 생성기 주석대로 «최악 케이스 시각»(20초 그리드)이라 명목 9웨이브인데 실제는 10~16웨이브를 받는다 → 정상 플레이어는 상시 par 를 넘어 「+N점」만 뜨고 압박이 장식이 된다.
  - par 가 **높다**: 웨이브 i 의 점수를 창 i 안에서 적립하는데 그 적은 리드인 2초 뒤 나와 걸어와 죽으므로 실제 처치는 대부분 창 i+1 에 들어간다 → 잘 막아도 상시 「부족」이고 수량이 지수 성장하므로 후반일수록 격차가 벌어진다.

  **두 힘이 서로 반대라 순효과는 재보기 전엔 모른다.** 숫자를 미리 흔들지 않았다 — 한 판 돌려 「부족/여유」가 어느 쪽으로 치우치는지 보고 정하는 게 맞다. 치우침이 크면 비율이 아니라 **par 축**을 바꿔야 한다(창을 한 웨이브 밀거나 명목 그리드 대신 실제 릴리즈 기준으로) — 어느 쪽이든 `PaceBaseline` 안에서만 끝난다(계약 9).
- **`maxPullsPerClear` 3 은 어림값** — 체감 후 조정
- **씬 배선 없음** — 튜토리얼의 `waveDock` 은 미배선이어도 런타임에 찾는다. 명시 배선을 원하면 그때 씬을 건드린다
- 나머지는 [README](README.md) 하단 후속 후보 참조
