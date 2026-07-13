# dreamcatcher-hand-deal-in — 각성 손패 카드감 재설계 (StS/HS 손패)

**상태: 재설계 승인, 구현 중 (2026-07-13)**

## 목표

플레이 중 각성 버튼으로 손패가 열릴 때, 카드가 **손에 쥔 카드 부채**처럼 읽히게 한다.
초기 "버튼→평면 UI 행 딜링"이 UI 타일처럼 느껴진 원인은 **도착지 레이아웃**(평평·벌어짐·정적)과
**직선 lerp 궤적**이었다. Slay the Spire / Hearthstone 손패 레퍼런스를 기준으로 구조를 바꾼다.

## 레퍼런스 모델 (StS / HS 손패)

정규화 위치 `t ∈ [-1,+1]`(왼→오) 기준:

- **포물선 아치**: `y = arcHeight · (1 − t²)`(가운데가 가장 솟음), 접선 회전 `rotZ = −t · rotMax`.
- **겹침**: 카드 스텝 < 카드폭(음수 간격) → 서로 겹쳐 "쥔 패".
- **풀 호버**: 커서 위 카드 = 들어올림 + 확대(≈1.2~1.4) + 회전 펴짐(→0) + 최상단 + 양옆 밀어냄(scatter).
- **스프링 추종**: 매 프레임 목표로 lerp(스냅 아님) → 무게감.
- **드로우**: 덱(하단)에서 곡선으로 솟아 확대·오버슛으로 아치에 안착.

## 구현 문서 목록

| # | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 아치 부채 + 스프링 | `0_arc_fan_layout.md` | 평면 행 → 포물선 아치+겹침+접선회전, 슬롯 목표+매프레임 스프링, z-order. |
| 1 | 풀 StS 호버 | `1_hover_interaction.md` | 들어올림/확대/펴짐/최상단/양옆 scatter. 드래그/타겟팅 공존. |
| 2 | 덱-드로우 딜 | `2_deck_draw_deal.md` | 하단에서 곡선 상승 → 아치 안착(오버슛+틸트+squash flex). 각성 버튼 pulse. |
| 3 | 퇴장 침강 | `3_close_sink.md` | 카드가 하단 덱으로 역스태거 침강 → strip 폴드 인. |

(구현 종료 시 `4_handoff_summary.md` 추가.)

## feature-wide 계약

- **손패 = 아치 부채**. `EnsureSlots` 의 평면 행 기하를 포물선 아치(y=arcHeight·(1−t²))+음수 스텝(겹침)+
  접선 회전으로 교체. 슬롯 배치식은 이 spec 이 소유(하드코딩 아닌 SerializeField 노브).
- **스프링 추종 모델**. 슬롯은 정적 위치가 아니라 `targetPos/targetRotZ/targetScale` 를 갖고 `Update` 에서
  실시간(`Time.deltaTime`, timeScale=1 고정)으로 lerp. **드래그/포탈-조준 중인 슬롯은 스프링 skip**(DragSlot 이
  transform 소유). 전이(딜/수렴/flip) 중에도 스프링 skip(트윈이 소유).
- **호버는 targets 를 통해서만**. 호버가 rect 를 직접 쓰지 않고 슬롯 target 을 바꾸면 스프링이 해석 → 드래그·딜과
  한 모델에서 일관. 호버 대상은 최상단 sibling, 양옆은 scatter falloff.
- **딜 소스 = 하단 덱**(버튼 아님). 카드가 트레이 하단 바깥에서 솟아오른다. 각성 버튼은 pulse 발광만(인과 힌트).
  (이전 "버튼 정확 좌표 딜" 결정은 재설계로 폐기 — 사용자 2026-07-13.)
- **PrimeTween Sequence teardown stop**. 딜/수렴 시퀀스는 필드 보유, `ForceClose`/phase 이탈/`OnDisable` 에서 Stop.
- **전이·드래그 가드**: `Transitioning` 이 딜/수렴 진행 포함. 딜/수렴 중 드래그·토글 금지.
- **슬로모 lease·페이즈 가드·drag 서비스 계약 불변**. `Open/Close/ForceClose` 부수효과 유지, 연출만 교체.
- **순수 프레젠테이션. ECS 변경 0, 채널 변경 0.** 덱 사이클·게이지 경제·카드 데이터/아트 불변.

## 파이프라인 커버리지

N/A — 플레이 오브젝트(유닛/적/투사체/해저드/VFX) 신설·생성→렌더 경로 변경 없음. 대상은 런타임 빌드 UGUI
카드 위젯(`DreamcatcherHandView._slots`)의 레이아웃/상호작용/등장·퇴장 트위닝뿐. `object-pipeline-map` 무관.

## 비목표 / 후속 후보

- **진짜 버텍스 커브(②-A) + 꼬깃꼬깃 펴짐(③)** [M] · 둘 다 서브디바이드 메시(+ ③ 는 언폴드 셰이더)가
  토대라 함께 별도 spec. 이 spec 의 안착 flex 는 4버텍스 squash-stretch(②-B)로 마감. 모바일 성능 검증 동반.
- **카드 딜/호버/안착 SFX** [S] · 연출 확정 후 SoundManager 틱.
- **사용 카드 소비 강조** [S] · use → 소비 1장만 별도 연출 후 나머지 침강.
- **idle bob/sway** [S] · 손패 정지 시 미세 상시 흔들림(스프링 모델 위에 얹기 쉬움).

## 연결 문서 / 레퍼런스

- 형제 spec: `docs/spec/gift-phase/`(PrimeTween 스태거/셔플/fly 선례).
- StS/HS 손패 레퍼런스: card_fan_demo(Godot, 포물선 아치+scatter 파라미터), ycarowr/UiCard(Unity, 하스스톤식 정렬/호버).
- 대상 코드: `Assets/_Project/Scripts/UI/Dreamcatcher/{DreamcatcherHandView,DreamcatcherCardDragSlot,AwakeningGaugeView}.cs`.
