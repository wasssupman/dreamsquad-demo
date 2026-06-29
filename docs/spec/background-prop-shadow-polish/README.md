# Background Prop Shadow Polish

> 상태: 완료 2026-06-29 (units 0~2, 사용자 육안 통과)
> 전제: `tilted-billboard` unit 3 (blob shadow), `tilemap-world-surround` unit 4 (원경 링 프랍), `tilemap-real-shadows`.
> 대상: `Assets/_Project/Scenes/BattleScene.unity` (Tilemap, URP). Legacy3D 불변.

## 목표 / 검증 질문

> **근경 프랍 그림자가 더 부드럽고(블롭 소프트닝), 원경(외곽 링) 프랍도 접지 그림자를 가지며, 원경 프랍 밀도가 자연스럽게 낮아졌는가?**

배경 프랍의 접지감을 다듬는 3종 시각 폴리시다. 새 시스템 없음 — 기존 블롭/링 파라미터 튜닝 + 원경 블롭 부착 1줄.

## feature-wide 계약

- **프랍 그림자 = 블롭 통일 유지.** 실시간 cast 는 프랍에서 미사용(기존 사용자 결정, 불변). 캐릭터 real-shadow 경로 불변.
- **원경 프랍도 동일 블롭 사용.** unit 1 에서 `tilemap-world-surround/4_distant_props.md` 의 "원경 그림자 OFF" 계약을 **ON 으로 갱신**한다. 별도 거리 dimming 없이 동일 `AttachPropBlob` 경로 재사용.
- **모든 수치는 데이터에서.** 블롭(alpha/footprint/size)은 `BattleBridge` serialized → static 미러, 밀도는 `forest.asset`. 하드코딩 금지.
- **변경마다 Play→게임뷰 스크린샷 육안 검증 후 확정.** 수치는 시각 판단으로 수렴(아래 시작값은 출발점일 뿐).

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_blob_softening.md` | 블롭 alpha/footprint 튜닝 | 근경 프랍 그림자를 더 부드럽게 |
| 1 | `1_distant_prop_blob.md` | `InstantiateRingProps` 블롭 부착 | 원경 프랍도 접지 그림자 (계약 OFF→ON) |
| 2 | `2_ring_density_reduce.md` | `ringPropDensity` 하향 | 원경 프랍 밀도 낮추기 |

## 후속 후보

- 원경 블롭 거리 기반 dimming (멀수록 옅게) — 균일 블롭이 부자연스러우면.
- 소프트 블롭 sprite 재오서링 (alpha/footprint 튜닝으로 부족하면 sprite 자체의 falloff 를 더 부드럽게).
