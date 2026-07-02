# 0 — Audit + Frame Contract

## 목적

upright flip 이 실제로 영향 주는 프랍 집합을 전수 조사하고, upright 저작 프레임/블롭 회전 계약을 확정한다. 코드 변경 없음(조사 + 계약).

## Audit 결과 (2026-07-02, live 측정)

flip 되는 루트(`_backgroundPropsRoot`/`_ringPropsRoot`)로 흐르는 프랍 = 활성 테마 풀. `forest`/`desert` 의 `playAreaProps`+`distantRingProps` 전수:

### forest (10종) — flip 준비 완료

- visualOffset **전부 0** (선행 접지 fix `c6c77dc`/`f395afd` 로 정리됨). visualOffset 재검증 부담 없음.
- 블롭: 전부 `localRotation=(0,0,0)` identity + 회전프레임 localPosition:
  - flower/rock/pine: `(0, 0, -0.20)` — local −z=월드 높이(+0.20 lift)
  - tree_1x4: `(0, 0.38, 0)`, barrel: `(0, 0.60, -0.20)`, log: `(0, 0.45, -0.20)` — local +y=월드 깊이(depthOffset)
- → 현재 블롭은 **부모 90° 에 의존해 XZ 바닥에 눕는다.** upright flip 시 identity 블롭이 **수직으로 서 버림** → 재저작 필수.

### desert — 접지 결함 잔존 (별도 이슈)

- `prop_style_small_rock/boulder_cluster/dead_tree/skull_sign/ruin_wall/crates_barrel/tree_stump/fallen_log` + `prop_dummy_rock/log`: **아직 `billboardMode=FullCamera` + nonzero visualOffset(+y 0.46~0.93)**. desert 는 forest 와 **별개 PropData 카피**라 접지 fix 미적용.
- 이들은 upright flip 이전에 **접지 fix(Tilted + BottomCenter + offset 0)** 가 먼저 필요하다. flip 만으로는 안 고쳐지고, 오히려 +y offset 이 월드 +Y 로 바뀌며 center-pivot sink 와 겹쳐 예측 불가.

## 스코프 결정 (critic B1/m6 확정)

- **In-scope**: 활성 테마 풀 프랍의 **블롭 재저작**(upright 프레임) + 루트 flip. forest 풀은 즉시 대상.
- **Out-of-scope (follow-up 이관)**:
  - **desert 접지 fix** — desert `prop_style_*`/`prop_dummy_*` 를 forest 와 동일하게 Tilted/BottomCenter/offset 0 재임포트. 이건 프레임이 아니라 접지 결함(선행 fix 의 desert 미적용분). 별도 작업.
  - **None 프랍 8종** — backdrop/Legacy3D 전용, flip 루트 미경유 (README 비목표).
  - **Legacy `MapView.cs:796`** — Legacy3D 전용 자체 프레임.
  - `_structurePropsRoot` — 이미 upright.

## Frame Contract (upright 전환 후)

props 루트가 `localRotation=Euler(-90,0,0)` 로 upright 가 된 뒤의 저작 관례:

- **+Y = 월드 위, XZ = 바닥 평면** (인스펙터 직관과 일치).
- **visualOffset**: `+Y` = 위로 들어올림 (기존 "─z=위" 예외 폐기). 현재 forest 값은 전부 0 이라 flip 후에도 0 유지.
- **블롭 lay-flat**: `localRotation = Euler(90,0,0)` 로 쿼드를 XZ 바닥에 눕힌다 (기존엔 identity + 부모 90° 의존).
- **블롭 위치 축 스왑**: 기존 `(x, depthY, -0.20)` → upright `(x, 0.20, depthZ)`. 즉 **높이=local +Y, 깊이=local +Z**.
- `PropDataEditor.AttachAuthoredBlob` 가 이 관례로 저작하도록 정정하고, **preservation 분기(`:109-114`)를 마이그레이션 시 우회**한다(M2).

## 완료 기준

- 영향 프랍 목록 확정 (본 문서 audit 표).
- upright frame contract 문서화 (위 섹션) — unit 1 구현의 계약 소스.
- desert 접지 fix 를 follow-up 으로 분리 (README/backlog 반영).
