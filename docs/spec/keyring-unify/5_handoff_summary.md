# 5 · Handoff Summary — keyring-unify

> 인계 지도. 최신 계약은 README + 번호 문서 우선. 여기는 커밋/위험 지점 압축.

## Commit

- `2d85b508` spec 초안 (critic APPROVE_WITH_CHANGES 반영)
- `76bcb69f` unit 0 — KeyringSim 추출 (동작 무변경)
- `143db2f8` unit 1 — KeyringStyle SO + 아웃게임 슬롯 이전
- `cb221775` unit 2 — 홀로 효과 include + 월드 셰이더/머티리얼
- `e4b15c28` unit 3 — 인게임 rig 홀로그램 적용
- `d63973bb` unit 4 — 이식 가이드 + 구 spec 계약 폐기

## Implemented

- 인게임/아웃게임 중복 키링 수학(스프링/기울임/낙하)을 순수 static `KeyringSim` 으로 통합. bit-exact 스냅샷 테스트로 등가 고정.
- `KeyringSim` Vector2 포워딩 오버로드 — 아웃게임 호출측 마샬링 풋건 흡수(리뷰 반영).
- `KeyringStyle` SO — 스프라이트 2 + UI/월드 머티리얼 4슬롯 단일 소스. 2단 폴백(SO null → 전체 절차적 / 슬롯 null → 요소별).
- 홀로 효과(그라데이션/스캔라인/플리커/펄스/글리치)를 `KeyringHologramCommon.hlsl` 로 추출 — CG(UGUI)·HLSL(URP) 양쪽 컴파일.
- `WorldCordHologram.shader` 신설(URP unlit 가산, `_LengthAxis` uv 전치). 인게임 고리=SpriteRenderer+홀로 링, 줄=LineRenderer+월드 홀로 머티리얼.
- 인게임 드래그 프리뷰가 로비와 동일 스타일 에셋(`KeyringStyleHologram.asset`)·동일 셰이더 함수로 홀로그램 렌더.
- 이식 가이드 `docs/reference/keyring-portability.md` (함정 11건).

## Key Files

- `Assets/_Project/Scripts/UI/KeyringSim.cs` — 공유 수학
- `Assets/_Project/Scripts/Data/KeyringStyle.cs` + `Data/Config/KeyringStyleHologram.asset` — 스타일 소스
- `Assets/_Project/Shaders/KeyringHologramCommon.hlsl` / `UICordHologram.shader` / `WorldCordHologram.shader`
- `Assets/_Project/Art/KeyringCordHologramWorld.mat` / `KeyringRingHologramWorld.mat` — 인게임 색·발광 튜닝처
- `Assets/_Project/Data/Config/DragSwaySettings.asset` — 인게임 움직임·굵기 튜닝처(Play 중 실시간)
- `docs/reference/keyring-portability.md` — 이식 지도

## Verified

- compile·콘솔 클린. EditMode 키링 7개 통과(무관 사전실패 2: ObstaclePlacerTests, SkyFallTests.FallProgress_ZeroPortion).
- same-frame A/B diff 0픽셀(UGUI include 재구성 무변경), 오프스크린 렌더로 인게임/아웃게임 홀로 + 절차적 폴백 확인.
- 사용자 Play 육안 확인 통과 2026-07-08.

## Notes (되돌리면 안 되는 의도)

- **월드 셰이더 uv swap**: 효과 축(lenUv)만이 아니라 텍스처 샘플 uv 도 전치해야 함(unit 2 실적발). `_LengthAxis`: 줄=1, 고리=0.
- **vertex color white 강제**(계약 7): 스타일 적용 시 LR/SR 색 white. `cordColor`(갈색)는 절차적 폴백 전용 — 홀로 셰이더가 vertex color 곱하므로 오염 방지.
- **공유 include**: 순수 float + t 파라미터, `_Time`/헤더 비참조 유지(CG↔HLSL 호환).
- **KeyringSim 계약**: dt clamp·초기화·재잡기·좌표 산출은 호출측. LeanAngle 내부 정규화 금지.
- **팔레트 중복 수용**: UI/월드 머티리얼 2곳(UGUI MPB 미지원).

## Follow-up (스코프 밖)

- 인게임 전용 홀로 팔레트(전투 배경/하이라이트 충돌 시) — 별도 `KeyringStyle` + 머티리얼 세트. 현재는 로비와 공유.
- 로프 스타일 월드 머티리얼 + `KeyringStyleRope` 에셋 (UI 로프 에셋은 보존됨).
- SkyFallTests.FallProgress_ZeroPortion 사전실패 — Meteor 잔재 테스트-구현 계약 불일치(`FallProgress(1,0)`=0 반환 vs 테스트 1 기대). keyring 무관, 별도 수정 후보.
