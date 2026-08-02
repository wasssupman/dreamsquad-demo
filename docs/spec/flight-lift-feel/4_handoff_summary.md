# 4 — Handoff Summary

## Commit

| 해시 | 제목 |
|---|---|
| `68d1c33f` | docs — spec 신설 (README + 0~3) |
| `3743abb0` | unit 0 — 비행 구간 시간 재매핑 순수 함수 |
| `fe6a54f1` | unit 1 — 뜬 높이의 시각 반응 단일 정의 |
| `4874e7c5` | unit 2 — 디펜더 비행이 lift 를 동반 전달 |
| `3c364ed3` | unit 3 — 비행 리듬 + 착지 눌림 |
| `9674b4bc` | unit 3 fix — lift 노브 라이브 튜닝 |
| `60041776` | tune — 아치 높이 3.5 → 4.5 |
| `c6f6405e` | fix — 코드 리뷰 지적 4건 |
| `b5e1525f` | docs — 계약 정정 3건 + 열린 결정 3건 |
| `ebf7238c` | tune — 아치 높이 4.5 → 6.0 |
| `5cde8fc7` | docs — handoff 작성 + 검증 기록 |
| `1746a731` | rev — 비행 중 블롭이 착지 타일에 남게 (기저선 앵커) |
| `bff474a1` | tune — PC 도 블롭 경로 + 블롭 알파 0.5 (+ 관찰용 도약 2초) |
| `61c28f5d` | tune — 관찰용 드롭 2초 (`deploymentDuration` 동반 상향) |
| `50cafa76` | tune — 관찰용 시간값 롤백 + Play 확인 기재 · spec 종료 |

## Implemented

- `KeyringSim.FlightTimeRemap(u, power)` — ease-out-in 재매핑. `power=1` 항등 조기 반환.
- `UnitLiftVisual.Resolve(lift, …)` — lift 하나에서 유닛 배율 · 그림자 배율 · 그림자 알파를 함께 파생.
- `SpineUnitView`/`QuadUnitView` 의 `ApplyRenderScale` — 스케일 쓰기 단일 지점(`_baseScale × _flightScale
  × _punchScale × _squash`). `PunchRoutine` 을 `_punchScale` 슬롯으로 전환.
- `BlobShadow.SetFlight(scaleMul, alphaMul)` — 지면 Y 고정은 유지, 크기·알파만 반응. 알파는
  `dim × flight` 2배수를 각자 보관해 곱한다.
- `SetDefenderViewOverride`/`SetFlightView` 가 lift 를 동반 전달(기본값 0 = 항등). 좌표 체계 무변경.
- 드롭·도약에 재매핑 적용(비행 구간만) + 착지 스쿼시 명시 발화.
- `BattleBridge` 노브 8개(lift 5 · 도약 리듬 3) + `DragSwaySettings` ⑩ 3개. `MirrorLiftKnobs` 가
  `LateUpdate` 에서 매 프레임 미러 → Play 중 실시간 튜닝.
- 아치 높이 6.0(드롭 SO · 도약 씬). 실제 apex ≈ 2.4 world.
- **비행 중 블롭 접지 앵커** — 드롭·재배치 아치가 `camUp` 이라 유닛 XZ 가 밀린다. 그림자는 아치
  기저선 위에 남아 착지 타일을 가리킨다(`BlobShadow.SetGroundAnchor`, 정상 피드가 자기해제).
- **`useRealShadows` 0** — PC 도 블롭 경로. 유닛 그림자는 조명이 아니라 앵커라는 룩 판정.
  바닥 receive 머티리얼·프랍 cast 도 함께 블롭 룩으로 넘어간다(상호배타 계약).

## Key Files

- `Assets/_Project/Scripts/UI/KeyringSim.cs` — 재매핑(+ 기존 `DismountPoint`)
- `Assets/_Project/Scripts/Presentation/UnitLiftVisual.cs` — lift → 3 배율
- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs` — `ApplyLift` / `ApplyRenderScale` / `SquashRoutine` / `Kill`
- `Assets/_Project/Scripts/Presentation/BlobShadow.cs` — `SetFlight` / `ApplyColor`
- `Assets/_Project/Scripts/Bridge/BattleBridge.Relocation.cs` — 오버라이드(lift 축) + `PlayLandingSquash`
- `Assets/_Project/Scripts/Bridge/BattleBridge.BossLeap.cs` — 도약 재매핑 + 착지
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` · `DefenderRelocationController.cs` — lift 계산

## Verified

- **EditMode 1790 중 1788 통과 · 실패 0 · skip 2**(기존 Ignored 2건). 각 유닛마다 실행.
- unit 0 은 TDD — 스텁으로 **신규 5건만 기대한 이유로 실패(RED)** 확인 후 구현(GREEN).
- compile 클린(각 유닛 후 `read_console` 로 CS 에러 0 확인).
- 수치 실측(`execute_code`): 재매핑 `p=0.7` 에서 u=0.1→0.162 / 0.5→0.5 / 0.9→0.838(대칭·체공 성립).
  lift 2.4 → 유닛 ×1.336 · 그림자 ×0.64 · 알파 0.48.
- 독립 코드 리뷰 1회 — **APPROVE-WITH-CHANGES**. 지적 4건 수정(`c6f6405e`), 계약 3건 문서 정정(`b5e1525f`).

- **사용자 Play 감각 확인 통과(2026-08-02)** — 드롭·도약을 각각 2초로 늘려 관찰한 뒤 원 수치 복귀.
  관찰용으로 전 디펜더 `deploymentDuration` 을 2초로 올렸다가(`61c28f5d`) 롤백 커밋에서 0.45 로 되돌렸다.

**미검증(남김)**: PlayMode e2e 미작성. 안드로이드 실기기 확인(현재 PC·모바일 모두 블롭 경로라
룩은 같지만 프로파일은 미측정).

## Notes (되돌리면 안 되는 의도)

- **스케일 쓰기는 `ApplyRenderScale` 단일 지점.** `transform.localScale` 직접 대입을 되살리면 매 프레임
  피드와 코루틴(펀치·스쿼시)이 서로를 조용히 지운다.
- **`SquashRoutine` 의 `k` 는 시간 증분 앞에서 적용한다.** 뒤로 옮기면 authored `amount` 에 도달하지
  못하고 세기가 프레임레이트에 비례해 갈린다(30fps 에서 절반).
- **`Kill()` 의 배율 원복을 지우지 말 것.** Kill 이후엔 `UpdatePosition` 이 오지 않아(`NotifyDeath` 가
  같은 프레임에 풀에서 제거) 확대가 굳는다. 넉업 정점 처치는 상시 경로다.
- **재매핑은 비행 구간에만.** 반동까지 왜곡하면 힘 모으는 타이밍이 흔들린다. 총 시간 불변이라
  "비행 창 ⊆ pending 창" 계약이 산다.
- **lift 는 원시 높이지 비율이 아니다.** 정규화 정책을 소비처가 알면 경로마다 갈라진다.
- **노브 소유 구분**: lift 반응 = 전역 단일(원근 보상) / 리듬·눌림 = 연출별 이중(취향). 뒤집지 말 것.
- **착지 스쿼시는 명시 호출.** "lift 0 이면 자동" 은 취소·teardown 에서 오탐이 터진다.

## Follow-up

1. **lift 축 2배 불일치 — 수용 상태로 남음.** 드롭·재배치는 `camUp` 투영(=화면 세로 상승분), 도약·넉업은
   월드 +Y 를 lift 로 넘긴다. pitch 60° 라 `camUp.y = 0.5` → **정확히 2배**. Play 에서 거슬리지 않아
   수용했으나 **계약 2 의 "같은 높이 = 같은 크기" 는 이 코드에서 참이 아니다.** 다섯 번째 소비처를
   붙이는 사람은 README "열린 결정" 1 을 먼저 읽을 것.
2. **탭 배치 던지기 적용** — 같은 인프라. `defender-drop-dismount` 후속 후보의 남은 절반.
3. **먼지 VFX · 카메라 킥** — 착지 임팩트의 나머지.
4. **테스트 공백** — `UnitLiftVisual.Resolve` 는 `BattleBridge` static 을 직접 읽어 EditMode 로 고정
   불가. 재매핑 적용 규약(`recoilFrac + (1−recoilFrac)·Remap(u)`)이 두 파일에 복제됐는데 테스트 없음.
5. **`liftScaleMax` 포화** — 아치 6.0 의 apex 2.4 에서 유닛 배율이 1.336 으로 상한(1.35)에 붙었다.
   아치를 더 올리려면 이 노브를 같이 올려야 크기 단서가 따라온다.
6. **안드로이드 실기기 확인** — PC·모바일 모두 블롭 경로가 되어 룩은 같지만 프로파일은 미측정.
   `useRealShadows` 를 끈 것이 모바일 성능에 주는 영향은 (원래 모바일이 블롭이었으므로) 없다 —
   달라진 것은 PC 쪽이다.

**해소된 항목**: 실그림자 경로 그림자 반응 no-op(`bff474a1`) · 블롭 착지 타일 미끄러짐(`1746a731`).
