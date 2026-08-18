# 8 — Handoff Summary (2026-08-19)

## Commit

브랜치 `feature/map-diorama-stage` (main 팁 기반, behind 0). 주요 커밋:
`7c7bf005`(설계+spec) → `27d383b5`(critic 반영) → `c05d1993`(unit 0) → `d8b82ed1`(unit 1+2 코드) → `6e60af32`(unit 2 에셋/씬) → `1caffef2`(unit 3) → `d41b5c11`+`a60bf3c5`(unit 4) → US-004b/5 커밋들 → unit 7(68파일 은퇴) + 아키텍트 정정.

## Implemented

- 디오라마 스테이지 파이프라인 전면 교체: `MapStage`+마커 저작 → `MapStageScanner`/`DioramaMapBuilder`(순수) → `GeneratedMap` 무변경 합성(열림=Walk/차단=Deco, placeMask 직접 조립)
- 브리지 스테이지 경로: 폴백 리니어·시드 커빙 은퇴, 연결성 실패 = 하드 실패, `AlignGridTo` 단일 grid writer, 스테이지 수명 = `TeardownGeneratedMap`
- 바닥 페인팅/절차 프랍 은퇴 · 오버레이 7채널 존치 · `BoardSortOrder` 폭 종속 stride + 대역 4000
- 골 균열/붕괴/앵커 = 마커 뷰 훅(브리지 등록부), 튜토리얼 브리지 앵커 교체
- e2e 스테이지 이관: `MapSlot` 포트, KayKit 스테이지 9종(픽스처·파일럿 포함), 덱/플랜 짝 승계, 이격 배치 정규화
- 구 파이프라인 68파일 은퇴 (후계 매핑 기록)

## Verified

- EditMode 두 lane **2397 그린** · PlayMode 스모크 Passed · PlayMode 전체 148 pass / 18 잔존(분류 기록) · 아키텍트 **APPROVED**(정정 6건 반영)

## Notes (되돌리면 안 되는 의도)

- 계약 11: 공성·본능·적 마음·Env **비가용** — 병합 = StructureMarker 후속까지 공성 기능 부재 (사용자 결정 필요, 아키텍트 지적)
- `grid.transform` writer 는 `AlignGridTo` 하나 — Initialize **앞** 호출 순서 불변
- BattleScene 커밋에 타 spec 발 재직렬화 churn 포함(NextWaveDock orphan 키 정규화 — 무해, 기록됨)
- Ralph 러너 2종(`RalphTestRunner`/`RalphEditorTasks`)은 검증 채널로 잔존 — 삭제 판단은 사용자

## Follow-up

- **사용자**: 육안 검증 축 5종(spec 5) · OutgameScene dev 패널 `pool` 수동 배선+저장 · 공성 부재 병합 판단 · push 승인
- **US-007(병합 게이트)**: 순서 의존 10(기제 미확인) · SceneTransition(브랜치 씬 편집 의심) · 환경 의심 4 · FlyingEnemy/SlimeSplit/Tween — main 기준선 대조
- spec 후속 후보: 접근 C · 물 영역 · LOS · 웨이브 재밸런스 · 라이브 맵 재저작
