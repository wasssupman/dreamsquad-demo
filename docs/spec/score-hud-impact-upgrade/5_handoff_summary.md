# 5 · Handoff Summary — score-hud-impact-upgrade

> **완료 2026-07-07** — units 0~3(시각) 구현·커밋·Play 검증 통과. 사운드(unit 4)는 **후속 후보로 이관**(미착수, ElevenLabs 클립 확보 시 착수). merge `0bad98a4` 로 origin(battle-log/lobby-keyring/스킵로그인)과 통합, `BattleBridge` 가 `OnEnemyKilled(EnemyKillScoreDelta=10)` 로 구동.

## Commit

- `87d3d752` docs(spec): 스펙 + SoundManager 싱글톤 예외 (CLAUDE.md §5 / TRD §5.2)
- `b559d136` unit 0 — PrimeTween 탄성 슬램 + 화이트핫→골드 + Kanit 폰트 + AoE 병합
- `d2e3b833` unit 1 — 골드 4점 스파클 임팩트 버스트
- `0274a04d` unit 2 — 발광 백라이트 + 대각 샤인 스윕
- `079be28b` unit 3 — 패널 킥 + 마일스톤 화면 플래시
- `2be826de` fix — 샤인 스윕 약화(은은한 글린트, Play 피드백)

## Implemented

- 처치당 연출: 숫자 탄성 슬램(PunchScale) + 화이트핫→골드(Tween.Color) + 골드 스파클 방사 + 글로우 flare + 대각 샤인 글린트 + 패널 킥. 100점마다 화면 가장자리 골드 플래시.
- **같은-프레임 처치(AoE) 병합**: `OnEnemyKilled` 누적 → LateUpdate flush 1회 → 강도 스케일된 단일 연출. 점수는 킬당 +10 합산(표시 전용 불변).
- **폰트 교체**: Anton → **Kanit Bold Italic**(다이내믹 이탤릭). Kanit SDF + Kanit Outline Mat + OFL.
- 파티클/글로우/샤인/비네트는 **절차적 UGUI Image**(ScreenSpaceOverlay 위 실제 PS 불가) + 자체 스프라이트(ScoreSpark/Glow/Shine/Vignette, 절차 생성) + `Wassup/UI/Additive` 셰이더.
- **가독성 우선**: additive 글로우 flash 절제(0.55→0.22)로 골드 숫자를 안 덮게. 샤인은 은은한 글린트.

## Key Files

- `Assets/_Project/Scripts/UI/ScoreHudView.cs` — 앵커·롤·펀치·플래시·글로우/샤인 타이머·킥·마일스톤·병합 flush
- `Assets/_Project/Scripts/UI/ScoreBurstPool.cs` — 풀링 UGUI 쿼드 버스트(결정론 방사·중력·전역상한)
- `Assets/_Project/Shaders/UI_Additive.shader` + `Assets/_Project/VFX/Score Additive.mat`
- `Assets/_Project/VFX/Textures/` — ScoreSpark·ScoreGlow·ScoreShine·ScoreVignette (절차 생성 스프라이트)
- `Assets/_Project/Fonts/` — Kanit SDF.asset · Kanit Outline Mat.mat · Kanit-BoldItalic.ttf · Kanit-OFL.txt
- `Assets/_Project/Scenes/BattleScene.unity` — ScoreHud 컴포넌트 필드(색·폰트·버스트·글로우·샤인·킥·마일스톤 값)

## Verified

- compile 0 err(다회 도메인 리로드 후).
- 오프스크린 렌더로 폰트/버스트/글로우·샤인/비네트 육안 확인(스크래치 PNG: `Assets/Screenshots/`).
- **Play 검증 통과**(사용자) — units 0~3 전체. 샤인 약화 후 "훨씬 자연스럽다".
- BattleScene diff는 매 유닛 ScoreHud 필드만(무관 오브젝트 무변경). 병렬 outgame-login 작업과 파일 교집합 0.

## Notes (되돌리면 안 되는 의도)

- **AoE 병합**: OnEnemyKilled 은 누적만, 연출 트리거는 LateUpdate flush 1회. 처치당 개별 트리거로 되돌리면 광역 처치 시 폭주.
- **가독성**: 글로우 flash 알파는 낮게 유지(0.22). 올리면 골드 숫자를 덮어 점수가 안 읽힘.
- **시간축**: 전부 unscaled(PrimeTween `useUnscaledTime:true` / 수동 `unscaledDeltaTime`) — 드캐 모달(timeScale=0) 중에도 동작. `Time.timeScale` 안 건드림.
- **트윈 수명**: 펀치/색/킥 핸들 보유 → OnDisable·phase-exit Stop + 리셋. 유령 트윈 금지.
- **배틀 카메라 불건드림**: 화면 피드백은 UI-space 패널 킥 + 풀스크린 UGUI 비네트만.
- **셰이더 빌드안전**: 빌트인 legacy additive 는 스트립 위험 → 자체 `Wassup/UI/Additive`. 파티클 스프라이트도 자체 생성(빌트인 UI 스프라이트는 `Resources.GetBuiltinResource` 런타임 null).
- **씬 위생**: BattleScene 저장 때마다 `DamageNumberSpawner.sparkColorBoost:2.2`(코드 기본값) 재유입 — 매 커밋 전 그 라인 제거함(damage-number 소관, score-hud 무관). 다음 세션도 저장 시 재확인.

## Follow-up

- **unit 4 (사운드)** — SoundManager + 처치 틱(피치 상승). ElevenLabs Text-to-Sound-Effects 로 저작-시점 SFX 생성 → 로컬 재생(런타임 API 금지). API 키 확보 후 착수. 계약: `4_sound-soundmanager.md`.
- 후속 후보(README): 연속처치 heat·킬 위치 "+N" 플로팅·콤보 배수·진짜 URP Bloom.
- 실기(Android) 프레임 확인: 동시 다처치 시 쿼드/드로우콜(전역 상한 72로 캡). 필요 시 상한 하향.
