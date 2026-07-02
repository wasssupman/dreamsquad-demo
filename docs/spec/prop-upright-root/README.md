# prop-upright-root — 프랍을 90° 타일맵 루트에서 떼어 upright 저작 프레임으로

상태: 완료 2026-07-03 (units 0~1, forest Play PASS. desert 접지는 follow-up)

## 문제

배경/원경 프랍 GameObject가 **90°X 회전된 타일맵 루트**의 자식으로 매달려 있다(측정: 프랍 `rootEuler=(90,0,0)`). 그 결과 프랍의 **저작/트랜스폼 프레임이 뒤집힌다**:

- prop-local **+Y = 월드 +Z(깊이)**, prop-local **−Z = 월드 +Y(높이)**.
- `visualOffset`(Visual localPosition), 블롭 그림자 위치, 자식 오브젝트를 저작할 때 "위"가 +Y가 아니다. 매번 이 예외를 외워야 하고, 안 외우면 축을 틀린다.
- 실측 근거: 코드 접근권이 있는 에이전트조차 루트 회전을 측정하기 전엔 Y/Z를 반복해서 틀렸다. 프랍을 건드릴 때마다 반복되는 인지 비용.

배치 **로직**은 문제 없다 — 이미 논리 셀(x,y) → `CellCenterToWorld` → **월드 좌표**(`transform.position`)로 배치된다. 오염된 건 **결과 GameObject를 회전 루트에 매다는 계층 한 지점**뿐이다.

## 목표

프랍의 트랜스폼/저작 프레임을 **월드-업(+Y=위)** 으로 통일한다. 타일맵 렌더 회전과 배치 로직은 **무변경**.

## 비목표 / 스코프 밖 (critic 반영)

- 배치 로직 변경 금지 (타일 좌표 의존은 옳은 커플링).
- **None 모드 프랍 8종(`prop_edge_*`·`prop_concept_*`)은 무관** — `backdrop_S1_forest.asset` edgeProps 로만 존재하고 `BackdropMounter.Mount`(=`BattleBridge.cs:719`, `!UseTilemapView` Legacy3D 전용)로 렌더된다. 이미 identity 루트·빌보드 disabled·upright. **이 spec 이 flip 하는 `_backgroundPropsRoot`/`_ringPropsRoot` 로 안 흐른다.** 리스크 아님, 건드리지 않음.
- **Legacy `MapView.cs:796` prop 경로 out-of-scope** — 자체 프레임(`Euler(0,yaw,0)`), Legacy3D 전용. 이번에 안 건드린다(미래 편집자 오손 방지 위해 명시).
- `_structurePropsRoot` 는 이미 upright(`TilemapMapView.cs:341`) — 무변경.

## feature-wide 계약

- 배경/원경 props 루트를 **upright 로 역회전**한다: `_backgroundPropsRoot`(`TilemapMapView.cs:294`)·`_ringPropsRoot`(`:424`)에 `localRotation = Euler(-90,0,0)` 추가 (`_structurePropsRoot` 선례와 동일). 프랍 위치는 이미 `transform.position`(월드, `:319`)로 세팅 → **placement 코드 무변경**, 로컬 프레임만 upright.
- upright 저작 관례: **+Y = 위, XZ = 바닥 평면**.
- **빌보드 회전은 무영향** (`BillboardRotation.Compute` = 월드 쿼터니언, `PropBillboard.cs:65` `target.rotation` 월드 세팅) → Tilted/FullCamera/YAxis orientation 회귀 없음. **단, `visualOffset` 은 `localPosition`(부모 프레임)이라 전 모드 위치가 flip 영향받는다** — 회전만 무영향, 위치는 아님.
- 블롭 그림자는 upright 프레임에서 **XZ 바닥에 눕도록** 명시 회전(`Euler(90,0,0)`) + 위치 축 스왑(현재 `(0, depthOffset, -0.196)` → upright `(0, 0.196, depthOffset)` 계열)으로 재저작. 저작 지점은 `PropDataEditor.AttachAuthoredBlob`.
- **블롭 마이그레이션 트랩(M2)**: `AttachAuthoredBlob:109-114` 는 기존 블롭 transform 을 그대로 복사하고 return 한다 → 단순 재생성은 **옛 −z 블롭을 보존**해 무력화. 마이그레이션은 이 분기를 우회(또는 기존 블롭 clear)해야 한다.
- `visualOffset` 에는 −z 코드 경로가 없다 — 현재 authored 값은 전부 **+y**(예: `y:0.55`·`0.93`). flip 시 이 +y 들이 월드 +Z→+Y 로 바뀐다. `PropDataEditor:118-119` 의 "local −z=높이" 주석은 **블롭 한정**이며 visualOffset 과 무관(주석 정정 대상).

## 작업 단위

| # | 문서 | 작업 | 완료 기준 |
|---|---|---|---|
| 0 | `0_audit_and_frame_contract.md` | flip 루트로 흐르는 **실제 프랍 집합** 열거(`forest.playAreaProps`+`distantRingProps`, FullCamera/Tilted) 중 **nonzero visualOffset·블롭 보유 프랍** 목록화. upright 저작 관례/블롭 회전 계약 확정. out-of-scope(None/backdrop, MapView legacy) 명문화 | 영향 프랍 목록 + 계약 문서화 |
| 1 | `1_upright_flip.md` | `_backgroundPropsRoot`/`_ringPropsRoot` 에 `Euler(-90,0,0)` + `AttachAuthoredBlob` upright 프레임 정정(+ **preservation 분기 우회**) + 영향 프랍 프리팹 블롭 재생성. **원자적**(루트 flip 과 블롭 재생성은 함께여야 블롭이 안 눕는다). visualOffset +y 는 재검증(대개 0/소수 — unit0 목록 기준) | compile + EditMode(아래) + Play 육안: Tilted 기립·블롭 XZ 바닥·visualOffset +Y=위 |
| 2 | `2_handoff_summary.md` | 회귀 확인 + 스테일 주석(`PropDataEditor:118-119`) 정정 + handoff | 스크린샷 회귀 없음, 주석 정합 |

### EditMode 테스트 (m7)

flip 후 background/ring props 루트 아래 프랍의 **월드 basis 가 upright(회전 ≈ identity)** 임을 assert. `_structurePropsRoot` 결과와 동일 basis. 순수 트랜스폼 불변식이라 Play 없이 검증 가능 — Play 스크린샷 의존을 보완.

## 검증 질문

> "프랍을 인스펙터에서 저작할 때 +Y 가 화면상 '위'로 직관적으로 동작하고, Tilted 기립·블롭 접지가 유지되는가?"

배경/프랍 변경은 Play→스크린샷 육안 검증 필수 (memory: `feedback_background_screenshot_verify`).

## 리스크 / 롤백 (critic 반영)

- 핵심 메커니즘(루트 `Euler(-90,0,0)`)은 `_structurePropsRoot` 선례로 검증됨 — 저위험.
- **실제 함정은 블롭 preservation(M2)** — unit1 이 이걸 우회 안 하면 "블롭 XZ 바닥" 기준이 조용히 실패한다. 롤백 안전성도 M2 해결 전제.
- None/backdrop 리스크는 없음(rev1 오판 정정).

## 참고

- 레퍼런스: `TilemapMapView.InstantiateStructureProps` (`_structurePropsRoot.localRotation = Euler(-90,0,0)`, `:341`).
- 선행 fix: `c6c77dc`(BottomCenter), `f395afd`(prop_style_* 접지). 이번 spec 은 그 위에서 프레임 자체를 바로잡는다.
- 대안(계층 유지형): visualOffset 을 `PropBillboard` 에서 월드-업 변환. upright 루트 채택 시 불필요 — 미채택 시 fallback 으로만.
