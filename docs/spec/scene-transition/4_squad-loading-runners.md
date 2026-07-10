# 4 — Squad Loading Runners (로딩 화면 = 스쿼드 3인 러닝)

## 목적

로딩 화면의 **단일 제네릭 캐릭터 러닝**을, 확정된(선택된) 스쿼드에서 뽑은 **유닛 3인이 함께 뛰어가는** 연출로 교체한다. 로딩 순간이 "내가 고른 스쿼드가 전장으로 달려나가는" 브랜드 컷으로 바뀐다.

검증 질문의 연장: "로딩 화면이 끊김 없이 이어지는가"에 더해 **"화면에 내가 편성한 유닛들이 보이는가"**.

## 변경 대상

- `Assets/_Project/Scripts/Core/SceneTransition.cs` — 스쿼드 해석 + 러너 스킨 주입.
- `Assets/Resources/SceneTransition.prefab` — `LoadingSpine`(단일) → `LoadingRunners`(부모 CanvasGroup) + `Runner0/1/2` 3개 SkeletonGraphic 로 재구성. 신규 SerializeField(`profileSO`, `catalog`) 와이어링.

## 구현

### 데이터 해석 (프리팹 배선만, 씬 배선 0)

`SceneTransition` 에 공유 SO 참조 2개 추가 — persistent 프리팹이 프로젝트 에셋을 직접 참조(씬 오브젝트 아님 → 부트스트랩 계약 무해):

```csharp
[SerializeField] private PlayerProfileSO profileSO;   // guid e610b4c0bbbeb4becb09188e400e6a55
[SerializeField] private DefenderCatalog catalog;     // guid 346c00d9daf72429d94ddd29337c7d23
```

`PlayerProfile.asset` 은 로비/배틀이 공유하는 in-memory 캐시(씬 로드를 넘어 생존) — 러너 해석 시점에 `SelectedSquad()` 가 최신 확정 스쿼드를 담는다.

### 러너 슬롯

- 단일 `loadingSpine` 필드 제거 → `[SerializeField] private SkeletonGraphic[] loadingRunners;` (프리팹에 3개).
- `loadingSpineGroup` 은 `LoadingRunners` 부모(빈 RectTransform)의 CanvasGroup 으로 이동 — 3러너 페이드를 **하나의 그룹 알파**로 제어(기존 페이드 로직 그대로).
- 3러너는 프리팹에서 **원근감 있는 어긋난 대형**으로 authoring — x/y 오프셋 + 앞뒤 스케일 차(가까운 러너 크게, 먼 러너 작게)로 달리는 무리의 깊이감. 수치는 전부 프리팹 소유(계약 #6).
- 러너 아래 **한글 로딩 캡션** `LoadingCaption`(TextMeshProUGUI, `꿈결 진입 중..`, Jua SDF 폰트) — `LoadingRunners` 자식이라 러너와 같은 CanvasGroup 으로 함께 페이드. 코드 참조 없는 정적 텍스트(프리팹 authoring).

### 스쿼드 → 러너 주입

전환 시퀀스에서 러너를 노출하기 직전 `ConfigureRunners()` 1회:

1. `squad = profileSO?.profile?.SelectedSquad()`.
2. `ids = SquadDraw.Resolve(squad.unitIds)` — 슬롯 순서·중복 제거. `skeletonDataAsset != null` 인 유닛만 채택 후 **매 로드 `UnityEngine.Random` 으로 셔플**해 앞 N(=러너 수)개를 뽑는다(로드마다 다른 3인). 3인 이하면 셔플 후 전원 사용.
3. 러너 i 에 대해:
   - 채택 유닛이 있으면: (dataAsset 이 다르면 재할당 후 `Initialize(true)`) → `SpineCombinedSkinCache.Apply(runner.Skeleton, unitData)` → `AnimationState.SetAnimation(0, loadingAnimation, true)` → 러너 활성.
   - 없으면(스쿼드가 3 미만): 러너 비활성(hide).
4. **폴백**: 채택 유닛이 0개면(스쿼드 미선택 / 스켈레톤 없는 로스터 / 테스트 모드) 모든 러너를 authoring 기본 스킨(`full_skins`)으로 그대로 노출 — 로딩 화면이 절대 비지 않는다.

모든 defender 가 Casual Character 단일 리그를 공유(`ee98f82…`)하므로 실질적으로 dataAsset 재할당은 발생하지 않고 스킨만 교체된다. 그래도 일반 케이스는 방어적으로 처리.

## 완료 기준

- compile 0 error. `SceneTransitionSmokeTest` 통과(기존 계약 회귀 없음).
- Play 로비→배틀: 확정 스쿼드의 앞 3유닛이 서로 다른 외형으로 함께 러닝. 배틀→로비도 동일.
- 스쿼드 유닛 <3: 있는 만큼만 러닝(빈 슬롯 hide). 스쿼드 미선택/테스트 모드: 기본 러너 3인 러닝(폴백).
- Spine/콘솔 에러 0. 러너 페이드 인/아웃이 기존과 동일(단일 그룹 알파).

**검증 2026-07-10**: compile 0 에러, 프리팹 force 임포트 0 에러(한글 TMP YAML 정상), 계층 구조 확인(`get_hierarchy` — LoadingRunners/{Runner0,Runner1,Runner2,LoadingCaption}), `SceneTransitionSmokeTest` 2회 통과(프리팹 인스턴스화 + `ConfigureRunners` 폴백 경로 무에러). 라이브 오버레이 픽셀은 에디터 툴링으로 캡처 불가(Screen Space Overlay + 에디트모드 SkeletonGraphic 한계, uGUI 버튼 클릭 무스크립트) — **대형/캡션 위치 육안 확정은 Play 확인 필요**.

**사용자 Play 확인 2026-07-10 (커밋 `ce623932`)**: 로비↔배틀 전환 시 확정 스쿼드 러너 대형·한글 캡션 정상. unit 4 및 scene-transition spec 전체 완료 마감.

## 후속 후보

- **대형/좌우 반전 authoring variant**: 방향별(로비→배틀 vs 배틀→로비) 러닝 대형 차별화.
- **등장 stagger**: 3인이 동시가 아니라 약간의 시차로 프레임인.
