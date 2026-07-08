# Keyring Unify — 설계 요약 (2026-07-08)

## 목표

인게임(월드)/아웃게임(UGUI) 키링 연출의 중복 로직을 공유 코어로 통합하고, 아웃게임 홀로그램 비주얼을 인게임으로 이식. 타 프로젝트 이식가능성은 지식 이식(가이드 문서)으로.

## 아키텍처 요약

- **KeyringSim** (순수 static): SpringStep / LeanAngle / FallStep — 두 컨트롤러의 중복 수학 통합. 동작 무변경, 수치 스냅샷 테스트로 고정.
- **KeyringStyle** SO: 스프라이트 2 + UI 머티리얼 2 + 월드 머티리얼 2 — 스타일 단일 소스, 2단 폴백(SO null → 전체 절차적 / 슬롯 null → 요소별).
- **KeyringHologramCommon.hlsl**: 홀로 효과 공유 include (순수 float, t 파라미터) — UGUI(CG)·URP(HLSL) 양쪽 컴파일. 월드용 `WorldCordHologram.shader` 신설.
- 렌더 rig 는 컨텍스트별 유지 (UGUI Image vs LineRenderer/SpriteRenderer) — rig 추상화는 흡수 비용 > 계약 크기로 기각.

## 결정 기록

- 통합 수준: **공유 코어 + 스타일 SO** (rig 추상화 기각, 비주얼-only 기각) — 사용자 선택 2026-07-08.
- 이식성: **지식 이식만** (asmdef/UPM/폴더 이동 기각) — 사용자 선택 2026-07-08.
- critic 리뷰 (Fable, OMC critic 레인): **APPROVE_WITH_CHANGES** — MAJOR 4건(vertex color 갈색 오염 / unit 3 자기완결성 / CG↔HLSL include 제약 / same-frame A/B 검증) 전부 spec 계약으로 반영.

## 구현 상세

`docs/spec/keyring-unify/` (README + units 0~4).
