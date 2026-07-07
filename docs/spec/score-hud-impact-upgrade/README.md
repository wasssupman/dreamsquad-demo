# score-hud-impact-upgrade

> 상태: **시각 완료 2026-07-07** — units 0~3 구현·커밋·**Play 검증 통과**(샤인 약화 `2be826de` 반영). 사운드(unit 4)는 ElevenLabs 클립 확보 후로 게이트(미착수). 커밋: `b559d136`(0)·`d2e3b833`(1)·`0274a04d`(2)·`079be28b`(3).

## 검증 질문

*"전투 중 적을 처치해 점수가 오를 때, 데미지 숫자와는 **다른 고유의 강렬한 보상감**으로 — 탄성 슬램 + 파티클 + 발광 + 화면 킥(+후속 사운드)이 함께 터지며 — 인지되는가?"*

## 상위 목표

라이브 점수 HUD(`ScoreHudView`, score-hud spec 완료 2026-06-05)의 현재 juice(수동 Lerp 카운트업 + 펀치 1.45x + 흰→골드 색 플래시)를 **정말 강렬한 임팩트**로 끌어올린다. 점수는 게임의 중요 지표이므로 처치마다 강하게 인지되며 올라가야 한다.

**기존 파이프라인 유지**: ECS `EnemyKilledEventsSingleton` → `BattleBridge` 드레인 → `ScoreHudView.OnEnemyKilled()` (킬당 +10, 표시 전용). 이 업그레이드는 **100% MonoBehaviour Presentation 계층**이다.

## 결정된 방향 (브레인스토밍 2026-07-07)

- **스코프**: 카운터 자체 juice 고도화 + 파티클 + 화면 피드백 + (후속) 사운드. **제외**: 킬→점수 플로팅, 콤보 배수 메커닉, 적별 차등 점수 (전부 후속 후보).
- **고유 시각 정체성 (데미지 폰트와 분리)**: 데미지 숫자 = Bangers SDF + 마그니튜드 다색(청록→골드→오렌지) + 라운드 도트. 점수 = **Kanit Bold Italic SDF(스포티 이탤릭·다이내믹) + 단일 "골드 보상(molten gold)" 톤** — 화이트핫→리치 골드 플래시, 탄성 오버슈트 슬램, 방사형 골드 스파크/코인 버스트. 이탤릭 기울기가 "달려 올라가는" 모션감을 주고, 다색 램프가 아닌 단일 프리미엄 골드라 데미지(Bangers 코믹)·기존 Anton(정적 직립)과 확실히 구분. (폰트 교체 결정 2026-07-07, score-hud spec 의 Anton 선택을 대체.)
- **애니 엔진**: PrimeTween(프로젝트 UI juice 관용구, `DraftCardVfxDriver` 선례). 현재 수동 Lerp 대체.
- **파티클**: ScreenSpaceOverlay HUD 위에선 실제 `ParticleSystem`이 렌더 안 되므로 **UiEmber식 절차적 UGUI Image 쿼드**(`DraftCardVfxDriver` 패턴) 사용. 모바일 안전.
- **사운드**: SoundManager 도입(싱글톤 제약 해제, 사용자 결정). 클립은 **ElevenLabs Text-to-Sound-Effects로 저작-시점 생성** → 로컬 재생. 런타임 API 호출 금지. API 사용은 일단 미룸.

## 작업 단위

| # | 문서 | 작업 | 에셋 의존 |
|---|---|---|---|
| 0 | `0_elastic-punch-gold-flash.md` | 탄성 펀치 + 화이트핫→골드 플래시 (수동 Lerp → PrimeTween), 카운트업 롤 강화 | 없음 (즉시) |
| 1 | `1_impact-burst-particles.md` | 처치당 방사형 골드 스파크/코인 UiEmber 절차 파티클 (풀링·결정론 분산) | 기존 텍스처 재활용 |
| 2 | `2_glow-shine-sweep.md` | 숫자 발광 펄스 + 대각 샤인 스윕 (가짜 글로우, 모바일 안전) | `.mat` (자체 저작) |
| 3 | `3_screen-feedback.md` | UI-space 패널 킥/셰이크 + 마일스톤 화면 가장자리 플래시(선택) | 없음 |
| 4 | `4_sound-soundmanager.md` | **[게이트]** SoundManager + 처치 틱(피치 상승) 배선 | **ElevenLabs 클립** ⚠️ |

**0~3은 에셋 의존 0/최소 → 즉시 구현·검증. 4는 ElevenLabs 클립 확보 후.**

## Feature-wide 계약

- **ECS·점수 로직 불변**: `EnemyKilledEvent`/채널/enqueue/드레인/점수값(킬당 +10, 표시 전용, ResultScreen 공식 무관) 건드리지 않는다.
- **데미지 폰트와 시각 분리**: Anton SDF + 단색 골드 아이덴티티 유지. 데미지=Bangers 다색과 통일하지 않는다.
- **하드코딩 금지**: 팔레트·펀치·버스트 수/크기·글로우·셰이크·피치 등 튜닝값 전부 `[SerializeField]` / `.mat` / 클립 에셋. (TRD §5)
- **애니 엔진 = PrimeTween**. HUD 연출은 **시간 기반 허용** — 시뮬의 index-결정론 원칙은 재현성 대상인 전투 시뮬에만 적용, HUD 연출은 재현 대상 아님.
- **시간축**: `unscaledDeltaTime`/`unscaledTime` 유지 — 드래그캐처 모달(`timeScale=0`) 중에도 롤/펀치 동작(기존 `ScoreHudView` 의도).
- **파티클**: 풀링된 UGUI Image 쿼드(UiEmber 패턴). 실제 `ParticleSystem` / 캔버스 `ScreenSpaceCamera` 전환 없음. 버스트당 개수 상한 + **전역 동시 쿼드 상한**(둘 다 직렬화). 풀 반납 시 누수 0.
- **같은-프레임 처치 병합 (AoE 안전)**: `OnEnemyKilled()` 는 BattleBridge 드레인에서 한 프레임에 여러 번 호출될 수 있다(AoE/전멸). 이를 **프레임 카운터로 누적**하고, 프레임당 1회 flush 로 **강도 스케일된 단일 연출**(버스트/펀치/사운드 각 1회, 처치 수에 비례 강화)을 낸다. 점수값은 여전히 킬당 +10 합산(연출만 병합, 스코어링 불변). 데미지 스펙의 "cluster당 1스파크" 원칙과 동일 결.
- **트윈 수명**: 진행 중 `Tween` 핸들을 필드로 보유 → `OnDisable`·phase-exit 시 `Stop()` + 스케일/색 리셋(`DraftCardVfxDriver.OnDisable` 선례). 비활성 RectTransform 위 유령 트윈 금지.
- **씬 값 재저작**: `baseColor`/`flashColor` 등은 `BattleScene` MonoBehaviour 블록에 이미 직렬화돼 C# 필드 기본값이 shadow 된다. 색/톤 변경은 **씬 컴포넌트에서 재저작**(MCP, 수작업 금지)하되, BattleScene write 격리 위생(스냅샷→checkout HEAD→내 delta 재적용→커밋→복원)을 따른다.
- **배틀 카메라 불건드림**: 화면 피드백은 UI-space(패널) + 풀스크린 UGUI 오버레이만. 배틀 카메라 transform/FX 무변경 (데미지 스펙과 동일 원칙).
- **진짜 URP Bloom 미사용**: 글로우는 TMP/오버레이 가짜 글로우(모바일 안전). 진짜 발광은 후속(전역 렌더 변경).
- **사운드**: 저작-시점 ElevenLabs 생성 클립을 로컬 재생. 클립 미할당 시 no-op(무음 안전). 런타임 API 호출 금지.

## 파이프라인 커버리지

**N/A** — HUD/UGUI Presentation 기능. 월드 플레이오브젝트(유닛/적/투사체/해저드/VFX)의 생성→렌더 경로를 신설·변경하지 않는다. 임팩트 버스트는 HUD-space UGUI Image 쿼드로, 월드 `VfxSpawner` 파이프라인을 타지 않는다. `object-pipeline-map.md` 갱신 불필요.

## 후속 후보 (현 스코프 밖)

- **연속처치 heat 상승** — 빠른 연속 킬이 버스트/글로우/셰이크를 시각적으로 가열(점수 배수 아님). 콤보 메커닉으로 번질 위험 → 분리. [S]
- **킬 위치 "+10" 플로팅 연결** — `EnemyKilledEvent.position`(현 reserved) 활용, 처치 위치에서 점수로 날아드는 연출. [M]
- **콤보/연속처치 배수 스코어링** — 표시 로직에 스코어링 메커닉 추가. 점수 모델 변경. [M]
- **적별 차등 점수** — 적 SO에 bounty 필드. [S]
- **진짜 URP Bloom** — 배틀 카메라 post-FX + HDR 숫자. 전역 렌더 변경·모바일 풀스크린 비용. 별도 스펙. [M]
- **SFX 다양화** — 마일스톤 플러리시, 라운드 종료 팡파레 등. unit 4 확장. [S]
