# 0 — Blocked() 분리: close-trigger vs tap-gate

## 목적

`DcInspectController.Blocked()` 는 현재 "선택을 강제로 닫는 조건"과 "새 탭을 막아야 할 조건"을
한 판정으로 묶어 매 프레임 `Close()` 한다. 손패 오픈·Active 조준이 선택과 **공존**해야 하는
이 feature 의 전제라, 두 성격을 분리한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs`

## 구현

`Blocked()` 를 두 판정으로 나눈다:

- **`MustClose()`** (선택을 닫는다 — 기존 `Blocked()→Close()` 유지):
  - 배치 드래그/arm: `drag.IsDragging || drag.HasArmedUnit` — 트레이 조작 = 단일 세션 원칙
    (relocation 계약 11 결과 동일 근거)
  - 이동모드: `relocationController.InMoveMode`
  - (페이즈 이탈·사망은 기존 별도 경로 그대로)
- **`TapGated()`** (새 탭 후보만 무장 금지 — 선택·줌·리티클 유지):
  - `handView.State == Hand` — 손패 오픈 중 보드 탭은 catcher 소유(unit 2)
  - `GameManager.IsAiming` 또는 `drag.IsAiming` — Active/방향 조준 중 탭 오독 방지(기존 근거 유지)

`Update()` 흐름: `if (MustClose()) { _pendingTap=false; Close(); return; }` →
`FeedZoomTarget()` → 탭 후보 처리 앞에 `if (TapGated()) { _pendingTap = false; }` 로 무장/판정만
스킵(줌 피드·릴리즈 유실 방어는 계속 돈다).

주의:

- 기존 주석의 근거(카드 드래그 touchup 재발 방지, armed race)는 **판정별로 이관**해 보존한다.
- `IsAiming` 이 close-trigger 에서 빠지므로, Active 카드 드래그 중에도 선택이 살아 있다 —
  리티클 대체/재주장은 unit 4 가 담당(이 unit 에서는 손대지 않는다).
- 이 unit 단독으로는 행동 변화가 "손패 오픈/조준이 선택을 안 죽인다" 뿐이어야 한다.
  손패 오픈 트리거(unit 1)·탭 라우팅(unit 2)은 여기서 구현하지 않는다.

## 완료 기준

- [ ] compile 클린
- [ ] Play: 유닛 선택 → 항아리 탭으로 손패 열기 → 선택(줌·리티클·패널) 유지, 손패 카드 D&D 로
      다른 유닛 부착 정상(조준 중 선택 잔존 — 리티클 겹침은 unit 4 전 임시 상태로 허용)
- [ ] Play: 선택 중 트레이 드래그 시작 → 선택 즉시 해제(기존 동작 보존)
- [ ] Play: 선택 중 이동모드 진입 → 선택 해제(기존 동작 보존)
