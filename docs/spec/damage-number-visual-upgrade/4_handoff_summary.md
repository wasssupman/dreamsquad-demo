# 4 · Handoff Summary — damage-number-visual-upgrade

## Commit

- `ccda445` docs(spec): 스펙(2트랙 critic 반영)
- `3ba0f57` unit 0 — 머리위 앵커(view-space) + 카메라축 격자 겹침 방지
- `831c923` unit 1 — 팔레트 재설계 + 정점 그라데이션 + 모션 + 타이밍 교정
- `9ef174a` unit 2 — 머티리얼 임팩트 룩(하프톤·글로우·흰 아웃라인)
- `3664093` unit 3 — 클러스터 임팩트 스파크(배칭·풀링)

## Implemented

- **머리 위 앵커**: 발치+`headViewOffset` 를 **ToView 이후 view 공간 world-up** 으로 올림(sim-Y 는 ToView 가 버림 — critic BLOCKER 였음).
- **겹침 방지 격자**: 카메라 빌보드축(right/up) 투영 점유 격자 + 위쪽 편향 나선 빈셀 탐색. 셀 중심 스냅 → 격자 배열. 멱등 셀 해제(OnDisable).
- **팔레트**: 흰→흰(실제 단조로움 원인) → 청록→스프링그린→골드→오렌지. `EnsureDefaults` 게이트 버그 수정.
- **정점 그라데이션**: 상단 밝게 + 페이드는 `_tmp.alpha`... (실제는 4-corner 알파, 단색 color 덮어쓰기 제거).
- **타이밍**: `TimeManager.DeltaTime(Battle)` (raw `Time.deltaTime` 정지-미반응 기존 버그 교정).
- **모션**: index 결정론 셰이크 + 미세 회전.
- **머티리얼**: 신규 비-모바일 Distance Field 변종 + 하프톤 face tex(medium@1.0) + 강한 warm 가짜 글로우 + 흰 아웃라인. 프리팹 배선.
- **스파크**: `_SKELETON` 파티클 + LateUpdate 클러스터 배칭 + 풀링. 클러스터당 1개(모바일).

## Key Files

- `Assets/_Project/Scripts/Presentation/DamageNumberSpawner.cs` — 앵커·격자·index·스파크 배칭/풀
- `Assets/_Project/Scripts/Presentation/DamageNumberView.cs` — view-space Play·정점 그라데이션·TimeManager·index 모션
- `Assets/_Project/Scripts/Presentation/DamageNumberStyle.cs` — 팔레트·배치·모션·스파크 튜닝 필드
- `Assets/_Project/Fonts/DamageNumber Impact Mat.mat` — 하프톤/글로우/아웃라인 머티리얼
- `Assets/_Project/VFX/DamageNumberSpark_SKELETON.prefab` + `DamageNumberSpark.mat`
- `Assets/_Project/VFX/Textures/DamageNumbers/` — 코덱스 텍스처 5종
- `Assets/_Project/Scenes/BattleScene.unity` — sparkPrefab 슬롯 배선
- `Assets/_Project/Tests/EditMode/DamageNumberPlacementTests.cs`

## Verified

- compile 0 err(4회 도메인 리로드 후 idle).
- `DamageNumberPlacementTests` 7/7 (MCP run_tests).
- 오프스크린 렌더: 팔레트 램프·하프톤·글로우·아웃라인 인게임 크기 가독성(청록/오렌지), 스파크 버스트 16 파티클 additive.
- 팔레트 라이브 확인(4키 35E0D0→FF6A2A).
- BattleScene diff 21+/10-, 스포너 컴포넌트 필드만(무관 오브젝트 무변경).

## Notes

- **되돌리지 말 것**: ① 앵커는 sim-Y 아니라 view-space(ToView drop). ② 타이밍은 `TimeManager.DeltaTime(Battle)`(raw deltaTime 은 정지 미반응). ③ 격자는 world X/Y 아니라 카메라축 투영. ④ ToView 는 스포너 1곳(View.Play 재변환 금지). ⑤ 페이드는 4-corner 알파(단색 `_tmp.color` 덮어쓰기 금지 — 그라데이션 뭉갬).
- **글로우 결정**: 씬 post-FX OFF·Volume 0 → TMP 가짜 글로우 밴드. 사용자 선택(모바일 안전). 진짜 URP Bloom 은 전역 렌더 변경이라 후속.
- **스파크 프리팹**: VFX 저작 규약상 `_SKELETON` 유지. 사용자 폴리시 후 정식 prefab 화 가능.
- 튜닝값 전부 `DamageNumberStyle`(BattleScene 스포너 인스펙터, Play 실시간) + 머티리얼 `.mat`.

## 강도 rev (2026-07-07, Play 피드백 "잘 안 보인다")

가독성/임팩트 부스트: 머티리얼에 **어두운 드롭섀도(underlay)** 추가(바쁜 배경 분리) + 아웃라인 0.28→0.35 · BattleScene 폰트 5.2~11.7→**8~18** · 펀치 1.6→2.4 · driftUp 0.7→0.9 · 스파크 size/burst/scale 확대(0.62/22/1.4). 값은 전부 라이브(scene/mat).

## Follow-up

- **Play 최종 확인**: combat 중 스파크 발화·클러스터, 전체 룩 육안(사용자 실기/에디터).
- **unit 2 Android 실기 프로파일 게이트**: 비-모바일 셰이더 동시 10+ 팝업 실기 프레임. 실패 시 폴백(underlay-글로우/경량 셰이더).
- 그 외 후속 후보(스킬/속성 색·크리티컬·힐/디펜더 숫자·DoT 합산·SO 승격·유닛별 정밀 앵커·진짜 emissive URP Bloom)는 README 참조.
