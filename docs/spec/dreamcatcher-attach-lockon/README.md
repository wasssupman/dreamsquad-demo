# Dreamcatcher Attach Lock-On — 부착 조준 피드백 오버홀 (StS 화살표 유지, 포커스 락온 + 오프셋 콜아웃)

> 상태: **완료 2026-07-18 · 커밋 `b487ac42`** (구현+투트랙 리뷰 반영+사용자 Play 확인). 인계: `7_handoff_summary.md`.
> 사용자 확정: 1안 포커스 락온 / 리티클(위치)+**오프셋 콜아웃(정체·유효성·확정)** 이중 신호 / 화살표 끝점 대상 당김 / 제스처 코어 유지 / 콜아웃 = 아이콘+이름+부착수(X/3).
> rev(구현 중 확장·리뷰 반영): 적 표식(살찌운 제물)도 리티클/콜아웃 적용 · 화살표 3-상태(흰=idle/하늘=가능/붉=불가)+삼각 화살촉 · **H1 좌표계(CanvasScaler scaleFactor 환산) 수정**(비-1080p 실기기) · 단일 facade 통합.
> 리뷰: 설계 critic(UX+기술) 및 코드 리뷰(code-reviewer + ecs-reviewer 투트랙). ecs=APPROVE(읽기전용 확인), code=REQUEST CHANGES→반영. 반영 요지는 하단 "리뷰 반영 요약".
> 부모 스펙: `docs/spec/dreamcatcher-awakening-hand/` (StS 화살표·스크린 픽킹·붉은 틴트가 rev 4에서 도입된 곳. Squad 호스트 바인딩 = unit 9 rev 5).

## 설계 배경

밀집 배치(유닛·악몽 다수)에서 슬더스식 부착 화살표의 시인성이 무너진다. 사용자 지목 세 불편 —
**① 화살표 선이 난장(스프라이트·VFX·투사체)에 묻힘 · ② 끝이 어느 유닛인지 모호 · ③ 붙였는지 확신 안 섬.**

**결정적 사실(critic)**: 부착 pick 은 `screenPos = 포인터 = 손가락` 위치의 유닛을 고른다(`DreamcatcherCardDragSlot.UpdateUnitHover` → `bridge.TryPickDefenderAtScreen`). 즉 락온 유닛은 **손가락 밑**이고, 유닛 위에 그리는 신호(리티클·틴트·확정비트)는 전부 손가락에 가린다. 화살표 끝점 당김도 *그려지는* 화살촉만 옮길 뿐 pick 은 손가락에 남는다. → **"어느 유닛에 부착되는지 확연히 인지"는 정체를 손가락 밖(오프셋)에 그려야만 성립한다.**

타겟: **20~40대 남성 미드코어 · 가로형 모바일 · 쉬운 동작 · 손가락 가림을 완전 회피 못해도 어느 유닛인지 확연히 인지되는 구조가 전제.**

## 목표

미드코어 조준 UX 표준(하스스톤·StS·MOBA 스킬 조준 = 배경 다운 + 타겟 하이라이트 + 락온 + 확정 비트)을 이식하되,
**제스처 코어는 그대로 두고 피드백만 오버홀**한다. 신호를 **위치(손가락 밑)** 와 **정체(손가락 밖)** 로 분리한다.

- **제스처 불변**: 손패 고정 카드 → 드래그로 조준 → touchup 즉시 커밋 → `FlyCardToUnit` 흡수. 부착 규칙·코스트·순환·시뮬 무변경.
- **피드백 요소**:
  - **A. 전장 dim** — 드래그 시작 시 전장 감광. 난장이 어두워져 화살표가 떠오름(불편 ①).
  - **B. 유효 base-ring** — 붙일 수 있는 배치 유닛에 얕은 링(어디에 붙일 수 있나 사전 조준, 불편 ②).
  - **C. 락온 리티클** — 화살표 팁 최근접 **유효** 유닛에 **단 하나**, 손가락 밑. **위치/락** 신호. full(3/3)은 invalid 폼.
  - **D. 오프셋 아이덴티티 콜아웃(핵심)** — 락온 유닛 위 손끝 반경 밖 오프셋에 **아이콘+이름+부착수(X/3)**. **정체·유효성·확정** 신호. 손가락에 안 가림(불편 ②·③).
  - **E. 확정 비트** — touchup 시 리티클 수렴 + 콜아웃 "찰칵" 펀치 + 손끝 반경 초과 링 펄스 + (모바일)햅틱 → 흡수(불편 ③).
- **화살표 코어 변경 2건**: ① 렌더 시인성(아웃라인/글로우/최소알파) · ② **Defender 락온 시** 끝점을 유닛 중심으로 ~0.7 블렌드(EnemyMark 등 비-Defender 조준은 기존 pointer raw 유지).
- **슬로모 재사용**: 손패 열림 중 이미 ~30% slomo(`TimeManager`, 부모 계약 #8). dim 이 그 위에 얹혀 "기획 순간" 강화. 신규 시간제어 없음.

## 검증 질문

> 유닛·악몽이 밀집한 상태에서 부착 카드를 드래그할 때 — ① 화살표 선이 배경 위로 확연히 떠오르는가? ② 손가락이 유닛을
> 가려도 **콜아웃(아이콘+이름+X/3)** 으로 "지금 어느 유닛에, 붙일 수 있는지"가 즉시 읽히는가? ③ 손 뗄 때 "이 유닛에 확실히
> 걸렸다"가 손가락 밖에서 보이고 느껴지는가? 이 모두가 기존 커밋·흡수·순환·시뮬을 바꾸지 않고 성립하는가?

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_focus_config.md` | 데이터 | `DreamcatcherFocusConfig` SO — 전 레이어+화살표+콜아웃+히스테리시스 노브 (컴파일만) |
| 1 | `1_battlefield_dim.md` | UI | 전장 dim 페이드 (레이어 A) — sorting = SafeAreaRoot 아래 |
| 2 | `2_lockon_reticle.md` | UI | 락온 리티클 단 하나 + attachable 게이트/invalid 폼 + 정체 히스테리시스 + 빨강틴트 제거 (레이어 C) |
| 3 | `3_lockon_identity_callout.md` | UI (핵심) | 오프셋 콜아웃 — 아이콘+이름+부착수(X/3), 손가락 밖 (레이어 D) |
| 4 | `4_arrow_lock_to_target.md` | UI | 화살표 끝점 Defender 락온 당김 + 아웃라인/글로우 시인성 |
| 5 | `5_valid_target_base_rings.md` | UI+bridge | 유효 유닛 base-ring + bridge 읽기전용 열거 + 컨트롤러 attachable API (레이어 B) |
| 6 | `6_commit_confirm_beat.md` | UI | touchup 확정 비트(수렴+콜아웃 펀치+손끝 초과 펄스+햅틱) (레이어 E) |
| 7 | `7_handoff_summary.md` | 인계 | 종료 시 작성 |

## Feature-wide 계약 (load-bearing)

1. **제스처 코어 불변.** 손패 고정 카드 → 드래그 → touchup 즉시 커밋 → `FlyCardToUnit`. 부착 규칙·코스트·순환 큐·ECS 시뮬은 이 spec 에서 **읽기만** 한다. 오버홀 대상은 조준 중 시각/확정 피드백뿐.
2. **모든 시각·타이밍 수치 = `DreamcatcherFocusConfig` SO** (하드코딩 0, 제약 #6). 경제/규칙 노브 `AwakeningConfig` 와 분리(런타임 튜닝 SO `DragSwaySettings.asset` 선례). `DreamcatcherHandView` 가 `[SerializeField]` 보유. (예외: 내부 이징률 `1-exp(-k·dt)`·MoveTowards 계수 같은 자명 상수는 인라인 — CLAUDE.md #10 취지. "look" 값(색·크기·오프셋·타이밍)은 전부 SO.)
2-1. **구현은 단일 facade** `DreamcatcherFocusPresenter` — dim/base-ring/리티클/콜아웃/확정펄스를 한 프레젠터가 소유·구동한다(unit 2/3 문서의 `DreamcatcherFocusReticle.cs`/`DreamcatcherFocusCallout.cs` 별도 파일은 stale — 단일 파일로 통합, 계약 #9 "단일 프레젠터"와 정합). 요소는 canvasRoot 직속 생성, `Update` 가 `SetAim` 상태를 매프레임 적용(움직이는 대상 추종).
3. **모든 오버레이 요소는 애셋-프리 UI 쿼드** (기존 `DreamcatcherTargetArrow` 기법). **draw-order 는 생성 순서가 아니라 명시적 `SetSiblingIndex` 로 강제.** 레인 순서(아래→위): **전장 < dim < [손패 카드/툴팁/게이지] < base-ring < 화살표 < 리티클 < 콜아웃 < 확정펄스**. 모두 `raycastTarget=false`. **dim 만 `SafeAreaRoot` 아래**(카드·드래그 툴팁·게이지 **감광 제외**), 조준 오버레이는 카드 **위**(기존 화살표의 '카드 위' 설계와 일치 — 조준 대상은 보드 상단이라 하단 카드와 공간 충돌 드묾). overhead 체력 UI(order 3)는 dim 에 포함(전장 quieting). ~~구 계약: 카드가 오버레이 위~~ 는 화살표 설계와 모순이라 폐기.
4. **락온 유닛 = 화살표 팁 최근접 유효 유닛** — 기존 `TryPickDefenderAtScreen`(스크린 스페이스 스프라이트 렉트) 재사용. 리티클·콜아웃·끝점·확정비트가 **이 단일 entity 하나**를 공유. **정체 히스테리시스**: 현재 락온 entity 를 우선 유지하고 새 후보가 `lockSwitchHysteresisPx`(SO) 이상 우세할 때만 전환(밀집 손끝 흔들림에 커밋 대상이 홱홱 바뀌는 것 차단 — 위치 스프링과 별개).
5. **화살표 끝점** = **락온 대상(Defender 부착 / 적 표식)에** `Lerp(pointer, targetCenter, blend)`(기본 0.7, SO). ~~구 계약: EnemyMark 제외~~ — 적 표식도 리티클/콜아웃 대상으로 확장되며 끝점 당김도 적용(rev, 코드 우선). `ActiveTile`/`ActivePortal` 은 카드-follow 라 화살표 없음. 대상 중심 = `BattleBridge.TryGetUnitScreenRect`(방어수/적 공용, `SpineUnitView.TryGetScreenRect` 투영).
5-1. **화살표 3-상태 색**(rev): idle(대상 없음)=흰색 / 부착 가능=하늘색(시안) / **부착 불가(full 3/3·이미 표식된 적)=붉은색**. 유효/무효를 색으로 구분(리티클 invalid 폼과 정합). 머리 = **삼각 화살촉**(절차 스프라이트, 끝이 대상에 꽂힘) + 아웃라인. 전부 SO 노브(`arrowNeutral/Valid/InvalidColor`, `arrowHeadSize`).
6. **유닛 전체 빨강 RGB 틴트 제거.** 락온 신호 주체 = 리티클(위치) + 콜아웃(정체). 기존 `SetDefenderHoverHighlight`/`UnitHoverTint` 의 전체 빨강 repaint 는 밀집에서 안 통함 + Spine R/G/B 는 곱셈 틴트라 "밝힘(pop)"이 기술적으로 불가(no-op/탈색) — 소비자가 이 경로뿐이라 **제거 안전**. 진짜 pop 이 필요하면 additive/`FlashWhite` 로 별도(곱셈 틴트로는 금지).
7. **유효/무효는 색이 아니라 형태로도 구분**(색약·야외). 유효 = 정상 리티클 + 콜아웃, **무효(full 3/3) = invalid 리티클 폼**(틈/회색/X) + 콜아웃 부착수 3/3 강조. 리티클 코너·확정 펄스는 `reticleMinScreenSize`·`confirmPulseMinRadius`(SO)로 **손끝 반경 초과**를 보장(occlusion 생존을 우연이 아닌 계약으로).
8. **유효성은 mode 별 · base-ring 은 부착(Unit/Squad)만.** bridge 열거는 **배치 defender 스크린렉트만**(순수 공간 read, 규칙 무지). attachable 판정 = **부착 캡 AND 기여여부**, hand 측 **드래그 시작 1회 스냅샷**(둘 다 드래그 중 불변): ① 부착수 < `maxAttachPerUnit`(컨트롤러 public `CanAttachMore`/`AttachCountOf`/`MaxAttachPerUnit`) ② **`bridge.WouldDreamcatcherCardApply(host, card)`** — "이 카드가 이 유닛에 실제 기여하나"(= `ApplyDreamcatcherCardToUnit` 이 -1 아님). 판정은 순수 `DreamcatcherAttachEval`(EditMode 핀 테스트), 유닛-종속 게이트만 미러: **통통구슬(ProjectileBounce)→투사체 유닛만 · 끝을 보는 눈(FrontmostTarget)·HeavyStrike→데미지 output · 이중 LethalTimer/DreamCocoon**. 데이터-검증 guard(magnitude 등)는 유닛 무관이라 미러 안 함. **동기화 계약**: apply 에 새 유닛-게이트 kind 추가 시 eval+테스트 갱신. → 통통구슬↔가디언처럼 커밋 거절될 유닛이 조준 중 **빨강(부착 불가)** 으로 뜬다(UI↔커밋 일치). mode 별: **Unit/Squad**=attach 여유(base-ring+리티클+콜아웃 X/3) · **Active-DefenderUnit**=셀 캐스트라 attach-cap 필터 제외(항상 유효, base-ring 없음) · **EnemyMark(적 표식)**=리티클+콜아웃 적용(rev — 콜아웃=카드 아트+이름, base-ring 없음). 유효성: 손가락이 **유닛 스프라이트 위**면 "유닛 불가" **빨강 무효**(적 표식은 유닛에 못 씀, 우선), 아니면 최근접 적(미표식=유효 "표식 가능" / 이미 표식=무효 "이미 표식됨"). 유효성은 슬롯이 결정→`SetAimEnemyMark` 로 전달(화살표·리티클·콜아웃 공유) · `ActiveTile`/`ActivePortal`=비적용(카드-follow). 화살표 유효성(`IsHoverAttachable`)과 리티클/콜아웃 유효성(`_lockValid`)은 같은 entity·같은 스냅샷 공유.
9. **연출 소유 = 드림캐쳐 조준 UI.** 메커닉이 연출 소유(프로젝트 원칙). 공용 `StatusFx`/`BattleBridge` 에 조준-연출 kind 분기 금지. dim·base-ring·리티클·콜아웃·확정비트는 전부 `DreamcatcherHandView` 소유 프레젠터 안.
10. **생명주기 하드클리어.** 모든 오버레이(dim·리티클·콜아웃·base-ring)는 `EndInteraction` **뿐 아니라** 뷰 `Close`/`ForceClose`/`OnDisable`/`OnPhaseChanged`(Battle/Placement 이탈)에서 **하드 클리어**(잔류 프레임/leak 금지, dim 과 동일 취급).
11. **성능**: 버퍼 재사용·1회 생성 필수 — `outBuf`/`_defRectBuf`/`_attachable`/`_rectBuf` 는 재사용, 링/콜아웃 UI 쿼드·절차 스프라이트는 1회 생성(매프레임 `new`·절차생성 금지), 콜아웃 카운트 문자열은 정체 바뀔 때만 재계산. Dictionary/HashSet foreach 는 struct enumerator라 무할당. **주의(리뷰 반영)**: pick(`TryPickDefenderAtScreen`, 기존 공용)과 base-ring 열거(`EnumerateDefenderScreenRects`)는 **각각 O(N) 순회**로 남는다 — 완전한 "1회 산출 공유"는 미구현(pick 은 타 소비자와 공용이라 미분리). 보드 규모 N 작고 Mono 메인스레드·무할당이라 수용, 완전 공유는 후속 후보. 소형 가로폰 클러터 완화용 base-ring **근접 reveal**(`baseRingRevealRadius`/`baseRingDistanceFade`, SO). RectTransform 절대배치는 CanvasScaler `scaleFactor` 로 device px↔canvas-local 환산(리티클 arm·콜아웃 clamp) — 비-1080p 실기기 필수.
12. **Squad = Unit 과 동일 조준**(부모 unit 9 rev 5): 화살표 + 유닛 몸체 드롭(anywhere touchup 폐지), host = 포인팅 유닛, 부착 캡 Unit+Squad **합산 공유**. 부모 README line 17("아무 영역 touchup")은 stale — unit 9 가 대체.

## 리뷰 반영 요약 (critic → 계약 매핑)

- **[UX C1/신설]** 오프셋 콜아웃(unit 3) — pick 이 손가락 밑이라 정체를 손가락 밖으로. 확정 가시성(H3)·색독립(M3)도 흡수. (계약 #7, unit 3·6)
- **[UX H1]** 리티클 attachable 게이트 + invalid 폼(full 유닛 거짓 락온 차단). (계약 #7, unit 2)
- **[UX H2]** 정체 히스테리시스/래칭(밀집 커밋 대상 플리커). (계약 #4, unit 2)
- **[기술 HIGH]** dim sorting 자기모순 수정(툴팁/카드 감광 회귀 차단) + sibling index 강제. (계약 #3, unit 1)
- **[기술 MED]** 끝점 당김 EnemyMark 누수 차단(계약 #5, unit 4) · 컨트롤러 public API + 드래그시작 스냅샷(계약 #8, unit 5) · Active-DefenderUnit 필터 예외(계약 #8, unit 5) · 빨강틴트 밝힘 불가→제거(계약 #6, unit 2) · 생명주기 하드클리어(계약 #10).
- **[기술 LOW]** per-frame 렉트 공유·버퍼 재사용·배칭(계약 #11) · edge-clamp(각 unit 완료기준) · 햅틱 = `Handheld.Vibrate`(~0.5s, "경량" 문구 조정, unit 6).
- **해소**: Squad 모델(부모 unit 9, 계약 #12) · `unscaledDeltaTime`(timeScale 상시 1, 무해).

## 파이프라인 커버리지

`docs/reference/object-pipeline-map.md` 대상 아키타입 신설 **없음** — 오버레이 UI 피드백 확장. 신규 플레이 오브젝트·생성→렌더 정거장 변경 없음 → 전 정거장 **N/A**. 유닛 위치·아이콘 참조는 기존 `SpineUnitView.TryGetScreenRect`(스크린 픽킹) + 유닛 SO 아이콘 재사용, 신규 렌더 경로 0.

## 후속 후보 (현 spec 범위 밖)

- **리티클/콜아웃 드림-웹 테마 강화**(거미줄/깃털/실 모티프) — 우선 크리스프 브래킷·명료 콜아웃으로 검증 후 감성 레이어 별도.
- ~~**EnemyMark 모드 조준 피드백 통일**~~ — **완료(rev)**: 적 표식도 리티클+콜아웃(카드 아트+이름+표식상태)+끝점 당김+dim 적용. 남은 것: 적 base-ring(다수 적이라 클러터 우려로 미적용), 색 통일(현재 화살표 3-상태 공용).
- **base-ring/리티클 pick↔열거 완전 공유** — 현재 pick(`TryPickDefenderAtScreen`)과 `EnumerateDefenderScreenRects` 가 프레임당 각각 O(N) 순회(계약 #11 주). 보드 규모라 수용 중 — 유닛 수 급증 시 렉트 집합 1회 산출 공유로 통합.
- **부착 결과 유닛 위 상시 뱃지/슬롯 시각화**(유닛당 3슬롯) — 조준(이 spec)과 사후 상시 표시는 별개. 부모 후속과 연결.
- **dim 데새춰(desaturate) post-process 버전** — 현재는 반투명 감광 패널.
- **인터랙션 모델 변경(2·3안: 자석 커서/직접 끌어놓기)** — 이번 락온+콜아웃으로 세 불편 해소되면 불필요.
