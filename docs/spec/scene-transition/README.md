# Spec — Scene Transition (씬 전환 연출)

> 상태: **초안 (미승인)** · 2026-07-10 작성

## 한 줄

로비(`OutgameScene`) ↔ 전투(`BattleScene`) 간의 즉시 컷 전환을, **화면을 덮는 페이드 토대(A) + Spine 브랜드 컷인(D)** 으로 감싼 연출 전환으로 바꾼다.

## 검증 질문

> "씬이 바뀌는 순간, 화면이 끊기지 않고 브랜드 컷인을 지나 자연스럽게 다음 씬으로 이어지는가?"

이 질문에 답하는 데 필요 없는 것(로딩 진행바, 씬 프리로드/Additive, 다중 전환 프리셋 등)은 전부 스코프 밖 → "후속 후보".

## 현황 (조사 결론)

- 씬 2개 · **Single 모드 동기 컷 전환**. 전용 전환 매니저 없음.
- `SceneManager.LoadScene(SceneNames.X)` 직접 호출 3곳:
  - `UI/Outgame/OutgameMenuController.OnStartGame()` → Battle (START)
  - `UI/Outgame/TestModePanelView` (웨이브 선택) → Battle
  - `UI/MenuPopup.OnExit()` → Outgame (일시정지 메뉴 나가기)
- 연출 인프라 **전무**: 페이드/로딩/트랜지션 오브젝트 없음. `DontDestroyOnLoad`·persistent 캔버스 없음(GameManager 는 의도적으로 battle-scoped 비영속).
- 가용 자산: **PrimeTween**(`Assets/Plugins/PrimeTween`) 재사용, **Spine**(SkeletonGraphic) 컷인용. URP 모바일 렌더러는 렌더 피처 비어 있음(이번 스펙은 렌더 피처 미사용 — 캔버스 오버레이 방식).

## 아키텍처 요약

- 전 과정 **MonoBehaviour / Presentation 계층**. ECS 맥락(Units/Movement/Combat/Effects) 과 무관 — `BattleBridge` 경유 없음, ECS 리뷰 대상 아님.
- **`SceneTransition`** = persistent 단일 역할 매니저(제약 #5 허용 범주, `SoundManager` 급). `DontDestroyOnLoad` 캔버스 위에서 cover-in → async 씬 로딩 → 컷인 → cover-out 시퀀스를 구동한다. **유일 공개 진입점은 static `SceneTransition.Go(string sceneName)`** (내부에 null-guard/degrade). `Instance.Go` 를 외부에 노출하지 않는다 — API 표면 1개.
- 부트스트랩: 씬마다 수작업 배선하지 않도록 `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` + `Resources` 프리팹 1개로 자동 생성. (per-scene 배선 없음)
- **수치는 프리팹의 `SceneTransition` 컴포넌트 `[SerializeField]`** 로 authoring: fade in/out 시간, cover hold, 색, min cover time, 컷인 애니 이름. 제약 #6은 "SO **또는 프리팹**" 을 허용 — 컨트롤러가 이미 단일 프리팹에 살므로 프리팹이 곧 authoring 소스다. **별도 설정 SO 는 만들지 않는다**(프로파일 스왑이 실제로 필요해지면 그때 SO 승격 — 제약 #8 과잉추상 가드).
- **컷인(D)**: 전환 캔버스 안의 Spine `SkeletonGraphic` 오버레이. `Go(sceneName)` 이 방향(into-battle / to-lobby)을 아는 **훅/파라미터는 유지**하되, 초기엔 **공용 컷인 1벌**을 양방향 재생. 구별되는 애니 2벌 authoring 은 단일 컷인이 실제로 어색할 때만(방향 variant YAGNI 회피).
- 순수 함수 추출: 없음. 타이밍은 SO 값 → 트윈 직결, sim-critical 계산 아님. 제약 #10 의 과잉추출 가드에 따라 인라인 유지.

## 작업 단위 목록

| 파일 | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 토대(A) | `0_scene-transition-core.md` | `SceneTransition` persistent 컨트롤러(수치는 프리팹 SerializeField) + async 로딩 래퍼 + 단색 페이드 cover-in/out. static `Go()` 진입점 + no-op 컷인 훅. 페이드만으로 end-to-end 동작. |
| 1 | 배선 | `1_wire-call-sites.md` | 직접 `LoadScene` 3곳을 `SceneTransition.Go` 로 리다이렉트. 모든 전환이 파이프라인 경유. |
| 2 | 디졸브 커버 + 스파인 로딩 | `2_dissolve-cover-transition.md` | 커버를 **라디얼 골든 디졸브**(로비 배경 디졸브 재사용)로 교체 — 현재→반대 시간대 스왑(클릭 지점에서 퍼짐) → **스파인 로딩 화면**(Casual Character 러닝) → 배틀. **0+1 검증·커밋 후 게이트**. |
| 3 | 인계 | `3_handoff_summary.md` | 구현/검증 종료 후 세션 인계 요약. |

**순서 의존**: 0 → 1 (진입점 있어야 배선). **0+1 로 "끊김 없는 전환" 검증 질문을 먼저 종결·커밋**한 뒤, 그 위에 커버 비주얼(2)을 얹는다. 한 번에 한 파일, 사용자 확인 후 다음.

> **방향 전환 (2026-07-10)**: unit 2 를 "Spine 컷인 오버레이"에서 **"로비 배경 디졸브 재활용 + 스파인 로딩 화면"**으로 교체. 사용자 결정 — 검정 페이드를 버리고, 캐릭터 터치 시 배경 낮/밤을 바꾸던 라디얼 골든 디졸브를 씬 전환 커버로 승격(라디얼 중심=START 버튼 클릭 지점, **현재 상태→반대**로 스왑). 스왑 뒤 Casual Character 러닝을 로딩 화면으로 노출 후 배틀.
>
> **후속 실험 (홀드/진행 예정)**: "낮/밤 스왑 대신 디졸브가 스파인 로딩 화면을 직접 노출"하는 변형(로딩 배경=단색 다크). 현재 버전을 체크포인트로 커밋한 뒤 별도 시도.

## Feature-wide 계약

1. **유일 진입점**: 모든 씬 전환은 static `SceneTransition.Go(sceneName)` 경유(공개 API 1개). `SceneManager.LoadScene` 직접 호출은 프로덕션 코드에서 제거(테스트 스모크는 예외).
2. **재진입 방지**: 전환 진행 중 `Go()` 재호출은 무시(멱등). 이중 로딩·이중 컷인 금지.
3. **persistent 단 하나**: `SceneTransition` 인스턴스는 전 씬 통틀어 1개(`DontDestroyOnLoad`). 중복 생성 시 후발 인스턴스 자기파괴.
4. **cover 보장 순서**: cover-in 완료(화면 완전 가림) → `allowSceneActivation=true` → cover-out. 로딩 순간이 절대 노출되지 않는다.
5. **min cover time**: 로딩이 즉시 끝나도 SO 의 min cover time 만큼 컷인을 보여준 뒤 아웃(깜빡임 방지).
6. **수치 프리팹 소유**: 모든 타이밍/색/컷인 애니 이름은 프리팹의 `SceneTransition` 컴포넌트 SerializeField. 코드 리터럴 금지(제약 #6, 프리팹 authoring 소스).
7. **time-scale 독립**: 전환 트윈은 `unscaledTime` 기반(전투 일시정지/`TimeManager` 와 무관하게 동작). `Time.timeScale` 건드리지 않음(제약: TimeManager 원칙 준수).
8. **teardown 안전**: 전환 중 씬이 파괴돼도 페이드 캔버스는 persistent 라 유지. 컷인 SkeletonGraphic 도 전환 캔버스 소속(파괴되는 씬에 두지 않음).

## 파이프라인 커버리지

`docs/reference/object-pipeline-map.md` 는 전투 **플레이 오브젝트**(유닛/적/투사체/해저드/VFX)의 생성→렌더 경로 체크표다. 본 스펙의 산출물은 전부 **아웃게임/UI Presentation 오버레이**(전환 캔버스 + SkeletonGraphic 컷인)로, 전투 시뮬 플레이 오브젝트가 아니다 → 파이프라인 맵 대조 **N/A (사유: 배틀 플레이 오브젝트 아님, ECS·풀링·BattleBridge 경로 미사용)**. 컷인 Spine 은 SkeletonAnimation(월드)이 아닌 `SkeletonGraphic`(Canvas) 이라 기존 프랍/유닛 Spine 파이프라인과도 분리된다.

## 후속 후보 (현 스코프 밖)

- **Spine 브랜드 컷인(옛 unit 2)**: 전환 커버 위에 Spine `SkeletonGraphic` 캐릭터 컷인. 전용 스와이프 컷인 애니 authoring 확보 후. (디졸브 커버로 대체되어 이관 — 디졸브 위에 캐릭터를 더 얹고 싶을 때 부활)
- **URP FullScreenPass 셰이더 전환(방안 B)**: 와이프/원형 마스크/글리치 풀스크린 셰이더. 표현력↑, 모바일 세이프 셰이더 필요. 디졸브만으로 부족하면 검토.
- **Additive 프리로드(방안 C)**: 전투 씬 로딩이 실제로 체감될 만큼 무거워지면 로딩 아키텍처 전환.
- **전환 프리셋 다양화**: 매치 승/패 결과별 다른 컷인, 로딩 진행 표시.
- **매치 종료 자동 복귀**: 현재 EXIT 버튼 흐름 의존. GameOver→씬전환 자동화는 별도 결정.
