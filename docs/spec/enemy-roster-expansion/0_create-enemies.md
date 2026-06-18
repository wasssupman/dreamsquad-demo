# Unit 0 — 신규 적 3종 SO 생성

## 목적

Vanguard / Sniper / Debuffer SO 를 거동 필드 조합으로 생성. 코드 변경 없음.

## 변경 대상

- (신규) `Assets/_Project/Data/Enemies/Enemy_Vanguard.asset`
- (신규) `Assets/_Project/Data/Enemies/Enemy_Sniper.asset`
- (신규) `Assets/_Project/Data/Enemies/Enemy_Debuffer.asset`

## 구현

`AttackUnitData` 인스턴스를 `CreateInstance` + `AssetDatabase.CreateAsset` 로 생성(YAML 손편집 회피). 필드는 README 표대로. 참조:
- 머티리얼: 기존 적 머티리얼 재사용 (Vanguard←Basic, Sniper←Rootcaster, Debuffer←Needler).
- projectile: Sniper←`Projectile_Enemy_RitualBolt`, Debuffer←`Projectile_Enemy_Needle`.
- enum 정수: attackMethod(None0/Melee1/Projectile2), targetMode(None0/Nearest1/Focus2), aimMode(Stop0/Move1), DefenderClass(None0/Ranger1/Guardian2), StatKind(DamageMul0/AttackSpeedMul1/DmgTakenMul2/RegenPerSec3/MoveSpeedMul4), CombineOp(Mult0/Add1/Override2), classMask Everything=-1.
- Debuffer outputs: [Damage 3] + [ApplyStat magnitude 0.6, duration 3, stat DamageMul, op Multiplicative].

## 완료 기준

- [x] 3 에셋 생성 + reflection: 거동 필드 표와 일치(머티리얼·투사체 연결).
- [x] Play: Vanguard focus==Guardian(가까운 Ranger 있어도), Sniper focus==Ranger(가까운 Guardian 있어도, range 8).
- [x] Debuffer: outputs=[Damage 3, ApplyStat DamageMul ×0.6/3s] 정확. ApplyStat 직접 주입 시 디펜더 damageMul 1.00→0.60 확인. 투사체 발사 확인.
- [x] 데이터만 — 코드 변경 없음, 컴파일/회귀 영향 없음.

> 주의: 투사체 **명중→outputs 적용**은 기존 ProjectileHitSystem 경로(검증된 인프라). 수동 틱 하네스에서 투사체 명중이 안 잡혀(Needler/Rootcaster 동일) Debuffer 디버프의 in-match 적용은 실시간 Play 에서 최종 확인 권장.

완료: 2026-06-18 / 커밋 해시 `0a05241`
