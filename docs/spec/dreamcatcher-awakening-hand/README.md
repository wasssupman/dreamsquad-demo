# Dreamcatcher Awakening Hand — 사용 방식 전면 개편 (3중1 → 각성치 + CR식 순환 손패)

> 상태: **구현 진행 중 2026-07-10 (rev 4 — 실플레이 피드백: 확정 지연 제거·유닛 호버 하이라이트 above-units 수정 / rev 3 — 설계 critic REVISE 반영: C1 skillRuntime 배선 해제 / H2 phase 강제 클로즈 / M1~M3 / L1~L3)**
>
> 설계 배경: 기존 3중1 선택(첫 배치 + 5웨이브마다, 일시정지 모달)을 버리고, 클래시 로얄의 덱 순환 구조를
> 드림캐쳐에 적용해 **언제든 사용 가능한 실시간 요소**로 바꾼다. 기존 **공용 스킬**(매판 2종 롤 + 코스트
> 사용, SkillBar)도 별도 시스템을 폐지하고 **Active 타입 드림캐쳐**로 흡수한다.
>
> 기획 원문: `docs/reference/드림캐쳐_각성안_최종스펙_v1.md` (다른 맥락 포함 — 사용방식 전환 부분만 반영)

## 목표

- **각성수치**(특수 재화): 아군/적 유닛 사망으로 획득, 상한 100. 우하단 게이지 UI(수치 + 버튼).
- **CR식 순환 손패**: 판내 큐 **12장** = 아웃게임 세이브덱 10장 + **공용(Active) 드캐 2장**(매판 공통 배정).
  매치 시작 시 시드 셔플 → 순환 큐. 손패 = 큐의 앞 N장(기본 5, 상시 상태). 버튼 토글로 유닛 선택 스트립과 플립 전환.
- **스와이프 사용**: Unit 타입 = 유닛 위 touchup 부착 / Squad 타입 = 아무 영역 touchup / **Active 타입 = 필드
  타겟(타일·유닛) 지정 사용**. 손패 영역 복귀 = 취소. touchup 후 **확정 지연**(취소 가능) 뒤 커밋.
- **슬로모**: 손패가 열려 있는 동안 전투 감속(게임은 안 멈춤) — 위기 중 침착한 지출.
- **타입별 비용**: Unit 15 / Squad 30 / Active 20 (config).
- **순환 규칙**: Squad·Active = 사용(커밋) 시 큐 맨 뒤. Unit = 부착 유닛이 죽어야 큐 맨 뒤. 전량 유출 시 빈 손패.

## 검증 질문

> 각성수치가 사망 보상으로 차오르고(상한 100), 손패에서 스와이프로 Unit 부착·Squad 즉시 적용·Active 필드
> 사용이 각각 비용 차감과 함께 동작하는가? 사용/부착/사망회수에 따라 12장 큐가 CR 방식으로 순환하는가?
> 손패 열림 중 슬로모, touchup 후 확정 지연·취소가 동작하는가? 기존 3중1 모달과 SkillBar 없이 매치가 정상
> 진행되는가?

## 사용자 확정 결정 (2026-07-09)

1. **손패 = 순환 큐의 앞 N장 상시 상태** (CR식). 버튼은 토글, 열 때마다 같은 손패. **N=5** (문서 기준).
2. **복귀 = 토글 재클릭 + 카드 사용(커밋) 시 자동 복귀 둘 다.**
3. **덱 순서 = 매치 시드 셔플 1회** (기획 문서의 "순서 고정"과 달리 셔플 확정 — 같은 시드 = 같은 순서로 결정론 유지).
4. **기본 수치 = 문서 기준**: 손패 5 / Unit 15 / Squad 30. (Active 20 은 튜닝 계수 ⑥ — 기본값만 배정.)
5. **슬로모 + 확정 지연 둘 다 이번 spec 포함.** → **rev 4 (2026-07-10): 확정 지연(오부착 방어)은 실플레이 확인 후 제거** — touchup 즉시 커밋. 취소는 드래그 중 손패 영역 복귀/ESC 로만.
6. **Active 는 SkillRuntime 쿨다운·CostRuntime 코스트 둘 다 제거** — 재등장 간격은 순환 큐가, 비용은 각성치가 대체.
7. **구 3중1 플로우와 SkillBar 는 dormant** — 코드 유지, 씬 배선/활성만 해제.

## 기획 문서와의 의도적 편차 (정합성 기록)

- **셔플**: 문서 §3·§11 은 "랜덤 없음·순서 고정"이나 사용자 확정으로 **시드 셔플** 채택. 시드 고정이므로 동일 시드 재도전의 결정론은 유지된다.
- **슬로모**: 문서 §11 은 "뷰 전용, 시뮬 영향 금지"이나 이 프로젝트의 결정론 모델(타임스탬프 로그, TimeManager 도메인 배율)에서는 **Battle 도메인 timeScale 감속**이 관례(구 pause lease 의 0f 를 slomoScale 로 바꾼 것). 문서의 제약은 별도 Game.Core 맥락의 것.
- **수급 수치**: 문서 §2 의 웨이브당 70~80 은 악몽 1~3/아군 4 백필과 산술이 안 맞는다(문서 내 자체 편차). 백필 기본값은 첫 요청 기준(악몽 1~3, 아군 4)으로 하고 밸런싱은 config/SO 튜닝으로 후속(G1·G3 테스트).
- **문서 §5(스탯 드캐 폐지·전술 전용)·§6(무의식 저주 규율)은 카드 콘텐츠 방향** — 이 spec(사용 방식 전환) 범위 밖. 기존 스탯 카드는 그대로 순환에 태운다.

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_config_and_reward_fields.md` | 데이터 | `AwakeningConfig` SO + 유닛별 `awakeningReward` 필드 + 백필 (컴파일만) |
| 1 | `1_awakening_gain_runtime.md` | ECS+bridge | 적 reward 베이크 + `EnemyKilledEvent` 필드 append + bridge C# 이벤트 노출 |
| 2 | `2_active_card_data.md` | 데이터 | `CardType.Active` append + `DreamcatcherCard.skill` 필드 + Active 카드 에셋 6종(스킬 래핑) |
| 3 | `3_cycle_deck_runtime.md` | 순수함수+테스트 | `DreamcatcherCycleDeck` 12장 순환 큐 (셔플/front-N/재순환/부착/회수/빈손패) + EditMode |
| 4 | `4_hand_controller.md` | 컨트롤러 | `DreamcatcherHandController` — 덱 resolve + Active 2장 주입·게이지·사용 검증·회수, 구 3중1 dormant |
| 5 | `5_gauge_ui.md` | UI | 우하단 각성수치 게이지(숫자 + 버튼) + 토글 신호 |
| 6 | `6_hand_view_flip.md` | UI | 유닛 선택 스트립 ↔ 손패 플립 전환 + StS풍 카드 핸드 뷰 + 슬로모 lease |
| 7 | `7_card_drag_use.md` | UI | Unit/Squad 카드 스와이프 사용 (유닛 하이라이트/anywhere/취소) + 확정 지연 |
| 8 | `8_active_use_and_validation.md` | UI+검증 | Active 카드 필드 사용(타일/유닛/Portal 2탭) + SkillBar dormant + 전체 Play e2e |
| 9 | `9_squad_host_binding.md` | rev 5 | Squad 호스트 바인딩 — 효과는 스쿼드 전체, 소유는 호스트, 사망 시 철회+회수 |
| 10 | `10_handoff_summary.md` | 인계 | 종료 시 작성 |

## Feature-wide 계약 (load-bearing)

1. **모든 수치는 config/SO** (하드코딩 금지). `AwakeningConfig`: `gaugeMax=100`, `gaugeStart=0`, `costUnit=15`,
   `costSquad=30`, `costActive=20`, `handSize=5`, `slomoTimeScale=0.3`, `maxAttachPerUnit=3`. (`confirmDelaySec` 는 rev 4 에서 제거.)
   사망 보상은 유닛 SO 필드: `DefenderUnitData.awakeningReward`(기본 4), `AttackUnitData.awakeningReward`(타입별 1~3).
2. **각성 게이지 = Mono 상태.** ECS 는 보상량 전달만. 가산은 bridge 사망 드레인의 C# 이벤트 구독(`WaveMilestoneReached` 선례). `gauge = min(gauge + reward, max)` — 초과분 소실.
3. **ECS 확장은 최소**: 적 스폰 시 `AwakeningReward`(IComponentData, Units 소유) 베이크 → `DamageApplicationSystem`(기존 enqueue 지점)이 `EnemyKilledEvent.awakeningReward`(append)에 기입. defender 는 ECS 무변경 — `DrainDefenderDeathEvents` 시점에 bridge 가 binding 의 `DefenderUnitData` 직독. 신규 큐/맥락/시스템 0.
4. **큐 = 12장 엔트리 단위**: 세이브덱 10(카탈로그 해석, 없으면 serialized 폴백) + Active 2(기존 `SkillLoadoutController.Roll` 결과를 Active 카드로 매핑). 12장을 **매치 시드로 셔플 1회**(`System.Random`, `UnityEngine.Random` 금지). 같은 카드 SO 2장 = 독립 엔트리.
5. **순환 규칙**: 손패 = 큐 front `handSize`. **Active 사용(커밋) → 큐 맨 뒤**. **Unit·Squad 사용 → 아웃풀**(레지스트리: 엔트리↔호스트) → **호스트 사망 → 큐 맨 뒤**(사망 순). 큐 < handSize 면 빈 슬롯. (rev 5/unit 9 — Squad 도 호스트 바인딩: 매치 영구·즉시 재순환이던 구 규칙 폐기, 사유는 밸런스.)
6. **적용은 기존 API 재사용**: Squad → `ApplyDreamcatcherCardHosted`(핸들 발급; 호스트 사망 시 `RevokeDreamcatcherEffects` = 같은 stackId 에 배율 1.0 중립화 재적용 — unit 9), Unit → `ApplyDreamcatcherCardToUnit`, Active → `CastSkillAtTile`/`CastSkillOnDefender`/`CastPortal`. 신규 캐스트 경로 금지. Squad 스택은 호스트 생존 수만큼만(영구 스택 폐기). **호스트당 부착 최대 `maxAttachPerUnit`(3), Unit+Squad 합산** — 컨트롤러 가드.
7. **Active 는 쿨다운·CostRuntime 미사용.** `SkillData.cooldownSec`/`cost` 는 dormant(에셋 값 유지, 소비 안 함). 재등장 간격 = 순환, 비용 = 각성치(`costActive`). **주의(critic C1)**: 기존 `CastSkillAtTile`/`CastPortal`/`CastSkillOnDefender` 는 내부에서 `skillRuntime.IsReady` 게이트 + `Consume` 을 강제한다 — **BattleBridge 의 `skillRuntime` SerializeField 배선을 해제**해야 `skillRuntime?.` 가드가 no-op 이 되어 계약이 성립한다(유일한 다른 소비자 SkillBar 도 dormant 라 안전).
8. **게임은 안 멈춘다 + 슬로모**: 손패가 열려 있는 동안 `TimeManager.Request(TimeDomain.Battle, slomoTimeScale)` lease 보유(플립 백 시 해제, 멱등 Dispose — 구 pause lease 패턴). 일시정지(0f) 금지.
9. **즉시 커밋 (rev 4)**: touchup 시 즉시 Commit — 적용·차감·순환·로그는 성공한 Commit 안에서만. 실패(대상 소멸/부착 상한/캐스트 거절) = 무차감·카드 원위치. 취소 = 드래그 중 손패 영역 복귀·ESC·토글·phase 이탈. 게이지 검증은 드래그 시작(dim)과 커밋 시점 이중. (~~confirmDelaySec pending~~ — critic H1/M4 는 pending 전제라 함께 은퇴; Recovered 재렌더는 드래그/2탭 중 deferral 로 동일하게 방어.)
9-1. **손패 뷰 생명주기 (critic H2)**: 손패 뷰는 `GameManager.PhaseChanged` 를 구독해 **Battle/Placement 이탈 시 강제 클로즈**(UnitStrip 복귀 + pending 드롭·무차감 + 슬로모 lease Dispose + Portal 2탭 상태 해제). "손패 열린 채 매치 종료"가 e2e 케이스에 포함된다.
10. **스와이프 = 기존 D&D 패턴 재사용**: `DefenderDragSlot`/`DefenderDragPlacementController` 세션 구조, 셀 변환 재사용. **유닛 타겟팅 검출은 스크린 스페이스 픽킹이 1차**(rev 4-3 근본 수정): 보드평면 레이캐스트는 바닥 셀만 줘서 틸트 빌보드 '몸체' 포인팅을 빗나간다 — `SpineUnitView.TryGetScreenRect`(스프라이트 렉트) → `bridge.TryPickDefenderAtScreen`, 셀 조회는 2차(발밑 탭·폴백 quad). 포커스 표시 = **호버 유닛 스파인 붉은 틴트 단일**(rev 4-4, `SetDefenderHoverHighlight` — 타일 하이라이트는 사용자 확정으로 제거).
11. **dormant 전환 3건**: ① `DreamcatcherController`(3중1 트리거) + `DreamcatcherSelectionView`, ② `SkillBar`, ③ **BattleBridge 의 `skillRuntime` SerializeField 배선 해제**(critic C1 — 캐스트 API 내부 쿨다운 게이트 무력화). 코드 유지·씬 배선/활성 해제. `SkillLoadoutController` 는 **계속 사용**(Active 2종 롤 소스, 시드·로그 유지). Active 롤이 2장 미만(풀 미구성/매핑 누락)이면 **경고 후 있는 만큼만 주입해 진행**(큐 = 10+α, critic M2).
12. **직렬화 append-only**: enum 케이스(`CardType.Active`)·SO 필드·이벤트 struct 필드는 끝에 추가(기존 에셋 값 보존, zero-init inert).

## 밸런스 의도 (config 로 조정)

- 판(3분, next-wave 미사용) 기준 **총 ~15장 사용**, ≈ 분당 5~6장. 평균 비용 ~18 → 분당 각성 ~100.
- 소스: 악몽 처치(1~3/kill) 주력 + 아군 사망(4) 보조. 아군 사망 지분 30%+ 목표(문서 G3)는 백필 후 실측.
- Active(응급 유틸) 남발 방지는 코스트+순환이 담당 — "Active 만 쓰기"는 큐가 안 돌아 구조적으로 불가(문서 §8).

## 파이프라인 커버리지

`docs/reference/object-pipeline-map.md` 대상 아키타입 신설 **없음** — UI/데이터/이벤트 확장이다. Active 는 기존 스킬 캐스트 파이프라인(텔레그래프·투사체·해저드)을 그대로 호출만 한다. 생성→렌더 경로 변경 없음 → 전 정거장 **N/A**. ECS 접점은 `EnemyKilledEvent` 필드 append + 적 스폰 베이크 컴포넌트 1개뿐.

## 후속 후보

- 덱빌더에서 덱 **순서 편집** UX (문서의 "순서를 짜는" 덱빌딩 감성 — 셔플 대신 고정 순서 채택 시 필수).
- 각성 게이지 연출(획득 플로팅/가득 참 강조), 카드 사용 VFX/SFX.
- 부착 카드의 유닛 위 표시(뱃지/아이콘) + 유닛당 3개 슬롯 시각화.
- 카드 콘텐츠 개편(문서 §5: 스탯 드캐 폐지·전술 전용, §6: 무의식 저주 규율) — 별도 spec.
- Active 카드 전용 아트(현재 uiTint/스킬명 폴백), 공용 풀 유틸 한정 재구성(Meteor 등 딜 스킬 제외 여부 — 밸런스 소관).
- 구 3중1·SkillBar 코드 완전 삭제 cleanup.
- 튜닝 테스트 G1~G6(문서 §13): 재화 진폭, 슬로모 체감, 사망 지분 30%+, 손패 유효성, Active 지출 균형.
- 순환/사용 이력의 토너먼트 로그 통합(`RecordDreamcatcherOffer` 계열 대체).
