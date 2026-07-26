# spawn-point-alert — 스폰 지점 사전 얼럿

**작성일**: 2026-07-20
**상태**: **완료 2026-07-20 (사용자 Play 확인)** — units 0~1 구현·커밋 (`5d996eb0`, `44f06965`),
z-fighting 보정 `086507e1`. handoff: `2_handoff_summary.md`.
**2026-07-26 재개**: unit 3 — 예보 소스를 큐잉된 웨이브로 바꿔 **모든 웨이브**(Wave 1·강제 포함)가
예고를 받는다. 선행 `wave-pattern/11`(스폰 리드인 2초). Play 확인 대기.
선행 `wave-pattern` unit 6(고정 시드, `2d8c843e`)과 함께 작업했다. 실기기 성능만 미측정.
**선행 spec**: `docs/spec/wave-pattern/` (GeneratedWavePlan — 예보의 소스), `wave-pattern/6_fixed_wave_seed.md` (검증 재현성 확보, 선행 권장)

## 목표 (검증 질문)

각 웨이브에서 적이 나올 스폰 지점마다, **첫 적이 등장하기 2~3초 전에** 스폰 지점→골
경로를 따라 흐르는 라인 트레일(명일방주식 진입 예고)이 떠서 플레이어가 방어 준비
방향과 진입 루트를 미리 아는가?

> rev 2026-07-20: 사용자 결정으로 비주얼을 "스폰 타일 위 마커"에서 "스폰→골 경로
> 라인 트레일"로 변경. 예보 산식(unit 0)은 무변경.
> rev2 같은 날: 1차 트레일이 셰브론 아이콘 반복 스탬프라 "그냥 아이콘 표기"로 읽혀
> 폐기. 명일방주 레퍼런스(연속 실선이 스폰→목적지로 그어짐)대로 재구현.

**예약 가능 근거**: 웨이브 타임라인은 배틀 시작 시 `GeneratedWavePlan` 으로 전부
확정되고, lane 배정도 `EffectiveSpawnIndex(= deckIndex % laneCount, 3스폰 기준)`
결정론이라 스폰 시각·지점을 사전 계산할 수 있다. 시뮬을 건드릴 필요가 없다.

## 작업 단위

| 파일 | 작업 구분 | 목적 |
|---|---|---|
| `0_spawn_forecast_math.md` | 순수 함수 | lane 산식 공유 추출 + per-lane 첫 스폰 시각 예보 + EditMode 테스트 |
| `1_alert_view_wiring.md` | 프레젠테이션/배선 | BattleBridge read-only 예보/경로 API + 경로 라인 트레일 뷰 + 씬 배선 + Play 검증 |
| `2_handoff_summary.md` | 인계 | 구현 결과·되돌리면 안 되는 판단·다음 후보 |
| `3_alert_for_every_wave.md` | 계약 반전 (2026-07-26) | 예보 소스를 "큐잉된 웨이브"로 → **모든 웨이브에 예고**(Wave 1·강제 포함). 선행 `wave-pattern/11` |

## 공통 원칙

- **시뮬 무변경**. ECS·웨이브 스케줄(트리거 그리드)은 그대로. 얼럿은 read-only 프레젠테이션이다.
  (2026-07-26: **스폰 타이밍의 리드인 2초는 이 spec 밖**이다 — 웨이브 시간 배정의 소유 spec 인
  `wave-pattern/11_wave_spawn_lead_in.md` 소관. 이 spec 은 그 창을 소비할 뿐이다.)
- 예보와 실제 스폰은 **같은 함수**(`ExpandWave` + `EffectiveSpawnIndex`)를 공유한다. 예보가 어긋나면 산식 버그이며, 공유로 원천 차단한다.
- **예보는 예측이 아니라 큐잉된 웨이브의 사실이다** (unit 3). `QueueWave` 가 실스폰과 같은 인자로
  1회 계산해 넣는다 — 그래서 자동·강제·Wave 1 이 모두 같은 창을 얻는다.
- 프레젠터는 `BattleBridge` read-only API 폴링(NextWaveDock 패턴). Bridge 게이트웨이 준수.
- 트레일 경로는 **유닛 이동과 같은 goal flow field** 를 셀 단위로 따라간다(같은 필드·같은 타이브레이크 = 표시 루트와 실제 진입 루트 일치). 표시 시작 시마다 재조회해 flow 변화(블로킹 해저드 등)를 반영한다.
- 리드 타임 기본 2.5초, 프레젠터 SerializeField (2~3초 범위).
- 표시 창은 lane 별 `[첫 스폰 시각 − lead, 첫 스폰 시각)`. Battle 클럭 기준이라 정지/슬로우 시 자연 동결.
- **모든 웨이브가 예고를 받는다** (unit 3, 2026-07-26). 창 = 웨이브 큐잉 시점 ~ 그 lane 의 첫 적
  등장. 실효 리드 = `min(leadSec, waveSpawnLeadInSec)` — 예보가 큐잉 순간에 생기기 때문.
  - Wave 1(트리거 0초): 배틀 시작 ~ 2초. **unit 1 의 "자연 스킵" 계약은 폐기.**
  - `Next Wave` 강제 호출: 당긴 시점 ~ +2초. **unit 1 의 "예고 없이 즉시 스폰" 계약은 폐기.**
  - legacy `deck.spawns`(생성 웨이브 미사용) 경로는 여전히 예고 없음 — `QueueWave` 를 안 지난다.

## 파이프라인 커버리지

가장 가까운 아키타입: **데미지 넘버** (순수 프레젠테이션 월드 마커).

| 정거장 | 본 spec | 확인 포인트 |
|---|---|---|
| 데이터 | 프레젠터 SerializeField (lead 시간·마커 비주얼 파라미터) | SO 아님 — 데미지 넘버와 동일 관례 |
| ECS | N/A — 시뮬 무관 순수 Mono | |
| 트리거 | N/A(큐 아님) — `BattleBridge` 예보 폴링. 예보는 `QueueWave` 가 큐잉 시점에 채운다(unit 3) | NextWaveDock 과 같은 read-only 폴링 |
| Spawner/Pool/View | `Presentation/SpawnAlertPresenter.cs` + lane 별 LineRenderer 2개(실선 트레이싱 + 흐르는 빛, 절차적 혜성 텍스처) | laneCount 만큼 생성 후 재사용, 풀 불요 |
| 씬 wiring | 프레젠터 GameObject + bridge 참조 | `unity-feature-wiring` 스킬 |

## 후속 후보

- 보스 웨이브 얼럿 차별화 (색/아이콘 — 현재는 크림슨 배너가 별도 존재)
- ~~Wave 1 사전 얼럿~~ → **unit 3 에서 해소**(배틀 시작 0~2초 창). 다만 "배치 페이즈(배틀 시작 전)
  에 미리 노출"은 여전히 별개 후보 — 시작 시점 예지가 필요하다.
- 얼럿 SFX (ElevenLabs 파이프라인)
- 안내를 라인 외 채널로 보강 (웨이브 번호 배너 등) — 현재는 경로 라인만. `NextWaveDock`·보스
  배너와의 역할 중복 판단이 선행.
