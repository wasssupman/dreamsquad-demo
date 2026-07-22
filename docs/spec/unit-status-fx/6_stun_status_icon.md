# 6 — Stun 상태 아이콘 (별)

## 목적

Stun 상태(CcKind.Stun — 적·아군 공통 action-lock)를 머리 위 별 연출로 표시한다. Sleep(Zz)
아이콘(unit 5)의 정확한 미러 — 본 spec 의 "새 상태 = registry 항목 + reconcile 훅 + 글리프"
계약 두 번째 적용. **동기**: 스턴이 수면과 시각적으로 구분 안 돼(둘 다 정지) 폭탄맨 스턴탄
적용이 눈으로 확인 불가했음(로직은 정상 — 관측 문제).

## 변경 대상

- `Assets/_Project/Scripts/Data/StatusFxKind.cs` — `Stun = 6` append
- `Assets/_Project/Scripts/Data/StatusFxRegistry.cs` — `FallbackGlyph.Stars = 2` append
- `Assets/_Project/Scripts/Presentation/StatusFxView.cs` — Stars 절차 글리프(+자 별 3개)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `ReconcileStatusFx` CcEffect 루프에 Stun 판정 동승
- `Assets/_Project/Data/Config/StatusFxRegistry.asset` — Stun 항목 추가

## 구현

1. **enum**: `StatusFxKind.Stun = 6` (append-only).
2. **글리프**: `FallbackGlyph.Stars` — FillRect 로 +자 별 3개(큰 별 위 중앙 + 작은 별 2개).
   Sleep 의 DrawZ 와 같은 픽셀 기법. Stars<Length 자동 캐시 확장(unit 5 LOW1).
3. **reconcile 훅**: 기존 Sleep CcEffect 루프를 **한 패스로 Sleep·Stun 동시 판정**하도록 확장
   (`remainingTime>0` 인 kind 수집 → 각 활성 kind 마다 Ensure). 앵커·쿼리 재사용, 두 상태
   공존 가능(스포너 키 = (entity, kind)). CcEffect 는 Effects 소유지만 **읽기만**.
4. **registry**: kind 6 — prefab 없음(폴백), offset (0, 1.65, 0)(Sleep +0.35x·Marked −0.35x
   와 안 겹치게 중앙 상단), scale 0.65, billboard 1, tint 골드 (1, 0.85, 0.2), glyph=Stars.
5. 만료/해제 시 buffer 에서 Stun 사라지면 reconcile EndFrame 자동 회수(상태 구동, 해제 코드 불요).

## 완료 기준

- [x] compile 클린, 기존 Sleep(Zz)/Aggro 외형·동작 무손실(glyph 하위호환).
- [x] (Play) 스턴탄 맞은 적 머리 위 별 표시 · 스턴 만료 시 자동 회수 · 수면(Zz)과 시각 구분.

사용자 확인 2026-07-22 (Play "잘나옴") · 커밋 `2c66c462`.
