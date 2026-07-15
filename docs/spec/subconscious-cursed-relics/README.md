# 무의식 저주 유물 2종

> 상태: 구현 완료 2026-07-15
> 선행: `gift-phase`, `dreamcatcher-awakening-hand`, `dreamcatcher-content-1`, `dreamcatcher-heavy-strike`

## 목표

림의 선물용 플레이스홀더 2장을, 기존 드림캐쳐 기능을 조합한 하이리스크·하이리턴 유물로 교체한다.

- **재앙의 심장** (`Unit/Subconscious`): 6초 동안 공격속도 +100%, 3번째 공격마다 피해 ×2. 이후 호스트가 사망하며 주변 2타일에 400 피해.
- **금이 간 성배** (`Squad/Subconscious`): 호스트 생존 중 전 아군 공격력 +70%, 유효체력 -40%.
- 기존 **느린 각성**을 유지해 무의식 풀은 총 3장으로 구성하고, 림은 서로 다른 2장을 뽑는다.

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_lethal_attach_preflight.md` | 코드·테스트 | 기존 시한부 효과를 덮어쓰는 중복 부착 방지 |
| 1 | `1_calamity_heart.md` | 데이터·PlayMode | 재앙의 심장 조합과 사망 연쇄 검증 |
| 2 | `2_cracked_grail.md` | 데이터·테스트 | 금이 간 성배 적용·철회 검증 |
| 3 | `3_gift_integration_and_validation.md` | 통합 | 카탈로그·림 풀·표시 경로 검증 |
| 4 | `4_card_art.md` | 아트 | 신규 2장과 기존 placeholder 3장 실아트 교체 |
| 5 | `5_handoff_summary.md` | 인계 | 구현·검증 결과 정리 |

## 공통 계약

1. 신규 `DcTriggerKind`, `DcPayloadKind`, `CardBuffKind`, ECS 타입·시스템·이벤트 채널을 추가하지 않는다.
2. 재앙의 심장은 `SelfBuffLethal + AttackN×HeavyStrike + OnDeath×SelfTileAoe`, 성배는 기존 hosted `AttackDamage + EffectiveHealth` 효과만 사용한다.
3. SO→ECS 변환과 사전검증은 기존 `BattleBridge.Dreamcatcher.cs` 안에서 처리한다.
4. 전용 인터페이스·validator·manager·UI를 만들지 않는다.
5. 기존 `LethalTimer` 때문에 부착이 실패하면 modifier·trigger·비용·순환 상태가 바뀌지 않는다.
6. 모든 수치는 카드 ScriptableObject가 소유하며 코드에 하드코딩하지 않는다.
7. 기존 플레이스홀더 에셋을 GUID 보존 rename·재저작하고 카탈로그 배열은 재구성하지 않는다.
8. Unit/Squad 카드의 기존 부착·호스트 사망·회수 수명주기를 유지한다.

## 파이프라인 커버리지

신규 플레이 오브젝트나 렌더 경로가 없으므로 `object-pipeline-map` 대조는 N/A다. 폭발, 강공, hosted stat modifier, Gift/Hand/Inspect 경로를 그대로 재사용한다.

## 비목표

- 저주 전용 enum·태그·프레임·VFX·SFX
- 기존 느린 각성 재설계
- 저주 전용 비용·등급·드랍률·보유 시스템
- 범용 다중-mechanic validator 프레임워크
