# 1. 기존 풀 맵 15폭 이하로 축소

## 목적

풀에 20폭 맵이 4장 있다. 카메라가 보드에 auto-fit(`BattleBridge.cs:4242`) 하므로 20폭 맵에서는 유닛이 작아져 캐릭터가 안 읽힌다. 전 맵을 **15폭 이하**로 맞춰 가독성 기준을 통일한다.

## 변경 대상 (GUID 유지 — 같은 에셋에 덮어쓰기)

- `Assets/_Project/Data/Maps/MapDocument_Serpent.asset` 20×11 → 15×11
- `Assets/_Project/Data/Maps/MapDocument_Coil.asset` 20×12 → 15×12
- `Assets/_Project/Data/Maps/MapDocument_Twin.asset` 20×12 → 15×12
- `Assets/_Project/Data/Maps/MapDocument_Spiral.asset` 20×12 → 15×12
- `Assets/_Project/Data/Maps/MapDocument_Zig.asset` 20×12 → 15×12 (**재조정** — 아래 참조)

`MapDocument_Hook`(13×12)은 이 spec 유닛 0 에서 이미 기준 이하로 만들었다 — 손대지 않는다.

## 핵심 원칙 — 레인 길이를 보존한다

폭만 줄이면 레인이 짧아지고, 웨이브는 **시간 기반**(`triggerTimeSec`)이라 적이 골에 더 빨리 닿는다 = 유효 DPS 창이 줄어 **난이도가 조용히 올라간다**. 점수 예산은 전 맵 동일이므로 이 드리프트를 흡수할 곳이 없다.

따라서 폭을 줄이는 만큼 **꺾임을 추가해 레인 길이를 원본 ±10% 안에 유지**한다. 결과적으로 맵은 더 구불구불해지는데, 이건 이 spec 의 목표(변주)와 같은 방향이라 손해가 아니다.

## 재작성 결과 (전부 검증 통과)

| 맵 | 크기 | 레인 (원본) | 편차 | walk/place/deco | 성격 보존 |
|---|---|---|---|---|---|
| Serpent | 15×11 | 22, 22 (21, 21) | +5% | 46/89/30 | 분리 평행 2레인 + 각자 골 |
| Coil | 15×12 | 22, 21, 23 (24, 25, 25) | −10% | 67/82/31 | 3레인이 **골에서만** 수렴 |
| Twin | 15×12 | 32, 32 (34, 34) | −6% | 66/102/12 | 좌우 대칭 2블록 + 각자 골 |
| Spiral | 15×12 | 17, 19, 17 (19, 19, 21) | −10% | 54/94/32 | 3레인 골 근처 수렴 |
| Zig | 15×12 | 20, 20 (20, 19) | ±0% | 42/42/96 | 대각 반대편 2스폰 · 180° 회전대칭 L자 2레인 |

검증(전 맵 통과): 2×2 walk 블록 0 · 스폰 전원 골 도달 · 고아 walk 셀 0 · **forest**(edges == cells − components, 분리 복도 맵은 컴포넌트 여럿이 정상) · 스폰/골 Walk.

> **Serpent 는 성격이 한 번 꺾인다.** 원본은 좌→우 직선 2줄이 우측 끝에서 골로 내려가는 형태였다. 15폭에서 직선을 유지하면 레인이 21→16 으로 줄어 난이도가 튀므로, 우측 끝에서 **되돌아오는 훅**을 넣어 22 를 맞췄다. 골이 우측 끝(x=18)에서 중앙(x=7)으로 이동한다. 이름과 "분리된 2레인" 정체성은 유지된다.

## 배치칸(Place) 규칙

walk 셀에 Chebyshev 1 인접한 비-walk 셀을 전부 Place, 나머지는 Deco. 기존 4장이 원래 "경로 주변이 넉넉히 배치칸"인 성격이었고(원본 place 87~134), 재작성본도 82~102 로 같은 대역이다. **Deco 를 직접 칠하므로 런타임 커빙(`DesignateDeco`)은 스킵된다.**

## 구현

1. `execute_code` 로 각 맵의 walk 세그먼트를 세우고 Place 링을 계산 → `MapDocumentBuilder.WriteToDocument` 로 **기존 에셋에 덮어쓴다**(GUID 유지 → 풀·씬 참조 무손상).
2. 파생값(`mergeDegree`/`chokepoint`)은 타일 격자에서 재계산 — Map Painter 의 Bake 와 동일 산식.
3. `authoringSeed = -1`, `generatorVersion = 0` (수동 입력 표시) 유지.

## 완료 기준

- [x] 5장 전부 폭 ≤15, 위 표대로 bake — 디스크 재파싱으로 2×2 0건 / 스폰 전원 도달 / 고아 0 / forest / 레인 길이 재확인
- [x] **풀 6장 전부 ≤15 달성** — Serpent 15×11, Coil·Twin·Spiral·Zig 15×12, Hook 13×12
- [x] GUID 5개 불변 (`.meta` 무변경), 풀 참조 무손상
- [x] EditMode green — 1266 중 1264 pass / **0 fail** / 2 skip. testrig 로그에서 동일 GUID 로 정상 임포트 확인(YAML 직접 기입본의 Unity 파싱 검증)
- [x] Play — 개발 override(유닛 2)로 축소본 진입, 정상 렌더·pathing 확인 (확인 2026-07-24)

## 구현 메모

- bake 시점에 UnityMCP 브리지가 끊겨 있어 `execute_code` 대신 **MapDocument YAML 을 직접 기입**했다. 헤더(`m_Script` guid·`m_Name`·`m_EditorClassIdentifier`)를 보존하고 데이터 라인만 교체했으며, `.meta` 는 건드리지 않아 GUID 가 유지된다. 검증은 (a) 디스크 재파싱 (b) testrig Unity 임포트 + EditMode 두 경로로 했다.
- **Zig 재조정 경위**: 이 세션 시작 시점(mtime 01:08)에 워킹트리 Zig 는 다른 세션이 20×12 → **12×10 으로 줄여둔** 상태였다. 그런데 그 축소는 레인을 20/19 → **12/12 (−40%)** 로 깎아 이 유닛이 막으려는 난이도 드리프트를 그대로 만들었고, 풀에서 혼자 최단 레인 아웃라이어가 됐다. 사용자 지시로 커밋본(HEAD) 형태 — 대각 반대편 2스폰 + 180° 회전대칭 L자 — 를 15×12 로 이식해 레인 20/20 을 복원했다. 폭 20 → 15 인데 레인이 안 줄어든 건 원본 L자가 세로변(11) + 가로변(9)로 폭을 다 쓰지 않았기 때문이다.
- 교체 전 워킹트리 판은 `scratchpad/Zig_worktree_backup.asset` 에 백업했다(git 미추적이라 되돌리려면 이 파일 필요).
