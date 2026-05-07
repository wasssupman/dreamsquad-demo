# Unit Rarity & Draft Rules

**작성일**: 2026-05-06  
**상태**: 완료 2026-05-07  
**연결 문서**: `docs/spec/hazard-caster-defenders/` (Epic 유닛 정의)

## 목표

유닛에 등급(Rarity)을 부여하고, 드래프트 풀 구성을 "슬롯 타입별 고정 배분"으로 전환한다. 카드 UI에 등급·슬롯을 2-layer로 표시하고 등급별 VFX를 추가한다.

## 풀 구성 계약

| 슬롯 | 장수 | 구성 방식 |
|---|---|---|
| Basic | 3 | `basicDeck[]` 고정 (항상 포함) |
| Meta | 2 | `metaDeck[]` 고정 (로테이션, 외부 로직이 교체) |
| Ego | 1 | `egoUnit` 고정 (현재 Bruiser) |
| Collection | 4 | 나머지 catalog에서 seed 기반 랜덤 |
| **합계** | **10** | 기존 poolSize=10, discardCount=3 유지 |

## 등급 배정

| 등급 | 유닛 |
|---|---|
| Common | Scout, Guardian, Cannon, Ranger, Piercer, Marksman |
| Rare | Archer, Bastion, Healer, Sniper |
| Epic | FireCaster, IceCaster, PoisonCaster, BlockingCaster |
| Ego | Bruiser |

## 카드 2-Layer 시각

- **테두리** = 등급 색상 (영구 속성): Common=회색, Rare=파랑, Epic=주황, Ego=보라
- **상단 배너** = 슬롯 색상 (세션 속성): Basic=파랑, Meta=골드, Collection=초록, Ego=보라

## VFX 계층

| 등급 | 효과 |
|---|---|
| Common | 테두리 Tween.Color pulse (subtle, 3s) + foil overlay (intensity 0.08) |
| Rare | 테두리 Tween.Color pulse (medium, 2s) + foil overlay (intensity 0.22) |
| Epic | pulse + foil (0.48) + UI ember 8개 + Particle System (선택) |
| Ego | pulse + foil (0.72) + UI ember 15개 + Particle System + 배너 shimmer |

foil overlay: `Assets/_Project/Shaders/DraftCardFoil_UI.shader` (Wassup/UI/DraftCardFoil). 홀로그래픽 필름 효과 — 레인보우 쉰, 마이크로 회절, 에지 림. 카드 틸트에 반응.

## 구현 문서 목록

| # | 파일 | 목적 |
|---|---|---|
| 0 | `0_data_model.md` | DefenderRarity, DraftSlotType enum + DefenderUnitData.rarity 필드 |
| 1 | `1_draft_rule_engine.md` | DraftController 슬롯 필드 + DraftSession 풀 구성 변경 |
| 2 | `2_card_decoration.md` | DraftCardFanView 2-layer 시각 (테두리+배너) |
| 3 | `3_card_vfx.md` | DraftCardVfxDriver (PrimeTween + 파티클) |
| 4 | `4_so_assignment.md` | 15종 defender SO rarity 배정 + Inspector 배선 |

## 비목표

- 소유(unlock) 시스템 구현 — Collection 후보는 지금 전체 owned로 처리
- 메타 로테이션 자동화 — Inspector 배열 교체로 관리
- 등급별 드래프트 확률 가중치 변경
- 등급 UI (로비/컬렉션 화면 등)

## 후속 후보

- Collection 소유 목록 unlock 시스템 (별도 spec)
- 메타 로테이션 자동 교체 로직 (별도 spec)
- 등급 표시 로비/덱 뷰 UI
- Ego 전용 강화 draft 규칙 (예: Ego는 버릴 수 없음 옵션)
