# Spec — Keyring Unify (로직·비주얼 통합 리팩토링)

> 상태: **승인 2026-07-08 — 구현 중** (critic 리뷰 APPROVE_WITH_CHANGES 반영, 비주얼 = 로비 동일 스타일로 시작 + 시인성 문제 시 머티리얼 파라미터만 튜닝)
> design: `docs/plans/2026-07-08-keyring-unify-design.md`

## 상위 목표

인게임(`DefenderDragPlacementController`, 월드 스페이스)과 아웃게임(`LobbyKeyringDrag`, UGUI 캔버스 px)에 따로 구현된 키링 연출의 **중복 수학을 공유 코어로 통합**하고, 아웃게임에서 완성된 **홀로그램 비주얼을 인게임으로 이식**한다. 다른 프로젝트 이식가능성은 **지식 이식**(가이드 문서 1개)으로 담는다 — asmdef/폴더 이동 없음 (2026-07-08 사용자 결정).

## 검증 질문

1. 인게임 드래그 프리뷰가 아웃게임과 같은 시안→마젠타 홀로그램 키링(발광 빔 줄 + 홀로 링)으로 보이는가?
2. 리팩토링 전후 양쪽 키링 동작(스프링 추종/기울임/낙하)이 수치 등가인가?
3. 타 프로젝트에서 키링 연출을 재구축할 때 필요한 지식이 가이드 문서 1개로 전달되는가?

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_keyring_sim.md` | `KeyringSim` 추출 + 두 컨트롤러 전환 + 테스트 | 로직 통합 (동작 무변경) |
| 1 | `1_keyring_style_so.md` | `KeyringStyle` SO + 아웃게임 슬롯 이전 | 비주얼 단일 소스 |
| 2 | `2_hologram_include_world_shader.md` | 홀로 효과 HLSL include + 월드 셰이더/머티리얼 | 셰이더 공유 기반 |
| 3 | `3_ingame_rig_style.md` | 인게임 rig 스타일 적용 + Play 검증 | 홀로그램 이식 본체 |
| 4 | `4_portability_guide.md` | 이식 가이드 + 구 spec 계약 정리 | 지식 이식 |
| 5 | `5_handoff_summary.md` | 인계 요약 | 종료 시 작성 |

## Feature-wide 계약

1. **동작(수학) 무변경.** unit 0 은 순수 리팩토링 — 현행 상수 기반 수치 스냅샷 EditMode 테스트로 등가를 고정한다.
2. **`KeyringSim` = 순수 static, 좌표계 비의존.** Vector3 기준(아웃게임은 z=0). dt clamp(`Mathf.Max(dt,1e-4f)`)·초기화·재잡기·좌표 산출은 호출측에 잔류.
3. **`LeanAngle` 내부 정규화 금지.** 호출측 입력 그대로(인게임=단위벡터의 camRight/camUp 투영값 = 비단위 2D, 아웃게임=단위 2D). eps `1e-3` floor 유지 — 스케일 불변 아니지만 현행 동작 보존이 우선.
4. **`KeyringStyle` SO = 스타일 단일 소스** (ringSprite / cordSprite / uiCordMaterial / uiRingMaterial / worldCordMaterial / worldRingMaterial). 2단 폴백: `style == null` → 전체 절차적(현행), style 내 슬롯 null → 해당 요소만 폴백.
5. **팔레트는 머티리얼 소유** (UI/월드 2곳 중복 수용) — UGUI 는 MaterialPropertyBlock 미지원이라 SO 팔레트 런타임 주입은 clone 수명 관리 비용이 더 크다. SO 헤더에 "팔레트 변경 = 머티리얼 2곳" 명시.
6. **공유 include = self-contained 순수 float 함수.** `_Time` 비참조(t 파라미터), UnityCG/URP 헤더 include 금지 → CGPROGRAM(UGUI)·HLSLPROGRAM(URP) 양쪽 컴파일. `fixed`→`float` 정밀도 변화는 "비주얼 무변경"의 허용 예외로 문서화.
7. **스타일 머티리얼 적용 시 vertex color = white 강제.** `cordColor`(DragSwaySettings 기본 갈색)는 절차적 폴백 전용 — 홀로 셰이더가 vertex color 를 곱하므로(`col.rgb *= IN.color.rgb`) 갈색 틴트 오염 방지.
8. **인게임 스타일 주입 = `DragSwaySettings.style` 필드.** `Configure` 시그니처 무변경, `CreateInstance` 폴백 시 style null → 절차적 자동 성립.
9. **셰이더 회귀 검증 = same-frame A/B 오프스크린 렌더.** 전 효과(스캔라인/플리커/펄스/글리치)가 `_Time` 구동이라 전/후 별도 캡처 diff 는 무의미 — 구 셰이더 사본과 신 머티리얼을 같은 프레임에 나란히 렌더해 diff.
10. **lobby-keyring-drag 계약 1("인게임 무변경·코드 공유 없음") 은 본 spec 으로 대체.** unit 4 에서 해당 README 에 폐기 주석 + 포인터 추가.

## 파이프라인 커버리지

N/A — 전투 시뮬 플레이 오브젝트가 아닌 드래그 프리뷰/로비 연출 오브젝트. 생성→렌더 정거장 구조는 keyring-cord-preview / lobby-keyring-drag 에서 확립된 경로 그대로이고 비주얼 소스(절차적→스프라이트/머티리얼)만 교체된다. `docs/reference/object-pipeline-map.md` 대상 아님 (lobby-keyring-drag 와 동일 사유).

## 후속 후보 (현 스코프 밖)

- 로프 스타일 월드 머티리얼 + `KeyringStyleRope` 에셋 (현재는 홀로그램만 월드 이식 — UI 로프 에셋은 보존됨).
- 중력 드롭 방식 · 줄 sag 곡선 · 착지 먼지 VFX (keyring-cord-preview / lobby-keyring-drag 후속 후보 승계).
- 드래그 중 전용 매달림 애니메이션.
- 홀로 팔레트의 인게임 전용 변형 (전투 배경/하이라이트와 충돌 시).
