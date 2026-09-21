# Defender Deploy Phase — 「배치 중」을 방어유닛 행동의 최상위 페이즈로

상태: **완료 2026-09-21** — 사용자 Play 확인(“문제 없음”). 커밋 `3b1992a9`(페이즈) · `d28be037`+`f5b37a37`(UnitAi 레이어). 남은 것: PlayMode lane · 골든 재베이크 · 푸시 — `5_handoff_summary.md`

## 검증 질문

**「배치 중」이 sim 이 소유한 단일 페이즈(비행 → 착지 → 배치 모션 → 활성화)로 존재하고, 그 길이가 저작 초가 아니라 배치 모션 자체에서
나오며, 공격·캐스트·피격·타겟팅·배치 스킬이 전부 그 페이즈 하나를 읽어 «배치가 끝나기 전엔 아무 일도 없다»가 성립하는가.**

## 배경 — 왜 지금 깨져 있나 (2026-09-21 조사)

방어유닛에는 적의 `EnemyAiState` 같은 단일 행동 상태가 없다. 「배치 직후 잠깐 멈춘다」가 세 갈래로 따로 존재한다:

| 층 | 실체 | 시계 |
|---|---|---|
| `PendingDeployment` 태그 | 12개 시스템 쿼리 `WithNone` — 공격·캐스트·피격·타겟·이동·오라… «판에 없는 것» | UI 코루틴 `RunDeployment` 이 `deploymentDuration`(0.45) 을 `WaitForSeconds` 로 재서 `ActivateDeployedDefender` 호출 |
| `AttackState.cooldownRemaining = deployDelaySec` | 공격 START 만 | attack-hit-delay unit 2 (전유닛 0 → 2026-09-17 ccec4a1d 가 애니 길이로 채움 = 4번째 사본) |
| placement-aura `Sleep` | 행동 불가(CC) | Effects |

그리고 **비행이 배치 창을 다 먹고 있었다**: `DragSwaySettings` 가 드롭 비행을 `min(dropTotalSeconds, deploymentDuration)` 으로 클램프해
둘 다 0.45 → 착지 = 활성화 프레임. 배치 모션은 착지에 시작되므로 보호 구간이 **원래부터 0초**였다. 사거리에 적이 있으면 첫 틱에
공격 사건이 나고 `PlayAttack` 의 `SetAnimation` 이 트랙 0 의 배치 원샷을 덮는다(실드셔틀 제보 2026-09-21 — CH4 가 아니라 구조 문제).

## 사용자 결정 (2026-09-21)

- **명시 초를 두지 않는다.** 배치 모션이 지정돼 있으면 그 모션 길이가 배치 페이즈 길이다. 없으면 0. 예외·대체 모션 끼워 넣기 금지.
  → `deploymentDuration` · `deployDelaySec` · `placementSkillDelay` **은퇴**.
- **파츠형 23유닛의 `Hit` 은 배치 모션으로 인정**(0.97s).
- **배치 UX 무관 통일** — D&D · 탭 · 스크립트(`PlaceDefenderAs`) 전부 같은 페이즈를 탄다. 경로별 예외 없음.
- 배치 스킬 = **배치 페이즈가 끝나는 엣지**(현행 `JustDeployed` 규칙 유지). 새 공통 초 없음.
- 비행 중 공격은 **계속 불가**(공중 유닛은 판에 없다). 비행은 페이즈의 첫 단계다.
- 기존 구조에 끼워 맞추지 말고 **로직–아키텍처 분리** 로 간다. 이 페이즈 판정이 이후 방어유닛 autobattle AI(적 FSM 의 대응물)의 첫 자리다.

## 작업 단위

| 파일 | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 데이터(가산) | `0_motion_length_and_retire.md` | `DefenderUnitData.DeployMotionSeconds`(파생 getter) + 테스트. **삭제는 없음**(critic H-4 — 삭제는 unit 2 와 한 커밋) |
| 1 | sim 페이즈 | `1_pending_phase_system.md` | `PendingDeployment{stage, remaining}` · `DeploymentActivationSystem`(Units, `UpdateAfter(BossPeriodicTrigger)`) · `DefenderActivatedEventsSingleton`(31번째) |
| 2 | 배선 + 은퇴 | `2_bridge_and_placement_wiring.md` | 진입 3경로 통일 · 착지 신호(유실 경로 포함) · 드레인(`_running` 앞) · 저작 초 3개 은퇴 · 테스트 7곳 교체 |
| 3 | 우선순위 자리 | `3_action_phase_resolver.md` | `UnitActionPhase`(Combat, 순수) — Attack/Movement 의 lock 식 추출(동작 무변) · 뷰 원샷 순서 |
| 4 | 검증 | `4_verify.md` | EditMode · 실에셋 길이 표 · Play 계측(실드셔틀 적 있는 배치) · 골든/PlayMode |

handoff 는 `5_handoff_summary.md`.

## Feature-wide 계약

1. **페이즈는 sim 이 소유한다.** `PendingDeployment` 는 태그에서 데이터 컴포넌트가 된다: `stage ∈ {InFlight, Deploying}`, `remaining`(초, 배틀 시간).
   `Deploying` 은 `DeploymentActivationSystem`(Units)이 틱하고, 끝나면 **같은 ECB** 에서 태그 제거 + `JustDeployed` 부착 + 활성화 이벤트 enqueue.
   → 아키 문서 §4.4 「`JustDeployed` 부착과 `PendingDeployment` 제거는 연속 두 줄」 위험이 구조적으로 사라진다.
2. **`InFlight` 는 시계가 없다.** 비행은 프레젠테이션 시간(unscaled)이라 sim 이 재지 않는다. 착지 프레임에 브리지 `LandDeployedDefender(entity)`
   가 `Deploying` 으로 전이시키며 `remaining = DeployMotionSeconds`. `PendingDeployment` 의 **기본값 = InFlight**(기존 테스트가 태그만 붙이던
   관용구가 그대로 «배제»). ⚠ **영구 InFlight 는 안전장치가 아니라 좀비다**(critic H-3): 비행이 착지에 못 가는 실제 경로 — `AbandonDismount`
   (바인딩 붕괴) · `FinishDismountsInstant`(OnDisable) — 에서도 `Land` 를 부른다. 비행을 끝내는 모든 출구 = Land. 종전엔 `RunDeployment` 의
   독립 시계가 이걸 가려 주고 있었다.
2-1. **Deploying 중 사망**: `DeadTag` 가 붙은 pending 유닛은 활성화 시스템이 같은 ECB 에서 `PendingDeployment` 도 걷는다(활성화 이벤트는 없음).
   브리지 취소 자격(`_cancellableDeployments`)은 사망 드레인이 정리한다.
3. **길이는 데이터가 답한다.** `DefenderUnitData.DeployMotionSeconds` = 명시 슬롯만 — 스프라이트 `spriteMotions.deploy`(frames/fps) 또는
   Spine `skeletonDataAsset`+`deployAnimation`(`Animation.Duration`). **뷰 인스턴스·폴백 체인(drag→attack→idle) 을 보지 않는다.** 헤드리스에서
   같은 값 → 결정론 유지. 없으면 0 = 착지 즉시 활성화.
4. **진입 3경로 = 한 함수.** `TryBeginDefenderDeployment`(D&D·탭) 와 `PlaceDefenderAs`(스크립트·테스트) 모두 `CreateDefenderEntity` 에서
   `PendingDeployment` 를 단다. 비행이 없는 경로는 생성 직후 브리지가 스스로 `LandDeployedDefender` 를 부른다(= Deploying 부터).
   `PlaceDefenderAs:7738` 의 즉시 `TriggerDeploymentOnPlaceSkill` 은 **삭제**(critic M-6 — 남기면 스크립트 경로만 배치 순간에 스킬이 나가고,
   pending 이 붙은 채라 `ExcludePendingDeployment` 로 자기 실드가 자기를 뺀다). **재배치는 이 spec 밖** — 자기 시계(`RelocationSettings.redeploySeconds`)
   를 이미 기다린 뒤 동기 `ActivateDeployedDefender` 로 활성화하는 현행 유지(동작 무변 · 테스트 무수정). 재배치도 모션 길이를 태울지는 후속 후보.
5. **활성화의 부수 효과는 드레인이 한다 — `_running` 게이트 앞에서.** 배치는 `StartBattle` 전에도 일어나는데(`PlacementPhaseView:294` 가
   StartBattle 을 부르기 전) 지금 브리지 드레인은 전부 `if (!_running) return;` 뒤에 있고 채널 싱글턴도 StartBattle 안에서 만든다(critic H-1).
   → 이 채널은 **`BeginPlacement` 에서 생성**하고 드레인은 `TickBattleFrame` 의 `_running` 게이트 **앞**에 둔다. 고정스텝 하네스(`StepOneTick`)
   도 같은 본문을 지나므로 동일. 드레인이 종전 `ActivateDeployedDefender` 의 장부(취소 유예 종료 · `_onPlaceTriggeredEntities` · 카메라 셰이크 ·
   효과 타일)를 처리. 첫 줄은 트레이스(`TraceChannel.DefenderActivated = 21`, append-only). 동기 진입점은 **이름 그대로**
   `ActivateDeployedDefender(cell, entity)` 로 남긴다(sim 시스템의 세 줄을 EntityManager 로 그대로 — 테스트 6곳·재배치 무수정).
6. **우선순위의 자리는 순수 함수 하나 — 오늘은 추출, 내일은 AI 상태.** `UnitActionPhase.Resolve(actionLocked, swinging) → Locked > Swinging > Free`
   (`Wassup.UnitAi` 로직 레이어 — 엔진 참조 불가 asmdef). Attack·Movement 의 `actionLocked`/`locked` 식이 이 함수를 부른다 — **동작 무변**.
   `Deploying`·`Dead` 는 이 함수의 인자가 아니라 **쿼리 랭크**다: `WithNone<PendingDeployment>` 14곳(+`EcsSkillContext` 2곳) = «존재 배제»(피격·타겟
   후보까지), `DeadTag` = 파괴 대기. 표 전체(Dead > Deploying > Locked > Swinging > Free)는 문서 계약이고 코드는 각 랭크의 실제 소비 지점을 가리킨다.
   critic M-9: HazardCast 에 CC 락을 새로 여는 것·Attack 루프에 Dead 게이트를 더하는 것은 **동작 변경**이라 이 spec 밖(후속 후보). 저장 상태
   (`DefenderAiState`)는 유닛이 스스로 전이하는 행동이 생길 때 이 함수 자리를 승격(적 FSM 선례).
7. **뷰는 같은 순서를 미러.** `UnitView` 원샷 우선순위 Death > Deploy > Attack — 배치 원샷 진행 중 `PlayAttack` 은 큐(Spine `AddAnimation`,
   스프라이트 `_oneShot` 큐 1칸). sim 이 막으니 2중 방어이며, 뷰가 sim 순서에 기대지 않게 하는 것.
8. **은퇴(unit 2 와 한 커밋)**: `deploymentDuration`(+ `DragSwaySettings` 드롭 클램프·주석 — 비행은 이제 페이즈의 일부라 pending 을 넘칠 수 없다) ·
   `deployDelaySec`(+ `UnitStatImportDto` 컬럼 · 시트 push) · `placementSkillDelay`. ccec4a1d 가 넣은 26유닛 값은 필드 삭제로 함께 사라진다.
   순찰병 생성(`:8711`)도 `deployDelaySec` 을 읽지만 값이 0 이라 동작 무변(순찰병은 pending 이 없어 소환 즉시 공격 — 종전과 같다).
8-1. **길이의 출처 = 재생의 출처.** 뷰 `PlayDeploy` 도 명시 슬롯만 튼다(폴백 체인 drag→attack→idle 은퇴, critic M-8) — 길이 0 인 유닛이 폴백
   원샷을 재생하면 sim 근거 없는 연출 지연이 생긴다. 백엔드 판별은 `SpineUnitPool.TrySpawn` 과 같은 게이트(`HasIdle`)를 쓴다(critic M-7).
8-2. **취소·퇴근 거부 창이 넓어진다(사용자 확인 대기).** `TryCancelPendingDeployment`(전액 환불 되돌리기, 기본 off)와 `RetireDefender` 거부 구간이
   0.45s → 모션 길이(최대 스나이퍼 1.67s). 「놓고 1.6초 뒤 전액 환불」은 플레이어가 겪는 규칙이라 워크플로 0 에 따라 확인 후 착수.
9. **슬로모·정지 일관**: `Deploying` 은 배틀 스케일 dt 로 틱(그룹 RateManager) — 배치 모션도 `_battleScale` 로 재생되므로 둘이 같이 늘어난다.

## 배치 모션 길이 정본 (2026-09-21 실측 · 리그 재수출 시 갱신)

| 유닛 | 모션 | 길이 |
|---|---|---|
| 파츠형 23종(캐논·배스티온·아처 …) | Layer Lab `Hit` | 0.97s |
| 말파이트 · 넉백머신 · 이쑤시개 | 시트 deploy 16f@24 | 0.67s |
| 실드셔틀 | CH4 `skill` | 0.67s |
| 스나이퍼 | CH2 `drop` | 1.67s |
| 소환사 | CH1 `attack1` | 1.33s |
| 순찰병 | (없음) | 0 |

테스트는 이 수치를 못박지 않는다(critic M-10) — 에셋 lane 은 불변식(「`deployAnimation`/시트 deploy 가 있으면 > 0, 없으면 0」·「뷰 resolver 가 트는 트랙과 같은 트랙의 길이」)만 단언한다.

## 이 spec 이 확장하지 않는 것

| 요소 | 왜 |
|---|---|
| placement-aura 의 Sleep | 배치가 «끝난 뒤» 오는 Locked 층. 성격이 다르다 |
| 적(공격 유닛) | 배치가 아니라 스폰. 적 FSM 은 별도 |
| `DefenderAiState` 저장 상태 | 계약 6 — 필요가 생길 때 |
| 재배치(relocation) | 자기 시계 + 동기 `ActivateDeployedDefender` 현행 유지 — 동작 무변(모션 길이 적용은 후속 후보) |

## 파이프라인 커버리지

플레이 오브젝트 신설 없음. 방어유닛 아키타입의 **데이터 SO**(파생 getter · 필드 3 은퇴) · **스폰 게이트**(`PendingDeployment` 데이터화) ·
**시스템**(Units 1개 신설) · **채널**(31번째) · **브리지 드레인**(활성화) · **View**(원샷 순서) 정거장이 바뀐다. 씬 wiring N/A(싱글턴은 기존 채널과 같은 부트 경로).

## 후속 후보

- `DefenderAiState` 컴포넌트 승격 — 유닛 자기주도 전이(재장전·후퇴 등)가 생길 때.
- 재배치도 모션 길이를 태울지 — 지금은 자기 시계(`redeploySeconds`) 뒤 동기 활성화(defender-relocation 소유).
- 비행 시간을 sim 이 아는 형태(결정론 리플레이가 배치 타이밍까지 담아야 할 때).
- `_onPlaceTriggeredEntities`(브리지 HashSet) 와 `JustDeployed` 의 1회 보장 이중 구조 정리.
- `HazardCastSystem` 에 CC 액션락 적용(지금은 캐스터가 잠들어도 캐스트한다 — 별도 결정) · `AttackSystem` 통합 루프의 `DeadTag` 게이트(사망 프레임 START 억제).
- `UnitActionPhase` 에 Deploying·Dead 인자 합류 — 위 둘이 결정되면.
