# 0 — Blocked() 분리: close-trigger vs tap-gate (+조준 중 줌 피드 중단)

> rev 2 (2026-07-29 critic 반영): 게이트 삽입 위치 명시(L2) + 조준 중 줌 피드 중단(M4) 편입.

## 목적

`DcInspectController.Blocked()` 는 현재 "선택을 강제로 닫는 조건"과 "새 탭을 막아야 할 조건"을
한 판정으로 묶어 매 프레임 `Close()` 한다. 손패 오픈·Active 조준이 선택과 **공존**해야 하는
이 feature 의 전제라, 두 성격을 분리한다. 조준 중에는 인스펙트 줌 피드도 끊는다 —
`inspectDolly 4.92 + lookWeight 0.5` 가 걸린 채 Meteor 타일/포탈 출구를 고르게 하면 안 된다
(이전에는 `IsAiming → Blocked → Close` 가 줌을 풀어 줬다 — critic M4).

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs`

## 구현

`Blocked()` 를 두 판정으로 나눈다:

- **`MustClose()`** (선택을 닫는다 — 기존 `Blocked()→Close()` 유지):
  - 배치 드래그/arm: `drag.IsDragging || drag.HasArmedUnit` — 트레이 조작 = 단일 세션 원칙
  - 이동모드: `relocationController.InMoveMode`
  - (페이즈 이탈·사망은 기존 별도 경로 그대로)
- **`TapGated()`** (새 탭 후보만 무장 금지 — 선택·리티클 유지):
  - `handView.State == Hand` — 손패 오픈 중 보드 탭은 catcher 소유(unit 2)
  - `GameManager.IsAiming` 또는 `drag.IsAiming` — 조준 중 탭 오독 방지(기존 근거 유지)

`Update()` 흐름:

1. `if (MustClose()) { _pendingTap = false; Close(); return; }`
2. 줌 피드: `if (!AimingNow()) FeedZoomTarget();` — `AimingNow() = GameManager.IsAiming ||
   drag.IsAiming` (조준 종료 후 staleness 2프레임으로 줌 자동 복귀 — 계약 13 정합. 손패
   오픈만으로는 끊지 않는다 — 선택 중 줌 유지가 기본).
3. 탭 후보 게이트는 **press 무장 분기 앞**에 둔다: `if (TapGated()) { _pendingTap = false; return; }`
   (뒤에 두면 press 프레임에 무효 — critic L2. 릴리즈 유실 방어 `!pointer.press.isPressed`
   경로는 게이트 앞에서 처리해 stale `_pendingTap` 이 남지 않게 한다.)

주의:

- 기존 주석의 근거(카드 드래그 touchup 재발 방지, armed race)는 판정별로 이관해 보존한다.
- `IsAiming` 이 close-trigger 에서 빠지므로 Active 카드 드래그·포탈 2탭 중에도 선택이 살아
  있다. 리티클 대체/재주장은 unit 4, 조준 프레이밍은 위 2 의 피드 중단이 담당한다.
- 이 unit 단독의 행동 변화는 "손패 오픈/조준이 선택을 안 죽인다 + 조준 중 줌 해제" 까지다.
  손패 오픈 트리거(unit 1)·탭 라우팅(unit 2)은 여기서 구현하지 않는다.

## 완료 기준

- [ ] compile 클린
- [ ] Play: 유닛 선택 → 항아리 탭으로 손패 열기 → 선택(줌·리티클·패널) 유지, 카드 D&D 로
      다른 유닛 부착 정상(조준 중 선택 잔존 — 리티클 겹침은 unit 4 전 임시 상태로 허용)
- [ ] Play: 선택 중 ActiveTile 카드 드래그 → 조준 시작 시 줌이 풀리고(홈 프레이밍) 커밋/취소 후 줌 복귀
- [ ] Play: 선택 중 트레이 드래그 시작 → 선택 즉시 해제(기존 동작 보존)
- [ ] Play: 선택 중 이동모드 진입 → 선택 해제(기존 동작 보존)
