# 4 — Handoff Summary

> dreamcatcher-unit-trigger 구현 종료 인계. 최신 계약은 README/번호 문서, 구현 상세는 코드/커밋 우선.

## Commit

- `3ad10ea6` spec (설계 critic 반영)
- `f91b13ef` unit 0 — 정의 계층 (`DcMechanic`: DcTriggerSpec/DcPayloadSpec + 카드 `binding`/`mechanics`)
- `45fef46b` unit 1 — `DcTriggerSlot` 부착 경로 + `ProjectileRequestCarrier`
- `660c3a71` unit 2 — AttackSystem RESOLVE 카운트/발동 arm + `DcTrigger.Tick` 순수함수
- `c642e462` unit 3 — 콕콕 바늘 카드 에셋 + Play 검증

## Implemented

- 개별 유닛 바인딩 트리거형 드림캐쳐 부류: 축 매칭 스탯% 패시브(기존)와 별개.
- 2계층: 정의(`Wassup.Data`, ECS 무참조) / 해석(BattleBridge 베이크 + Combat 실행). 아키텍처 교체 시 번역자만 재작성.
- `AttackN(N)` 트리거 × `ProjectileToTarget(magnitude)` 페이로드 1조합 end-to-end.
- `DcTriggerSlot`(Combat buffer) = 효과 인스턴스별 독립 카운터(instanceId). 같은 카드 2장 = 슬롯 2개.
- 카운트 = AttackSystem RESOLVE 1회 = 1카운트(멀티 output 무관, 근접/원거리 공통). period 도달 시 `ecb.CreateEntity` 캐리어에 `ProjectileSpawnRequest`(Homing×SingleSplash, damage=flat magnitude) + `ProjectileRequestCarrier`.
- 캐리어는 기존 `DrainProjectileSpawnRequests` 가 스폰 후 `DestroyEntity`(기존 RemoveComponent 앞 분기). 신규 시스템/드레인/큐 0.
- 부착 API `BattleBridge.ApplyDreamcatcherCardToUnit` — binding=Unit·defender·ECS ready 가드, 비-defender/magnitude≤0/None 거절(LogWarning). 콕콕 바늘 카드 = Shard02_GA 비주얼(speed 26), period 5, magnitude 20.

## Key Files

- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — 정의 계층 (트리거형 + 개조형 공용 파일)
- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs` — `binding`/`mechanics`/`attackMods`
- `Assets/_Project/Scripts/Battle/Combat/DcTriggerSlot.cs` · `DcTrigger.cs`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileRequestCarrier.cs`
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — RESOLVE dc arm (파일 하단)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `ApplyDreamcatcherCardToUnit` / drain 캐리어 분기 / teardown
- `Assets/_Project/Data/Dreamcatcher/Card_PokeNeedle.asset`

## Verified

- 컴파일 클린. EditMode: `DcTriggerTests` 4/4 (5회째 발동+리셋 / 매회 / period0 불발 / 독립 카운터). 머지 후 전체 582 그린.
- 실전투 Play: `Archer Damage 20.0` 로그 실증(5회 주기), flat 20(시너지 변조 무관), 슬롯 2개 독립 카운터, 캐리어 누수 0, 콘솔 에러 0.
- ecs-reviewer(unit 2): CRITICAL/HIGH 0. 8앵글 code-review(unit 1): 3건 반영(비-defender 거절=계약 2 런타임 강제 / magnitude≤0 skip / 무로그 가드 LogWarning).

## Notes (되돌리면 안 되는 의도)

- **캐리어 SFX 미재생·outputs 스냅샷 없음 = 의도** (캐리어에 DefenderUnitTag/output 버퍼 없음). 계약 5·파이프라인 표에 명시.
- **flat magnitude — damageMul 미적용** (계약 7). dc 투사체는 시너지/버프 무관 고정값.
- **소유권 = Combat** (트리거 소스·페이로드 출구가 대부분 Combat). bridge `_em` 직접 부착 write 는 스폰타임 선례(ProjectileRef 등)와 동일 클래스.
- README "확장 비용 지도" — 새 트리거/페이로드는 enum+arm+훅, 세만틱이 다르면(상태형/조합/프리미티브 밖) 한 번 지불. 되돌리지 말 것.

## Follow-up

- **unit 3 handoff 시점 미착수 항목**: 개별유닛 바인딩·회수 UX + 레지스트리(카드↔유닛↔instanceId, 사망 시 회수). `DrainDefenderDeathEvents` 가 seam. 별도 spec.
- 추가 트리거(Kill/Damaged/NextWave) · SelfTileAoe/NextAttackModifier 페이로드 · 카드 고유 SFX/VFX · 설명 템플릿 렌더링 — README 후속 후보.
- 공격 개조형 (c) 부류(튕김) → `docs/spec/dreamcatcher-attack-mod-bounce/` (unit 0 완료, 1~4 대기).
