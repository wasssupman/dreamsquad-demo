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

- [x] 골든 **8건**(`summoner` 추가됨)의 **이벤트 본문 무변화**. ⚠ **byte-identical 은 불가능하다** —
      `MatchConfigSnapshot.Describe` 가 SO 를 리플렉션으로 통째 접으므로 **필드를 넣는 것만으로
      `configHash` 가 바뀐다**(값 0이어도). 그리고 `DiffAgainst` 는 해시 불일치를 **가장 먼저**
      보고하도록 설계돼 있다.
- [x] **전/후 `configHash` 쌍을 이 문서에 기록**한다. 이 변경은 판독기의 기존 두 범주
      (시트 드리프트 / 코드 회귀) 밖의 **세 번째(스키마 확장)**이고 겉모습이 드리프트와 같다 —
      해시 쌍을 남기는 것이 유일한 방어다.
- [x] **골든 8건 초록**(계약 13) — 이 unit 은 저작하지 않으므로 성립해야 한다.
- [x] 배관 확인: `bodyRadius` 를 임시로 0 초과로 넣으면 충돌 2곳이 실제로 반응한다(되돌린다).

---

### 진행 기록 — 완료 2026-08-31

**저작 0.** 필드·컴포넌트·배관만 넣었다. 값은 전부 0 이라 오늘과 동작이 같다.

| 무엇 | 어디 |
|---|---|
| 저작 필드 | `AttackUnitData.bodyRadius` (`[Min(0f)]`, 기본 0) |
| 런타임 | `Battle/Units/HitRadius.cs` — 맥락 Units |
| bake | 적 스폰 1줄. **조건부로 붙이지 않는다** — `HasComponent` 로 갈리면 「어떤 적은 몸이 있고 어떤 적은 없다」가 되어 판정이 데이터에 따라 두 갈래가 된다 |
| 소비처 1 | `ProjectileMoveSystem` 도달 판정 2곳(Homing · BezierHoming) — `hitThreshold + bodyRadius` |
| 소비처 2 | `ProjectileHitSystem` sweep(PathHit) — 피해자별 반경 배열 |

**사거리 술어(`AttackReach`)는 아직 안 건드렸다** — 그건 unit 4a 소관이다. 이 unit 이 여는 것은
**충돌** 두 곳뿐이다.

**완료 기준 1·2 — 이벤트 본문 무변화가 「정확히 한 줄」로 증명됐다.**
재생성 후 `git diff --numstat` 이 8파일 전부 `1 1` 이고, 그 한 줄이 `configHash` 다.
「byte-identical 은 불가능하다」는 예측이 맞았고, 그 불가능한 부분이 **딱 그 한 줄**임이 나왔다.

| 시나리오 | 전 | 후 |
|---|---|---|
| `basic` · `long_boss` · `no_defense` · `force_wave` | `7d57582fda28dcdb` | `2a8cdc9e9597a838` |
| `restart` | `2eb3fcb6fac746ed` | `14bebc8308a684e3` |
| `seed_b` | `9cbab01f0e6eb031` | `bfb0dcc7c4c4e053` |
| `seed_c` | `95a8b9134839201c` | `3c24678167592493` |
| `summoner` | `64b200d54e5910a9` | `8ad13764781230af` |

**완료 기준 4 — 배관은 살아 있다. 다만 한 번 거짓 실패가 났고 그게 unit 6 에 직결된다.**

`Enemy_Basic` 하나에만 `bodyRadius: 0.9` 를 주고 재생성했더니 **이벤트가 하나도 안 움직였다**
(configHash 만 바뀜). 「배관이 죽었다」로 읽힐 뻔했지만 아니었다 — **전 적 24종**에 주니 크게
움직인다: `long_boss` 4001/6211줄 · 킬 **23→25**, `summoner` 91/89, `seed_b`·`seed_c` 67/68,
`basic` 20/18, `no_defense` 는 1/1(방어유닛이 없어 투사체가 없으니 당연).

⚠ **여기서 나온 규칙: 적 한 종의 저작 변경은 골든에서 보이지 않을 수 있다.** 그 적이 코퍼스에서
투사체 도달 판정을 좌우하는 순간을 못 만나면 그렇다. **unit 6 이 보스 `bodyRadius` 를 저작할 때
「골든이 안 움직였다」를 무회귀의 근거로 삼으면 안 된다** — 보스가 실제로 도는 `long_boss` 를
직접 봐야 하고, 거기서도 「안 움직임」은 「저작이 안 먹었다」와 구분되지 않는다.

`Projectile_MachineGunBullet.hitThreshold` 0.7 → 0.4 복귀 검토는 **unit 6 으로 넘긴다** — 저작 값
변경이라 이 unit 의 「저작 0」 원칙에 어긋나고, 되돌리는 순간 골든이 움직인다.
