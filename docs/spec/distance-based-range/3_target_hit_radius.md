# 3 — 몸 크기 축 (`hitRadius`) + 보스 재조정

## 목적

전투원에게 **몸**을 준다. 오늘 판정은 전부 **중심점 하나 대 중심점 하나**라, 스프라이트가
일반 유닛의 **1.89배**인 보스가 몸통을 눈으로 관통당해도 무판정이다. unit 4 가 자를 조이면
그 어긋남이 「겹쳐 있는데 안 때린다」로 즉시 드러난다.

**전환 앞에 둔다.** 순서를 뒤집으면 보스전이 두 번 깨진다 — 사거리가 좁아져서 한 번,
몸이 커서 또 한 번.

## 변경 대상

- `Scripts/Data/AttackUnitData.cs` — `hitRadius` 필드(기본 **0** = 무회귀)
- `Scripts/Battle/Units/` — `HitRadius` 컴포넌트 1개
- `Scripts/Bridge/BattleBridge.cs` — 적 스폰 bake 1줄
- 충돌 2곳 — `Combat/Projectile/SweepHitMath.cs` 소비처, `ProjectileMoveSystem` 도달 판정
- `Assets/_Project/Data/` — 보스 3종 `.asset` + **보스 HP/방어**

## 구현

- **유효 반경 = `hitThreshold + target.hitRadius`.** 기본 0 이면 오늘과 byte-identical.
- 보스 저작: 스프라이트 배율(`spineVisualScale` 2.6 / 2.9 / 3.2)에 비례한 값. 일반 적은 0 유지.
- `Projectile_MachineGunBullet.hitThreshold` 가 임시 완화로 0.4 → 0.7 올라가 있다.
  이 unit 이 근본을 고치므로 **0.4 복귀를 검토**한다(전 적 대상 균일 확대였던 것을 되돌린다).

**⚠ 이 unit 의 최대 항목은 필드가 아니라 밸런스다** (계약 5). `hitRadius 0.9` 를 주는 순간
사거리 1 유닛(전체 41%)의 대보스 허용 거리가 **1.5 → 2.4(+60%)**, 허용 면적은 **2.56배**다.
동시에 보스를 때릴 수 있는 유닛 수가 크게 는다 — unit 4 의 −9.5% 면적 손실과 자릿수가 다르다.

방향은 **의도한 물성**(큰 몸 = 큰 표적)이므로 되돌리지 않는다. 대신 **보스 HP/방어 재조정을
이 unit 안에서** 끝낸다. 다음 unit 으로 미루면 전환 직후 보스가 녹는다.

## 완료 기준

- [ ] `hitRadius` 전부 0 인 상태에서 골든 7건 **byte-identical**.
- [ ] 보스 저작 후: 머신거너 직선 탄이 보스 몸통을 관통하면 **맞는다**(Play 육안 1회).
- [ ] 보스 3종 각각 3분 판 생존 시간이 재조정 전후로 **같은 대역**(고정 스텝 하네스 비교).
- [ ] 골든 재생성은 여기서 하지 않는다 — unit 6 이 한 번에 한다.
