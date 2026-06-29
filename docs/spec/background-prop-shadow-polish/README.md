# Background Prop Shadow Polish

> 상태: 완료 2026-06-29 (units 0~4, 사용자 육안 통과). 커밋 ee10b86(0~2) · 4704d4f(3~4)
> 전제: `tilted-billboard` unit 3 (blob shadow), `tilemap-world-surround` unit 4 (원경 링 프랍), `tilemap-real-shadows`.
> 대상: `Assets/_Project/Scenes/BattleScene.unity` (Tilemap, URP). Legacy3D 불변.

## 목표 / 검증 질문

> **근경 프랍 그림자가 더 부드럽고(블롭 소프트닝), 원경(외곽 링) 프랍도 접지 그림자를 가지며, 원경 프랍 밀도가 자연스럽게 낮아졌는가?**

배경 프랍의 접지감을 다듬는 3종 시각 폴리시다. 새 시스템 없음 — 기존 블롭/링 파라미터 튜닝 + 원경 블롭 부착 1줄.

## feature-wide 계약

- **프랍 그림자 = 블롭 통일 유지.** 실시간 cast 는 프랍에서 미사용(기존 사용자 결정, 불변). 캐릭터 real-shadow 경로 불변.
- **원경 프랍도 동일 블롭 사용.** unit 1 에서 `tilemap-world-surround/4_distant_props.md` 의 "원경 그림자 OFF" 계약을 **ON 으로 갱신**한다. 별도 거리 dimming 없이 동일 `AttachPropBlob` 경로 재사용.
- **모든 수치는 데이터에서.** 블롭(alpha/size)은 `BattleBridge` serialized → static 미러, 프랍별 크기는 `PropData.visualScale`, 밀도는 `forest.asset`. 하드코딩 금지.
- **블롭 크기 모델 (unit 3)**: `블롭 월드 지름 = BlobShadowSize(1타일 기준 전역, 1.0) × prop.visualScale(프랍별)`, **원형**. footprint 타원 제거 — 바닥 평면 + 퍼스펙티브가 화면상 타원을 만든다. 부모 lossyScale 나눗셈은 유지(틸트·jitter·placement 스케일 상쇄, load-bearing).
- **정적 스폰 (unit 4)**: 프랍 블롭은 스폰 시 transform 1회 세팅(`live: false`), 매 프레임 강제 없음. 유닛 블롭만 라이브(`live: true`, 이동 따라가기). **방향 offset 없음 — 발밑 정중앙**(좌우/깊이/페이즈 무관 일관). 방향성 그림자는 후속 후보.
- **변경마다 Play→게임뷰 스크린샷 육안 검증 후 확정.** 수치는 시각 판단으로 수렴(아래 시작값은 출발점일 뿐).

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_blob_softening.md` | 블롭 alpha/footprint 튜닝 | 근경 프랍 그림자를 더 부드럽게 |
| 1 | `1_distant_prop_blob.md` | `InstantiateRingProps` 블롭 부착 | 원경 프랍도 접지 그림자 (계약 OFF→ON) |
| 2 | `2_ring_density_reduce.md` | `ringPropDensity` 하향 | 원경 프랍 밀도 낮추기 |
| 3 | `3_blob_size_simplify.md` | 블롭 크기 모델 단순화 | footprint 제거 → 원형, `BlobShadowSize×visualScale` 1타일 기준 |
| 4 | `4_blob_static_spawn.md` | 정적 스폰 (매 프레임 강제 제거) | 프랍 블롭 스폰 1회 세팅, offset 없음(발밑), 유닛만 라이브 |

## 후속 후보

- 원경 블롭 거리 기반 dimming (멀수록 옅게) — 균일 블롭이 부자연스러우면.
- 소프트 블롭 sprite 재오서링 (alpha 튜닝으로 부족하면 sprite 자체의 falloff 를 더 부드럽게).
- **방향성 그림자("해 전방")** — offset 살리려면 화면 균일 + 카메라 보정이 필요해 매 프레임 재계산 불가피(정적 스폰과 양립 불가). 단순성 위해 unit 4 에서 보류. 정말 필요하면 별도 spec 으로 카메라-이벤트 기반 재계산 설계.
