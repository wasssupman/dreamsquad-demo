# 3 — 몸 크기 축 (`bodyRadius`) 신설

## 목적

전투원에게 **몸**을 준다. 오늘 판정은 전부 **중심점 하나 대 중심점 하나**라, 스프라이트가
일반 유닛의 **1.89배**인 보스가 몸통을 눈으로 관통당해도 무판정이다. unit 4 가 자를 조이면
그 어긋남이 「겹쳐 있는데 안 때린다」로 즉시 드러난다.

**전환 앞에 둔다.** 순서를 뒤집으면 보스전이 두 번 깨진다 — 사거리가 좁아져서 한 번,
몸이 커서 또 한 번.

## 변경 대상

- `Scripts/Data/AttackUnitData.cs` — `bodyRadius` 필드(기본 **0** = 무회귀)
- `Scripts/Battle/Units/` — `HitRadius` 컴포넌트 1개.
  **소속 맥락 = Units** — 몸 크기는 `Health`·`FactionTag` 과 같은 성격의 「그 유닛이 무엇인가」다.
  쓰기는 스폰(Bridge) 1회, 읽기는 전부 Combat(사거리·투사체 충돌). 어디 둬도 동작은 같으나
  M1 이식 시 **어느 모듈로 가나**가 실질 질문이라 Units 로 못박는다 — sim lib 에서는 **엔티티 정의 모듈**(`Health`·`FactionTag`·`AttackState` 와 같은 묶음)로 가고, `IComponentData` 를 벗기면 `float bodyRadius` 필드 하나로 끝난다.
- `Scripts/Bridge/BattleBridge.cs` — 적 스폰 bake 1줄
- 충돌 2곳 — `Combat/Projectile/SweepHitMath.cs` 소비처, `ProjectileMoveSystem` 도달 판정

## 구현

- **유효 반경 = `hitThreshold + target.bodyRadius`.** 기본 0 이면 오늘과 byte-identical.
⚠ **이름은 `bodyRadius` 다.** `SweepHitMath.cs:11` 의 파라미터명이 이미 `hitRadius` 이고 실인자는
`ProjectileData.hitThreshold`(투사체 피격 반경)라 뜻이 다르다 — 같은 unit 이 그 둘을 **더하는** 코드를 쓴다.

⚠ **이 unit 은 저작하지 않는다.** 보스 `bodyRadius` 저작과 HP/방어 재조정은 **unit 6** 이 한다 —
여기서 하면 `long_boss.trace.txt` 가 움직여 계약 13(units 0~3 골든 초록)이 그 자리에서 깨진다.
- `Projectile_MachineGunBullet.hitThreshold` 가 임시 완화로 0.4 → 0.7 올라가 있다.
  이 unit 이 근본을 고치므로 **0.4 복귀를 검토**한다(전 적 대상 균일 확대였던 것을 되돌린다).

**⚠ 이 축의 최대 항목은 필드가 아니라 밸런스다** (계약 5) — 그래서 unit 6 이 진다. `bodyRadius 0.9` 를 주는 순간
사거리 1 유닛(전체 41%)의 대보스 허용 거리가 **1.5 → 2.4(+60%)**, 허용 면적은 **2.56배**다.
동시에 보스를 때릴 수 있는 유닛 수가 크게 는다 — unit 4 의 −9.5% 면적 손실과 자릿수가 다르다.

방향은 **의도한 물성**(큰 몸 = 큰 표적)이므로 되돌리지 않는다. 대신 **보스 HP/방어 재조정을
이 unit 안에서** 끝낸다. 다음 unit 으로 미루면 전환 직후 보스가 녹는다.

## 시트

`bodyRadius` 는 **시트에 컬럼이 없는 신규 필드**라 임포터가 스킵한다(고아 컬럼 `aggroRange` 와 대칭).
**SO 저작으로 끝난다.** ⚠ 반대로 unit 6 이 만지는 **보스 HP 는 시트가 정본**이다(`UnitStatImportDto.health`) —
`.asset` 만 고치면 다음 로그인 임포트가 되돌린다.

## 완료 기준

- [ ] 골든 7건의 **이벤트 본문 무변화**. ⚠ **byte-identical 은 불가능하다** —
      `MatchConfigSnapshot.Describe` 가 SO 를 리플렉션으로 통째 접으므로 **필드를 넣는 것만으로
      `configHash` 가 바뀐다**(값 0이어도). 그리고 `DiffAgainst` 는 해시 불일치를 **가장 먼저**
      보고하도록 설계돼 있다.
- [ ] **전/후 `configHash` 쌍을 이 문서에 기록**한다. 이 변경은 판독기의 기존 두 범주
      (시트 드리프트 / 코드 회귀) 밖의 **세 번째(스키마 확장)**이고 겉모습이 드리프트와 같다 —
      해시 쌍을 남기는 것이 유일한 방어다.
- [ ] **골든 7건 초록**(계약 13) — 이 unit 은 저작하지 않으므로 성립해야 한다.
- [ ] 배관 확인: `bodyRadius` 를 임시로 0 초과로 넣으면 충돌 2곳이 실제로 반응한다(되돌린다).
