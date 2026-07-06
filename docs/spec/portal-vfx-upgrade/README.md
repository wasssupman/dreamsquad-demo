# portal-vfx-upgrade

> 상태: **완료 2026-07-06** (unit 0 — 물빔 연결선 제거, 원안 스월 유지)
> 검증 질문: **포탈 입구/출구 사이를 잇던 어색한 물빔이 사라지고, 양끝 스월 비주얼은 원안 그대로인가?**

## 결정 이력

- 최초 제안(룬 게이트 프랍 재활용 + 빔 그라데이션)은 **사용자 반려** — 실험 후 전체 롤백.
- 확정 스코프: **원안(PixPlays WaterAOE 스월) 유지, `PixPlays_WaterBeam` 연결선만 제거.** 선으로 잇는 표현 자체를 쓰지 않는다.

## 작업 단위

| # | 문서 | 목적 |
|---|---|---|
| 0 | `0_portal_prefab_rebuild.md` | Portal_SKELETON 에서 LinkBeam 의 WaterBeam 서브트리 제거 |

## 계약

1. `SpawnPortal` 코드 무변경 — `LinkBeam` 자식이 비어도 LineRenderer 루프·PixPlays 핸들러 모두 자연 no-op.
2. Entry/Exit 서브트리(PixPlays_EntryWaterAoe/ExitWaterAoe) 무접촉.
3. 프리팹 guid 불변 — 씬 슬롯 재배선/씬 저장 불필요.

## 후속 후보

- 포탈 전용 비주얼(물 스월 대체) — 사용자가 방향 정할 때 재개. 룬 게이트 컨셉은 반려됨(기록용: 실험 스크린샷 `Assets/Screenshots/portal_vfx_new.png`).
- 입구/출구 시각 구분(현재 동일 스월) [S].
