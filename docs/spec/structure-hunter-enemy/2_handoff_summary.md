# structure-hunter-enemy — 인계 요약 (units 0~1)

## Commit

| 해시 | 제목 |
|---|---|
| `5f123d9c` | docs(new-enemies): 신규 적 2종 spec — 거점 사냥꾼 · 비행 웨이포인트 |
| `04a63dd8` | feat(structure-hunter): unit 0 — 마음사냥꾼. 유인이 안 통하는 첫 적, 코드 0줄 |
| `3ac29394` | fix(structure-hunter): 리뷰 반영 — 덱 편입 되돌림 + 저작 3건 + 문서 사실오류 3건 |
| `2e33d735` | feat(structure-hunter): unit 1 — 실루엣·동시 등장 상한·덱 편입 |

GitHub `main` 푸시됨. **GitLab 미러는 미완** — SSH 22 포트 타임아웃(망 문제). 복구되면 `git push gitlab main:refs/heads/master` 한 줄.

## Implemented

- **`Enemy_Heartseeker`(마음사냥꾼)** — 방어유닛을 조준하지 않고 마음(골 타워)으로 직진해 때린다. `targetFactions = DefenderCore | BlockingHazard`
- **도발 면역이 저작에서 파생된다** — 유닛 비트가 없으니 `AggroStateSystem` 이 `Aggroed` 부착을 막는다. unit 0 은 이 동작에 **코드를 한 줄도 쓰지 않았다**
- **`AttackUnitData.maxPerWave` 신설**(0 = 무제한) — 종류별 동시 등장 상한. 기존 적 12종 에셋 무편집
- **`WavePatternGenerator.ClampGroupCounts`** 순수 함수 + 호출 2곳(일반 웨이브 · 보스 호위)
- **실루엣 구분** — `back`(등짐) + `helmet` + `top`. `back` 슬롯을 쓰는 적은 13종 중 이것뿐
- **라이브 덱 7종 편입**(6 + `Endless`) + `waveSeed` 20260801~07 → 20260811~17 재기준

## Key Files

- `Assets/_Project/Data/Enemies/Enemy_Heartseeker.asset` — 저작 전부
- `Assets/_Project/Scripts/Data/AttackUnitData.cs` — `maxPerWave`(왜 필요한지 주석에 있음)
- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — `ClampGroupCounts` + 호출 2곳
- `Assets/_Project/Tests/EditMode/WavePerTypeCapTests.cs` — 클램프 6케이스
- `docs/spec/structure-hunter-enemy/{README,0_...,1_...}.md` — 계약·계측·결정

## Verified

- EditMode **2117 / 실패 0 / 의도적 스킵 3** (신규 6개 포함, 기존 웨이브 테스트 전량 그린)
- **라이브 계측**(unit 0): 마음사냥꾼 38,457 프레임 — 도발 부착 **0** · 방어유닛 겨눔 **0** · 골 타워 겨눔 **5,605** · 방벽 HP 500→175. **음성 대조군**(같은 판 일반 적) 도발 **9,001** · 방어유닛 겨눔 **24,453**
- **생성 플랜 카운트**(unit 1): 라이브 7덱 × 100웨이브 → 마음사냥꾼 등장 109웨이브 **전부 2기 이하**, 상한 초과 0, 웨이브 8 이전 등장 0
- 실루엣: 색을 완전히 제거한 오프스크린 렌더에서 Basic·Kindler·마음사냥꾼이 윤곽만으로 구분됨
- 콘솔 신규 에러 0 · `.cs` 변경은 unit 1 에만(unit 0 은 0줄)
- ❌ **사용자 Play 체감 확인 미완** — 웨이브 8 이후 실제 등장·골 압박은 아직 사람 눈으로 안 봤다

## Notes (되돌리면 안 되는 것)

1. **마스크에서 `BlockingHazard` 를 빼지 말 것.** 「거점만」을 문자 그대로 구현하면 완전 봉쇄에서 영구 교착된다 — 봉쇄 해소 수단이 「적이 벽을 부순다」인데 이 적이 그 안전망 밖으로 나간다(연결성 검사는 없다). 방벽은 `Factions.AnyUnit` 에 없어 **도발 면역은 그대로**다.
2. **`engageMovement = Halt` 를 `Advance` 로 바꾸지 말 것.** 골 셀에 진입하면 `PastGoalTag` 가 붙어 **때리기 전에 사라진다**.
3. **`ClampGroupCounts` 가 rng 를 소비하게 만들지 말 것.** 상한을 저작하지 않은 덱의 웨이브 스트림까지 흔들려 6개 맵 난이도가 조용히 바뀐다.
4. **「막을 수 없다」는 수사이지 사양이 아니다.** 사양은 «가디언으로 유인할 수 없다» 하나다. 방벽·CC·감속은 통하며, 그게 이 적을 상대 가능하게 만든다. 밸런싱할 때 결함으로 읽지 말 것.
5. **「골 락 금지 가드」는 없다.** `battle-structures` unit 0 이 그 예외를 제거했고 `AttackSystem` 에 `GoalTowerTag`/`StructureTag` 참조가 0건이다. 계측의 «고착 0» 은 관측이지 보장이 아니다.
6. **`stabilityDamage` 는 이 적에게 죽은 값**이다(`canSiege` 경로가 읽기 전에 빠져나간다). 골 압박은 근접 피해에서 나온다.

## 계측 하네스 함정 (다음에 라이브 검증할 때)

- `TryBeginDefenderDeployment` 는 **시작만** 한다. `ActivateDeployedDefender(cell, entity)` 를 이어 부르지 않으면 유닛이 `PendingDeployment` 로 남아 **적이 조준도 공격도 하지 않는다**. 엔티티 쿼리에는 잡히므로 정상으로 오인하기 쉽다 — 이 함정 때문에 3회차까지의 «도발 0» 이 **공허한 0**이었다.
- **음성 대조군을 같이 세지 않으면 «0» 은 «기능이 죽은 판» 과 구분되지 않는다.**
- **골 타워 엔티티는 `StartBattle` 이후에 생긴다**(Placement 에서 조회하면 0).

## Follow-up

`docs/spec/README.md` 의 Follow-up Backlog 참조 — 이 spec 항목은 그쪽으로 이관했다.
