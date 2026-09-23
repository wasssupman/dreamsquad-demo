# battle-core-rebuild — 전투를 ECS 에서 순수 C# 「전투 코어」로 옮긴다

상태: **승인·진행 중 2026-09-23** — **조각 A 완료**(리뷰 전건 APPROVE): unit 0(main `4caee406`) · unit 1(`384e869b`·`dc0baa41`) · unit 2(`d5c16070` + 수정 `843b786a`) · unit 3(`0ae6b5cd`) · unit 4(`50ec0dae` + 수정 `0501630b`·`aedf3f7b`·`d12423bd` + 거점 스폰 `55688ef5` + 경로 방패 `7abfec27`) · 헤드리스 lane(`c74825be`).
**조각 B 구현 완료 — unit 5a·5b·5c**(5a `aa16ee9d`~`1b7e033b` · core-reviewer APPROVE. 5b — 드래그 배치·퇴근·제출 입력 · HUD 6 · 맵 오버레이 · 예고선 · 카메라 프레이밍. 5c `ebf055f4`·`605f15a7`·`2aad2ef4` + 코어 사건 `+DefIndex` — 판 종료 → 결과 화면 → 제출 게이트 · 전투 사운드 3 · 모드 진입 3단 · dev 토글). 이동 튜닝 정의표(`fae42944`)로 골든 11종 Unity 재굽기.
검증: Unity EditMode 코어 lane **371/371** · 새 PlayMode lane **21/21** · 헤드리스 3종(build 0 · test 360 · Check build 0) · Play 육안(결과 화면 · 사운드 3종 실계수 · 콘솔 에러 0).
장부 잔량: `bridge-methods` 미정 **51**(128 → 98 → 64 → 59 → 51, 6a 몫 8행 닫힘) · `bridge-fields` **0**(91행 전부 분류). 브랜치 `rebuild/battle-core`. **다음 = 사용자 플레이 1차**(질문 = 배치·이동·전투·점수·종료의 손맛 · 부재 목록은 `5c` 의 「아직 안 보이는 것」 표) → `core-reviewer` → **조각 B main 머지**. 모든 커밋은 리드가 클린 export 로 build/test/Check 재실행해 검증한다.

설계 입력: [`docs/plans/2026-09-22-battle-core-rebuild-census/`](../../plans/2026-09-22-battle-core-rebuild-census/) — 6영역 census(약 395행) · 종합(`00`) · 상호 리뷰(`01`·`03`) · 트리거→발동 rev 3(`04`) · 매치 모드 연구(`05`) · **계획 완전성 리뷰(`06`, 13건 — 이 rev 2 의 근거)**. 핵심 클래스 UML 은 [`class-diagram.md`](class-diagram.md), 매치 모드는 [`match-mode-design.md`](match-mode-design.md).
선행 spec 처리: `battle-sim-extraction` **M0 완료·M1+ 폐기**(후계 = 이 spec) · `battlebridge-dissolution` **흡수** · `ecs-lifecycle-teardown` **은퇴**.
선행 완료(2026-09-23): **CLAUDE.md 범위별 재편**(상태 라인 · `[옛 전투]` 꼬리표 · 「새 전투 코어 — 절대 제약」 6항). `AGENTS.md` 는 symlink 라 자동 동기.

### 사용자 결정 기록 2026-09-24 (투사체)

① 시전자 착탄 효과는 **그 시전자가 쏘는 모든 탄**에 적용 ② 같은 부여 겹침 = **합, 상한 있음** ③ 부여 어휘는 **기존 착탄 효과 그대로**(화염·출혈 스택 등, 신설 없음) · F30 = **(a) 고친다**(출처 = 발사자). 감사에서 발견한 라이브 결함: 발사 명세 선정 규칙 enum 번호 어긋남(12 중 11 오독) → 수정 중.

### 사용자 결정 필요 — 조각 C(효과) 착수 전 · **1건**

**같은 적이 쏘는 디버프가 이제 안 쌓인다**(F30). 지금은 발사마다 새 슬롯이 생겨 **곱으로 누적**된다 — 출처로 투사체 개체를 보내서 생긴 라이브 결함이다. 고치면 곱누적이 상시 배율이 되어 **킨들러류 원거리 적이 눈에 띄게 약해지므로** 수치 재조정과 한 묶음이 된다. 이번에 고칠 것인가, 결함을 박제하고 뒤로 미룰 것인가? 답이 오기 전에는 **현행(투사체 출처)을 박제**한다.

**에이전트가 정한 것 3건**(코드 구조·축이라 질문 대상이 아니다). ⑴ 불 스택 규칙을 **저작 자산별로** 가른다 — 지금은 종류당 한 벌이라 드래곤을 올리면 킨들러가 같이 올라간다. **값은 오늘과 같게** 저작하므로 판은 안 바뀐다. ⑵ 한 몸에 상태가 여럿일 때 **무엇이 이겨 보이나**는 6c 플레이에서 확인한 뒤 데이터로 굳힌다(옛 코드의 암묵 순서를 베끼지 않는다). ⑶ 존 효과의 **진영 축을 연다**(제약 8 이 이 하드 게이트를 명시 지목했다) — 라이브 저작은 오늘과 같은 값이다.

**고지 1건**(질문이 아니라 제약 13 적용의 결과). **레드불을 스치듯 지나가도 먹게 된다** — 지금은 같은 칸이어야 먹는데, 제약 13 이 예외를 배치 판정 하나로 못박았으므로 픽업도 「칸 반폭 + 내 몸」 자를 쓴다. 판정이 넓어지며, 체감 확인은 6c 플레이 항목이다.

### 사용자 결정 필요 — 조각 D(트리거→발동) 착수 전 · **4건**

전부 **플레이어가 겪는 규칙**이다(CLAUDE.md 작업 지침 0). 답이 오기 전에는 각 항목의 **기본값**으로 구현하고 해당 unit 의 「이식 제외」 표에 「사용자 답 대기」로 적는다.

① **같은 카드를 두 장 붙인 유닛이 죽으면 몇 번 터지나?** 지금은 **죽으면 한 번, 퇴근시키면 두 번**이다 — 같은 규칙인데 두 문이 다르다. 종류가 다른 카드는 사망에서도 각각 다 터지므로, 갈리는 것은 **같은 카드 여러 장**뿐이다. 그 한 번 제한은 옛 사망 이벤트가 값 한 벌만 실을 수 있어 생긴 **기계의 한계**이고(퇴근 쪽 코드가 그렇게 적어 뒀다) 새 코어엔 그 한계가 없다. **기본값 = 사망도 두 번**(계약 2 「땜빵은 의도만 옮긴다」). → 7b · `rules.md` E4

② **보스가 도약할 자리를 고르는 자가 사각형인 채로 둘까?** 판정은 전부 원 자로 통일됐는데(제약 13) 밀집도 계산만 아직 사각 자다. 원으로 바꾸면 **보스가 내려앉는 칸이 달라진다** = 밸런스 변경. **기본값 = 현행 사각 자 박제**(폭탄맨 폴백을 unit 3 이 같은 이유로 보류한 것과 같은 처분). → 7d

③ **자고 있는 유닛이 주기 스킬을 계속 쓰는 것은 사양인가 버그인가?** 옛 주기 트리거는 행동 잠금(기절·수면)을 **읽지 않는다**(실측). 스펙은 사양으로 썼고 사용자는 **버그로 읽었다**(Play 관측 2026-08-11). 고치면 주기 스킬을 가진 **전원**의 동작이 바뀐다. **기본값 = 현행 박제**. → 7d

④ **「한 발이 반경 안 전원에게」 저작 축을 살릴까 은퇴시킬까?** 발사 명세에 그 손잡이(`FanOutToAllCandidates`)가 있는데 **라이브에서 켠 곳이 0건**이고 소비자도 없다. **기본값 = 은퇴**(정의표·`configHash` 에서 제거). 살리려면 소비처를 unit 7 에서 배선한다. → 7a · unit 3 이월

**에이전트가 정한 것 3건**(코드 구조·축이라 질문 대상이 아니다). ⑴ 조각 D 를 **7a·7b·7c·7d 넷**으로 나눈다 — 복사·적응 대상이 실측 16,900줄이고 카드 화면만 9,588줄이다. ⑵ 저작 enum(트리거 10 · 페이로드 33)을 **코어가 같은 번호로 미러**하고 pin 테스트가 대조한다(6a 의 `CoreSkillEnumPinTests` 선례) — 어셈블리가 갈려 컴파일러가 못 잡는다. ⑶ 「쓰기는 발행으로만」(S20)의 강제 수단을 asmdef 에서 **`IntentApplier` 단일 표면 + 아키텍처 테스트**로 옮긴다.

**고지 2건**(질문이 아니라 선행 결정의 결과). ⑴ **재배치(유닛을 다른 칸으로 옮기기)는 안 옮긴다** — `defender-clock-out/0` 이 2026-08-13 팀 리뷰로 진입구를 껐고 퇴근이 그 자리를 대신한다. 라이브에 없는 기능이라 되살리는 것은 새 기능이다. ⑵ **마메모의 「웨이브 회전 정지」에 해당하는 훅은 코드에 없다**(rev 3 §8 확인 대기 해소) — 실제 기제는 웨이브 생성기의 보스 호위 후처리이고 unit 4 가 이미 이식했다.

> **반영 시점(1프레임 지연)은 질문이 아니다.** rev 3 §4 가 이 축을 **「변경 없음」**으로 닫았다 — 지연을 만든 것은 채널이 아니라 **단계 순서**이고, 새 코어가 같은 순서를 쓰므로 규칙이 그대로 계승된다.

## 상위 목표

**현재 게임이 실현한 기획(규칙과 흐름)을 Mono 전제에서 새로 설계한 전투 코어로 옮긴다.** 완벽 재현은 목표가 아니다 — 원래 로직과 흐름을 최대한 따르되, 땜빵·우연은 상세를 옮기지 않고 의도만 옮긴다. 세세한 차이는 이식 뒤 사용자가 플레이하며 고친다. **`BattleBridge` 는 데모용 임시 수단이었고 이번에 소멸한다** — 어디로도 흩어지지 않고, 그 일의 담당자들이 각자 갖는다.

- 규칙의 정본 = census. 구조의 정본 = 이 spec + UML. 기존 ECS 구조는 「왜 그 규칙이 필요했나」의 증거로만.
- 산출물 = 엔진을 모르는 `Wassup.BattleCore` + Unity 층(정의표 빌더·드라이버·뷰 풀·입력·HUD). 서버·네트워크 없음.

## 검증 질문

> **같은 modeId+seed 로 헤드리스 EditMode 에서 판이 끝까지 돌고, 사용자가 실기기에서 플레이했을 때 「지금 게임과 같은 게임」이라고 느끼는가.**

부수 질문: 전투 코드 어디에도 `Unity.Entities` 가 없고, `manifest` 에서 Entities 계열이 사라져도 asmdef 4개가 초록인가.

## 작업 단위 (세로 조각 순 · rev 2)

| 조각 | 파일 | 작업 구분 | 목적 |
|---|---|---|---|
| **0 환경** | `0_transition_environment.md` | **전환 환경** (완전성 리뷰 13건의 자리) | ① **씬**: 새 `BattleCoreScene.unity` — 옛 `BattleScene` 무변, 뷰 풀 다툼 없음 ② **브랜치·워크트리**: `rebuild/battle-core` 브랜치 + 전용 워크트리(main 은 승인 push 하나로 전부 실린다) · main 머지는 조각 B·D·E 경계에서 리뷰 후 ③ **동결 장치**: `.githooks/pre-commit`(`core.hooksPath`) 이 `Scripts/Battle/**`·`Bridge/**` 스테이징을 `[old-battle]` 태그 없이 거부 + 착수 대기 spec 4건 판정(카메라·squad-slots = 계속 / wide-board-content·heart-stress-axis 12 = 새 코어에서) ④ **리뷰 도구**: `ecs-review-detector` 에 `Scripts/BattleCore/` 경로 추가 + `core-reviewer` 에이전트(코어 제약 6항 체크리스트) — 첫 커밋 전 ⑤ **귀속표 3종**: 브리지 메서드 369 + 직렬화 필드 91 + **브리지 밖 규칙 보유자 10**(`TilemapMapView`·`GameManager`·`DreamcatcherHandController`·`DraftController`·`SkillLoadoutController`·`CostRuntime`·`PlacementInput`·`SkillRuntime`·`PlacementCooldownRuntime`·`MatchTally`; `TimeManager` 는 의도된 예외) → 담당자 / 삭제, 기계 대조 스크립트 동반 ⑥ **도구 10개 처분표**(6개가 삭제 예정 폴더 안): 재작성 시점을 조각 C·D 앞으로 ⑦ **규칙 분류표**(census 「코드에만 있는 규칙」 ~100건 → 이식 필수 / 보류 / 제거) ⑧ **서버 payload 판정**: v1 제출은 `KillScoreTimed` 만, `WaveClear`·`TimeAttack` 은 `submitsReport=false`(로컬) — 서버 API 확장은 후속 후보 ⑨ `SimEntityId` 센티널 확정(Match 호스트 0 · 유닛 1~ · None = -1) |
| **A 뼈대** | `1_skeleton_and_harness.md` | 뼈대 + 하네스 | `Wassup.BattleCore` asmdef · `MatchDefinitionBuilder`(SO→정의표, **`configHash` 의 새 소유자 = `MatchDefinition`**) · `BattleWorld`/`Unit`/틱 골격 · 커맨드+receipt · `EventBus` · **새 코어 헤드리스 러너**(옛 러너와 병존 — A/B 는 둘이 동시에 있을 때만 가능) · 새 코어 골든(`LegacyTraceV0` 포맷) |
| | `2_map_and_movement.md` | 맵·이동 | `MapSnapshot` 수신 · 흐름장 슬롯 · 통행층별 NavGrid · 장애물 재빌드 · 이동 결정 순서 · 평활화·충돌·분리 · 감지·어그로·도발 이동 · 골 도달 · 순찰 |
| | `3_combat.md` | 전투 판정 | 공격 루프(START/RESOLVE) · 도달 산식(제약 13) · 타겟팅·락·히스테리시스 · 방향 도형 · 투사체 궤적×페이로드 · 발사 명세 · 피해·실드·킬 귀속 · 사망 2단계 · UnitAi 상태 · `AttackMod` 축 |
| | `4_match_owners_and_mode.md` | 매치 담당자 + 모드 | 담당자 8(`MatchClock`·`WaveScheduler`·`CostLedger`·`PlacementService`·`HeartMeter`·`ScoreLedger`·`HandDeck`·`GimmickHost`) 각자 상태+규칙+틱 단계+이벤트 · `MatchModeData` SO → `ModeDef` · `IMatchGoal` + concrete 3 · **완료 기준에 포함**: `enemy-wave-integration` 스킬 갱신(같은 커밋 의무) · 모드 유효성 테스트(`EditModeAssets` lane) · 덱 타이머 이관은 「모드가 이기고 덱 값 폴백」으로 **조각 E 머지까지** 유지 후 unit 9 에서 제거 |
| **B 첫 플레이** | `5a_driver_and_unit_views.md` | Unity 층 1/3 | `BattleDriver`(**`Build(…, structures:)` 필수** · `TimeManager` 리스 → 틱 발행률) · `ViewOrder` 상수로 정렬 방출 · 유닛/투사체/피해숫자/히트바/오버헤드/도약 뷰 풀 각자 구독 · **직렬화 필드 91 의 새 주인 4분류**(코어 정의표 / 뷰 설정 SO 7 / 씬 배선 참조 / 삭제) · 디버그 도구 2(감지 프로브·장애물) · **새 PlayMode lane `Wassup.Tests.PlayMode.Core`** · 파이프라인 커버리지(유닛·투사체). 상태 FX·빔·VFX·오라 풀은 **사건이 열리는 unit 6·7 에서** |
| | `5b_input_hud_overlay.md` | Unity 층 2/3 | 드래그 배치 입력 → 커맨드+receipt(판정 0) · 퇴근·제출 입력 · HUD 5(읽기 모델) · 맵 오버레이(`AttackReach.InReach` 호출만) · **예고선 = `StructureChoice` 호출만**(M18) · 카메라 재사용 |
| | `5c_match_outcome_and_audio.md` | Unity 층 3/3 | **판 종료 → 결과 화면 → 제출 게이트**(`submitsReport && allowSubmit`, `ReportResult` 시그니처 무변 — 지금은 브리지 안에만 있다) · **전투 사운드 3종**(브리지 안 호출 3건의 새 주인, 클립은 뷰 데이터 SO) · 모드 진입 3단 · 「아직 안 보이는 것」 표 → **카드 없이 판이 돈다 — 사용자 플레이 1차**(질문 = 배치·이동·전투·점수·종료의 손맛) → main 머지 |
| **C 효과** | `6a_modifiers_cc_dot_stacks.md` | 효과 1/4 — 슬롯 | 모디파이어(병합 4축, 전역 번호판 폐기 → **`SlotTag` = (종류, 판별자) 짝** — 판별자를 접으면 옛 슬롯 충돌 버그가 재현된다)·스택(2축 · 임계 규칙은 **저작 자산별**)·군중 제어(런타임 슬롯 **3**; `Slow`·`DoT` 는 저작 토큰)·지속 피해((출처, 원소) 2축)·실드(FIFO · 시간 만료 없음)·최대체력 배율(`Unit.BaseMaxHealth`) · **부여는 큐가 아니라 관문 함수**(옛 3채널 소멸)이되 **반영 시점은 단계 위치가 계승**(F29 = rev 3 §4 「변경 없음」) · **회수 = 슬롯 삭제**(F27·F28·F33 동시 해소) · `ModifierAuthoring` salvage(상한 = `(배율−1)×최대중첩`) · **새 phase 신설 없음** · `DeterminismTests` 확장 |
| | `6a2_projectile_onhit_gate.md` | 효과 1.5/4 — 탄 관문 | **시전자가 쏘는 모든 탄이 시전자의 착탄 효과를 싣는다**(사용자 결정 2026-09-24 ①) · 부여 슬롯(합·상한 ②) · 어휘 신설 없음(③: 화염 = `ApplyStack(Fire)`) · `SpawnRequested` 한 곳에서 접음 · 킨들러 화염·난도질꾼 출혈이 탄에 실리는 첫 자리(unit 3 이월 해소) · 공격 수식자 5종은 같은 슬롯의 다른 종류(실행 unit 7) |
| | `6b_hazards_fields_effect_tiles.md` | 효과 2/4 — 물건 | 존 장판(연속 원 · 감속은 **이동속도 모디파이어** · **진영 축 개방**, 제약 8)·길막(문은 「부서짐」 하나)·장 캐리어(겹치면 가장 강한 값 · 재발행 주기 **재산출**)·효과 타일(적용 + **회수**) · 디버그 커맨드 2 |
| | `6b2_pickups_resignations_gimmick_stacks.md` | 효과 3/4 — 기믹 셈판 | 픽업(제약 13 자로 소비)·라스트런(`ProgressiveStates.LastRun`)·사직서(누적·임계)·열기/피로(피로 누적은 스탯 적용 **뒤** = 1틱 지연 박제) · **기믹 수치를 `GimmickDef` 로**(config 싱글턴 4 소멸) · **`[Periodic]` seam — enum + 호출부** · 디버그 커맨드 3 · 장부: E6(표식 없음 · 사건이 정본)·M3(**완료 — unit 2 가 이미 분리**) · **골든 11종 Unity 재굽기**(조각 C 의 마지막 코어 변경) |
| | `6c_effect_views_and_tools.md` | 효과 4/4 — 그림·도구 | 5a 가 일부러 안 만든 뷰 풀 4(상태 FX·빔·VFX·오라) + 해저드·픽업·사직서 뷰 · 오버헤드 **실드 비율·스택 아이콘**(5a 이월) · 선택 패널 **델타 칩**(5b 이월, 재곱 금지) · 배치 연출 VFX·카메라 흔들기(5b 이월) · 임팩트 소켓 높이(5a 이월) · 방패 걸린 마음의 **부수 피해 제외 소비처**(`EffectEligibility`, unit 4 이월) · **디버그 도구 재작성 3**(tools.md 6·7·9) · 장부 마감 + `check_ledgers.py` exit 0 → **사용자 플레이(조각 C)** |
| **D 트리거** | `7a_binding_core_and_skills.md` | 트리거→발동 1/4 — 바인딩 코어 | **정본 = [`04_trigger_layer_rev3.md`](../../plans/2026-09-22-battle-core-rebuild-census/04_trigger_layer_rev3.md) — rev 2→3 정정 9건(Squad 수명=호스트 · 기믹 per-unit 타이머 · PlacementAura 2바인딩 · SplitOnDeath=OnSlain · fireCap≠lifetime · BFS 직접 재진입만 · AreaBlast 병합 철회 · skillId 은퇴 범위 · 사망 seam 스냅샷)은 7a 의 첫 표** · `BindingDef`/`BindingRegistry`/`TriggerDispatcher` · **정적 라우팅 표 유지**(프리뷰가 드래그 중에 형을 묻는다) · `CoreSkillContext`+`IntentApplier`(S20 을 「단일 쓰기 표면」으로 닫는다) · concrete **33**(형 표 = 제약 13) · seam **6**(`Immediate` append, Cast 없음) · `AttackMod` 5 · 유닛 저작 스킬(적 악몽·배치 스킬·퇴근) · `FanOutToAllCandidates` 재판정(기본값 은퇴) · 장부 **44 → 36** |
| | `7b_card_rules_and_bindings.md` | 트리거→발동 2/4 — 카드의 규칙 | unit 4 가 「효과 자리는 진단 통로로」 남긴 구멍을 닫는다: 부착·시전이 **바인딩을 실제로 붙인다**(동기 트랜잭션 ①적용 ②차감 ③순환) · Squad = **host 유닛 수명**(소멸 ∪ 퇴근) · `CostRate` = 메타 intent `SetCostRegenMul` · PlacementAura **2 바인딩** · 표식 `fireCap 1` + 소멸까지 부착(부착 상한 밖) · `trigger == None` 3장 · 인수인계 = **손패 집합 연산** · `Applicability` 코어 이전(preflight ↔ bake 한 함수) · 회수 = **슬롯 삭제**(6a 축 재사용) · 장부 **36 → 28** |
| | `7c_card_views_and_hand_ui.md` | 트리거→발동 3/4 — 카드의 화면 | **이 spec 최대 덩어리(실측 9,588줄)**: 손패 뷰 · 드래그 슬롯 · 포커스 락온 · 각성 게이지·항아리 독 · 카드면·문안(**formatter 가 이긴다**) · 흡수 비행 · 타겟 화살 · 검사 패널 · **부착 범위 링**(반경은 코어 `RangeCatalog` 가 준다 — 뷰 재계산 0) · 선택 패널·오버헤드 **부착 카드 줄**(6c 이월 2행) · 표식 뷰 · 입력 → 커맨드(판정 0) · **장부 잔량 무변(28)** |
| | `7d_gimmicks_boss_split_and_ledger.md` | 트리거→발동 4/4 — 기믹·보스·분열·장부 | 6b2 가 남긴 「무엇이 언제 그것을 놓는가」: 기믹 4(유닛 호스트 per-unit 타이머 · **필터는 기믹마다 다르게 현행** · 레드불만 Match 주기 · 사직서 드랍=사망 seam + **운석 barrage**) · 보스(위협 귀속 C25 이월 · `fireCap 1` 은 궁극기만 · 착지 선정 자) · 분열 `OnSlain` · 호접몽 · 순찰 소환 · **마메모 훅 없음 확인**(웨이브 호위 후처리가 그 기제 — rev 3 §8 해소) · 디버그 도구 2(tools 10·11) · **장부 28 → 0 + `check_ledgers.py` exit 0 = 조각 E 진입 조건** · **재배치는 이식 제외**(진입구가 이미 꺼져 있다) → 사용자 플레이 2차 |
| **E 전환** | `8_view_migration.md` | 뷰·규칙 보유자 이전 | Entities 누수 **28** 파일(Presentation 15·UI 8·Data 3·Core 1·Skills 1·Installer 1) → `SimEntityId` — **키 치환이 아닌 3곳**(`SpineUnitPool`·`QuadUnitViewPool`·`DcAuraVisualPool` 의 매 프레임 생존 폴링) 은 계약 「모든 소멸은 소멸 이벤트를 낸다」 + 코어 `IsAlive` 노출(자가 치유 + 경고 로그) 둘 다로 · 브리지 밖 규칙 보유자 10 → 담당자로 · 브리지 소멸(귀속표 대조 스크립트 = 남는 선언 0) · `object-pipeline-map.md` 재작성 |
| | `9_ecs_removal.md` | ECS 제거 | asmdef diff 명시(`Wassup.Runtime` + 테스트 3 의 `Unity.Entities`·`Entities.Graphics`·**`Unity.Transforms`** 제거) · 패키지 제거(전이 의존 `serialization`·`scriptablebuildpipeline` 확인) · `Battle/`·`Bridge/` 삭제 · 테스트 148 파일 정리(옛 PlayMode lane 은퇴) · 도구·리뷰 도구 은퇴(`ecs-reviewer`·`two-track-review`·훅 2) · **문서 목록**: `battle-core-architecture.md` §2~§10 · `test-procedure.md` · `enemy-movement-algorithm.md` · `map-wave-balancing.md` · `score-formula.md` · `lessons/01·04` · 스킬 3(`unity-vfx-integration`·`unity-feature-wiring`·`enemy-wave-integration`) · CLAUDE.md 옛 절 삭제 + 코어 절 승격 · `ingame-flow.md` 1축 문면(모드별) |
| | `10_handoff_summary.md` | 인계 | — |

**조각의 「초록」 정의**: 조각 A = EditMode core+assets lane 초록. **조각 C 는 6a·6b·6b2 까지 EditMode 초록이고, 6c 가 그 효과를 화면에 올리므로 사용자 플레이가 붙는다**(초안의 「조각 C = EditMode 만」은 뷰 풀이 unit 6 안에 들어온 뒤로 더 이상 맞지 않는다). 조각 B·D = + 새 PlayMode lane 스모크 + 사용자 플레이 확인(질문은 그 조각의 「아직 안 보이는 것」 표를 뺀 범위로 — 5c). 조각 E = 옛 lane 은퇴 후 새 lane 만으로 초록 + Entities 0건.
**진행 규칙**: 조각 안의 unit 은 전부 구현한 뒤 한 번에 테스트한다. 각 unit 문서 하단에 **「이식 제외」 표**(일부러 안 옮긴 것 + 이유). 플레이 중 이상하면 그 표부터 본다. **장부 소진**: `bridge-methods.md` 미정 128 은 조각 E 에서 한꺼번에 재분류하지 않는다 — 5a·5b·5c·6a·6b·6b2·6c·7 각 완료 기준이 자기 몫(뷰·입력·HUD·결과·사운드 / 슬롯·해저드·효과 뷰 / 카드·기믹·보스·도약·재배치)을 「새 주인」 또는 「삭제」로 닫고, unit 종료 시 `check_ledgers.py` 의 잔량을 상태 라인에 숫자로 적는다. 조각 C 의 배정은 **59 → 51 → 46 → 46 → 44**(6a **8행** · 6b **5행** · 6b2 **0행** · 6c **2행**)이고, 조각 D 가 **44 → 36 → 28 → 28 → 0**(7a **8행** · 7b **8행** · 7c **0행** · 7d **28행**)으로 닫는다 — **미정 0 이 조각 E 진입 조건**이다. ⚠ 6b 의 효과 타일 3행과 6c 의 뷰 풀 행들은 **이미 배정된 행의 주인 정정·확정**이라 잔량을 줄이지 않는다 — 6b2 가 0행인 것도 누락이 아니라 그 영역의 브리지 행이 이미 `GimmickHost`·「디버그/로그」로 배정돼 있기 때문이다. `rules.md` 보류 51 은 전부 unit 번호 또는 「후속 후보」를 단다.

## Feature-wide 계약

1. **규칙은 옮기고 기계는 옮기지 않는다.** 컴포넌트·시스템·큐·ECB·Burst 우회는 가져오지 않는다.
2. **땜빵·우연은 의도만 옮긴다.** unit 0 분류표가 정본. ⚠ 없으면 조용히 망가지는 세부(처치 드레인→전멸 판정 순서 · 분열 자식 셀 양자화 · 어그로 배타성 등)는 「필수」다.
3. **검증 = 의도 규칙의 EditMode 테스트 + 사용자 플레이.** 옛 골든은 참고. 새 코어는 자기 골든(unit 1 의 러너)을 갖고, 옛 러너와 **병존**하는 동안만 A/B 비교가 가능하다.
4. **전투 코어는 엔진을 모른다.** `noEngineReferences`, 참조는 `Unity.Mathematics`·`Wassup.Skills`·`Wassup.UnitAi`(둘 다 이미 같은 형태 — 실현 가능 검증됨). salvage 시 `NativeArray` → 배열.
5. **결정론**: 고정 틱 1/60 · 단일 스레드 · `SimEntityId` 오름차순. 슬로모·정지 = 틱 발행률. 종료 후 틱 0. **같은 런타임 안의 계약이다** — 런타임 간 비트 동일은 약속하지 않는다(2026-09-23 실측: Unity Mono 는 float 식을 확장 정밀도로 평가해 .NET 9 헤드리스와 약 300틱부터 1 ulp, `kill_race_3min` 은 9,887틱에서 이벤트 순서까지 갈렸다. IL2CPP 는 또 다르다). 골든의 정본 런타임 = Unity EditMode, 헤드리스 lane 은 골든 제외.
6. **값의 정본은 판 밖.** 시트→SO→`MatchDefinitionBuilder`→`MatchDefinition`(plain, `configHash` 소유). 시트 파이프라인은 브리지·Entities 참조 0 — 무변.
7. **커맨드 ≠ 이벤트.** 커맨드 = 동기 + receipt. 이벤트 = 값 스냅샷, `SimEntityId` 키. **모든 소멸 경로는 소멸 이벤트를 낸다**(뷰 폴링의 후계).
8. **트리거→발동은 rev 3.** 정적 (트리거,페이로드)→(concrete,형) 표 유지 · 세대 BFS 는 직접 재진입만 · `AttackMod` 축 5종 · 매치 핵심 규칙은 바인딩 밖.
9. **제거 확정**: 캐스터 4기 + 캐스트 기계 · 유출 한도·몽마의 계약·적 마음 판정·뽑기 폴백 진입. 소환사 유지.
10. **콘텐츠 동결 — 장치로 강제한다.** pre-commit 훅 + CLAUDE.md 범위 규칙. 카메라·UI·아웃게임은 계속, 옛 전투 규칙 변경은 `[old-battle]` 태그 커밋(버그픽스)만.
11. **ECS 제약은 범위 꼬리표로 과도기를 지난다**(CLAUDE.md 재편 완료). unit 9 에서 옛 절 삭제.
12. **매니저를 두지 않는다.** `BattleBridge` 는 어디로도 흩어지지 않는다 — 귀속표대로 담당자가 갖거나 삭제. 검사 대상은 브리지만이 아니라 **브리지 밖 규칙 보유자 10** 까지. 판정·상태·저장은 담당자만. 순서 의존은 이벤트 구독 순서. 뷰도 통합 뷰 없이 풀마다 구독.
13. **매치 모드는 닫힌 집합.** `IMatchGoal` + concrete 3 을 v1 에(제약 8 개정). `WaveClear`·`TimeAttack` 의 마음 붕괴 = **패배**(통로는 `stress_full` 그대로). 한 토너먼트 = 한 모드. 슬롯 append-only. **v1 서버 무변**(제출은 `KillScoreTimed` 만).

## 파이프라인 커버리지

모든 플레이 오브젝트의 생성→렌더 경로가 바뀐다. `object-pipeline-map.md` 는 unit 8 에서 코어 기준으로 전면 재작성. 그 전 unit 은 「이 unit 이 여는 정거장」만 적는다. **예외: 5a 는 유닛·투사체 아키타입 표를 자기 문서에 둔다** — 뷰 풀을 신설하는 unit 이라 CLAUDE.md 규칙상 필수.

## 후속 후보 (범위 밖)

- **서버 API 확장**(modeId·sortDirection·leaderboardId) — 서버는 이 저장소 밖. v1 은 단일 제출 모드.
- 마음 N개 공유 체력(`heart-stress-axis/12`) — 새 코어 unit 4 위에서(`HeartMeter` 가 체력을 들어 이사 비용 0).
- 규칙 분류표 「보류」 재결정 — 사용자 플레이 뒤.
- 스폰 측면 오프셋의 순번 파생(X25 — 지금은 가변 상태) · 슬로모 느낌 재확인(X26, 틱 발행률로 바뀐 뒤 플레이로) · 희귀도 축(E21 — 소비처 0, 아웃게임 UI 몫).
- 결정론 등급 상향(리플레이·스냅샷) · 틱 30Hz 실측 · 트리거 연쇄 깊이 근거 · 마메모 웨이브 훅 위치 확인.
- `docs/spec` 1,157건의 옛 포인터는 역사서라 두고, `docs/reference` 14건만 unit 9 에서 고친다.
- **효과 census 에서 조각 C 에 자리를 못 준 것**(전부 「소비처가 없어 지금 정하면 근거 없는 결정이 된다」): ⑴ `regenPerSec` 의 음수 강제 고정 처리 — 생산자 0 ⑵ 결합식 바닥/천장 4개(`[0.2,5]`·`[0.15,3]`·`0.05`)의 **SO 저작화** — 지금은 근거 주석 동반 상수 ⑶ 감속장 스냅샷(F35, 「안에 있는 대상이 영향을 받는다」의 마지막 예외) — 생산자가 unit 7 ⑷ 실드 부여의 한 틱 지연을 즉시로 통일할지(unit 3 이 현행 비대칭으로 이미 결정 — 뒤집으려면 별도 근거 + 골든 재굽기).
- **`unit-stats-and-modifiers` spec 2편의 은퇴 표기** — 그 문서가 요구하는 고정소수점 scale 1000 + 「가산 후 1회 승산」은 **채택하지 않는다**(모든 저작 수치가 재조정 대상이 된다). 새 코어는 현행 float `(1+Σadd)×Πmul` 이고, 그 결정으로 두 문서가 죽는다 — 문면 정리는 unit 9 의 문서 목록에 붙인다.
