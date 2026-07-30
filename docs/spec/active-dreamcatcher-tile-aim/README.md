# Active Dreamcatcher Tile Aim — 액티브 사용방식 통일 (화살표 + 타일 지정)

> 상태: **완료 2026-07-30** — units 0~3 구현 + 리뷰 rev 반영 + 사용자 Play 육안 확인. 인계는 `4_handoff_summary.md`
> rev(2026-07-30, code-review REQUEST CHANGES 반영): 보드 밖 엄격 판정(H1) · 퇴화 포탈 거절(H2) ·
> range 0 단일 셀 점등(M1) · Focus null 가드(M2) · 월드 생존 가드(M3) · 2단계 입구 표식 보존(M4)

## 배경

한 손패 안에서 카드의 물리가 두 종류로 갈려 있었다.

| 기존 모드 | 대상 | 카드 | 조준 표현 |
|---|---|---|---|
| `Defender` | 부착(Unit/Squad) + Active-유닛 | 손패 고정 | 화살표 + 락온 링/리티클/콜아웃 |
| `EnemyMark` | 적 표식 | 손패 고정 | 화살표 + 최근접 적 픽 |
| `ActiveTile` | 운석·감속장·회오리 | **포인터 추종** | 범위 프리뷰만 |
| `ActivePortal` | 포탈 | **포인터 추종** | 릴리즈=입구 → 2번째 탭=출구 |

Active 타일 계열만 "카드를 집어 보드에 내려놓는" 배치 D&D 문법을 써서, 같은 손패에서
문법이 3개가 됐다. 포인터 추종은 손패 하강 예외(`IsPointerFollowing`)까지 파생시켰다.

동시에 대상축(`SkillTargetType`)도 실체가 없었다 — 유닛 대상 스킬(공격폭증·속사)은
부착이 아니라 **모디파이어 지속시간**이고, 커밋 인자도 이미 타일이다. "유닛에 붙는 카드"의
락온 문법을 쓸 근거가 없다.

## 목표

Active 6종을 **화살표 + 타일 지정** 하나로 통일하고, 대상축을 폐기해 "Active = 타일 기준
범위 효과"로 개념을 정리한다.

## 사용자 확정 결정 (2026-07-30)

1. **타일 기준 범위 효과로 재정의.** `SkillTargetType.DefenderUnit` 은퇴. 공격폭증·속사는
   지정 타일 반경 내 **아군 전부**에 모디파이어를 건다.
2. **`range` 기본값 1(3×3).** 에셋 값이며 언제든 조정 가능.
3. **포탈 2단계 = 화살표 기점이 입구로 이동.** 1단계는 다른 Active 와 완전히 동일.
4. **units 0~3 연속 진행** 후 테스트/투트랙 리뷰.

## 작업 단위

| 파일 | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 시뮬·데이터·문안 | `0_tile_target_unification.md` | 대상축 폐기 + 아군 광역 버프를 `CastSkillAtTile` 로 수렴 + 카드 문안 |
| 1 | 조준 통일 | `1_tile_aim_mode.md` | `AimMode.TileAim` 단일화 — 카드 손패 고정 + 화살표 + 범위 프리뷰 |
| 2 | 포탈 2단계 | `2_portal_two_step.md` | 입구 확정 후 화살표 기점 이동 + [입구,출구] 동시 점등 |
| 3 | 검증 | `3_play_validation.md` | PlayMode 테스트 + Play e2e 6종 + 회귀 |

**순서 의존**: 0 → 1 필수. 의미를 먼저 옮기고 표현이 따라간다 — 반대로 하면 3×3 프리뷰를
보여주면서 1기만 걸리는 거짓 피드백 구간이 생긴다. 2 는 1 위에. 3 은 마지막.

## Feature-wide 계약

1. **모든 Active 는 타일 대상.** `SkillTargetType` enum + `SkillData.target` 필드 삭제.
   "적을 겨누나 아군을 겨누나"는 `SkillEffectType` 로 판별한다 — **새 필드를 만들지 않는다**.
2. **`range` = 효과 반경(체비셰프 타일).** 변환은 `GridMath.RangeToTiles` 단일 경로.
   `range 0` = 지정 타일 1칸.
3. **캐스트 창구는 `CastSkillAtTile` + `CastPortal` 둘.** `CastSkillOnDefender` 와
   컨트롤러의 `CommitActiveDefender` 은퇴. 신규 캐스트 경로 금지(awakening-hand 계약 6 계승).
4. **범위 내 아군 판정은 단일 소스.** `CollectAlliesInRange` 하나를 실제 적용(`ApplyAllyBuff`)과
   조준 카운트(`CountDefendersInRange`)가 공유한다 — UI 예고와 커밋 결과가 구조적으로 일치.
   `PendingDeployment` 유닛은 양쪽에서 제외.
5. **아군 버프만 대상 0기 = 커밋 실패(무차감).** 적 대상 장판·해저드는 0기여도 성공 —
   빈 곳 선점이 전술적으로 유효하기 때문. 이 비대칭은 의도다.
5-1. **스킬 아군 버프는 전용 모디파이어 슬롯(`SkillAllyBuffStackId = 3`)을 쓴다** (사용자 결정
   2026-07-30). 슬롯 규약은 `on-place=0 · 시너지=1 · 효과타일=2 · 스킬 아군 버프=3 · 드림캐쳐=100+`.
   구 `CastSkillOnDefender` 는 on-place 와 같은 0 을 썼는데, merge 키가 같으면 magnitude 가
   **덮어써지므로** 뒤늦게 배치된 가디언 오라(×1.3)가 각성치를 지불한 ×2.0 을 내려버렸다.
   대상이 1기일 때는 드문 사고였지만 반경 확대로 흔해졌다 → 분리해 **합산**한다.
   같은 스킬 재캐스트는 같은 키라 여전히 refresh(멱등).
6. **조준 물리는 하나**: 카드는 손패에 고정(1.08배), 화살표가 겨눈다. 카드가 포인터를
   따라가는 모드는 없다 — `IsPointerFollowing` 및 그 손패 하강 예외 삭제.
7. **타일 조준 끝점 = 조준 타일 월드센터의 스크린 좌표.** 유닛 락온의 `lockCenter` 와 같은
   역할이라 선이 타일에서 끝난다.
8. **범위 프리뷰 = 기존 `SkillAim` 채널.** 타일맵 range/cells 는 서로를 지우는 단일 채널이라,
   포탈 2단계는 `[입구, 출구호버]` 두 셀을 한 번에 칠한다(`SetSkillAimCells`).
   **`range 0` 은 `SetSkillAimRange` 로 칠 수 없다** — `SetPlacementRange` 가 `tileRange<=0` 에서
   자기 `ClearPlacementRange` 앞에 조기 return 해, 아무것도 안 칠하면서 채널 소유권만 가져간다
   (직전 텔레그래프가 남고 해제 시 그걸 지운다). 반경 0 은 단일 셀 점등 경로를 쓴다.
9. **취소 무차감 4경로 불변**: 손패 영역 릴리즈/탭 · 보드 밖 · ESC · phase 이탈.
   **"보드 밖" 은 엄격 판정을 요구한다** — 기존 `TryScreenToCell` 은 `GridMath.WorldToCell` 의
   격자 clamp 때문에 맵 밖(빈 배경)에서도 가장자리 셀에 true 를 준다. 타일 조준은
   `TryScreenToCellStrict`(= `WorldToCellUnclamped` + bounds)를 쓴다. 관대한 판정이 맞는
   부착/적 표식은 기존 함수를 유지한다.
9-1. **포탈은 입구 == 출구를 만들 수 없다.** `MovementSystem` 의 포탈 스냅이 flow step **앞**에
   돌아서 같은 타일 링크는 반경 내 적을 매 프레임 되돌리는 정지 필드가 된다(카드 1장 값의
   광역 CC). UI 는 조준 단계에서 거절하고, `CastPortal` 도 창구에서 거절한다(이중).
   릴리즈 직후 포인터가 아직 입구 위인 상태는 **불가(붉음)가 아니라 안내(무색)** 로 표시하고
   "출구 타일을 탭하세요" 를 유지한다.
10. **선택 중 Active 차단 유지**(use-flow 계열 07-29 결정) — 통일 후에도 선택 중 손패는 부착 전용.
11. **밸런스 수치는 에셋에서만.** `range 1` 외 magnitude/duration/costActive 는 이 spec 범위 밖.

## 파이프라인 커버리지

`docs/reference/object-pipeline-map.md` 대상 아키타입 신설 **없음** — 기존 스킬 캐스트
파이프라인(텔레그래프·투사체·해저드·모디파이어)을 호출만 한다. 생성→렌더 경로 무변경이라
전 정거장 **N/A**. 변경은 조준 UI + bridge 캐스트 창구 통합 + 에셋 값이다.

## 후속 후보 (현 spec 범위 밖)

- **범위 내 아군 초록 하이라이트** — 지금은 상태줄 카운트로 예고한다. 체감 후 판단
  (틴트 합성은 `ApplyInvalidSweep` 선례 재사용).
- **손가락 오클루전 오프셋** — 조준 타일이 손끝에 가려지는지 실기기 확인 후.
- **적 대상 / 아군 대상 범위 프리뷰 색 구분** — 지금은 같은 aim 타일.
- **Active 전용 카드 아트** (현재 uiTint/스킬명 폴백) — awakening-hand 에서 이관된 잔여 후보.
- **`SkillData.cooldownSec`/`cost` 완전 삭제** — Active 흡수 후 dormant 상태 유지 중.
