# 7 — 인계 요약 (dreamcatcher-content-5)

## Commit

| 해시 | 내용 |
|---|---|
| `8995140e` | **동반 커밋** — 타 작업(on-place-skill-rework units 9~11)과 한 커밋에 묶였다. 아래 ⚠ 참조 |
| `d63e9cf5` | 누락된 잿불 장판 에셋 + 셀 바인딩 뒷문 차단 |
| `13d54060` | bake/드레인 회귀 핀 픽스처 보강 |

> ⚠ **분리 커밋이 불가능했다.** 두 작업이 같은 파일 5개(`MovementKind`·`MovementBinding`·
> `ProjectileData`·`ProjectileMoveSystem`·`ProjectileHitSystem`)에 얽혔고, 이동 arm 이 아직
> untracked 였던 `Boomerang.cs` 를 호출해서 한쪽만 커밋하면 **컴파일되지 않는 트리**가 남았다.
> `git log -- <content-5 파일>` 로 이 spec 의 이력을 좇으면 첫 커밋의 제목이 타 작업이다.
>
> ⚠ 그 커밋에서 **`Hazard_Ember.asset` 만 빠졌다**(untracked). 카드는 들어가고 그 카드가
> 가리키는 장판이 없어 저장소 상태에서 잿불이 죽어 있었다 — `d63e9cf5` 가 메꿨다.
> **에셋 신설 spec 은 커밋 후 `git ls-files` 로 참조 대상이 전부 추적되는지 확인할 것.**

## Implemented

- **부메랑** — N타마다 발사 축을 따라 4타일 나갔다 **돌아온다**. 스치는 적을 진행 방향으로
  민다 → 나갈 때 밀리고 **돌아올 때 딸려온다**. 가까운 적(경계 2.8타일 안)은 왕복에 두 번.
- **잿불** — 적을 처치하면 그 자리에 불씨 장판(1초마다 12 · 4초). 통행 층은 처치한 유닛 사양.
- **불나방떼** — 3초마다 주변 4타일 안 적들에게 10마리가 0.12초 간격으로 산개 발사(마리당 7.2).
- 엔진 축 3개: `MovementKind.BoomerangReturn`(왕복 궤적) · **관통탄 넉백**(PathHit 공통, 0=꺼짐) ·
  `DcPayloadKind.SpawnHazard`(카드가 장판을 깐다).
- **카드에 발사 명세 개방** — 여태 loud 거절이던 `EmitProjectilePattern` 카드 경로를 열었다.
- **카드의 추가 투사체가 탄 에셋의 궤적을 존중한다** — `SpawnNeedleCarrier` 가 (Homing,
  SingleSplash)를 하드코딩하고 있어 저작이 무시되던 결함을 고쳤다.
- **궤도(팽이)가 주인과 함께 사라진다** — content-4 계약 2 의 뒷문장을 뒤집었다(아래 주의 4).

## Key Files

- `Battle/Combat/Projectile/Boomerang.cs` — 왕복 수학(`Position`/`TotalTime`/`IsComplete`)
- `Battle/Combat/Projectile/ProjectileMoveSystem.cs` — 왕복 arm + 궤도 arm 의 주인 생존 판정
- `Battle/Combat/Projectile/ProjectileHitSystem.cs` — PathHit 넉백(스윕 방향 × 속도)
- `Battle/Combat/AttackSystem.cs` — `SpawnNeedleCarrier` 궤적 축 존중 + 방향 바인딩 배선
- `Battle/Units/DamageApplicationSystem.cs` · `EnemyKilledEvent` — 잿불 스탬프(장판 index + 통행 층)
- `Bridge/BattleBridge.cs` — 왕복 드레인 분기 · 넉백 SO→state · 킬 드레인 장판 스폰 ·
  `TryBuildPatternSlot`(적/카드 공용)
- `Bridge/BattleBridge.Dreamcatcher.cs` — bake 전량(궤적 축 · SpawnHazard · 패턴 카드 개통)
- 에셋: `Card_{Boomerang,EmberField,MothSwarm}` · `Projectile_{Boomerang,Moth}` ·
  `Pattern_MothSwarm` · `Hazard_Ember`

## Verified

- EditMode **2485 중 2482 통과 · 0 실패 · 3 스킵**(스킵은 전부 기존 문서화된 무시 항목).
- 신규: 왕복 궤적 12건 · 왕복×넉백 e2e 5건(두 다리의 힘이 반대인지 포함) · 궤도 소멸 2건 ·
  패턴 카드 부착 3건(2장 동시 부착 · 비주기 트리거 거절 포함) ·
  **bake/드레인 회귀 핀 9건**(`BoomerangBakeAndDrainTests` — 드레인이 발사점/직전위치/축/거리를
  실제로 채우는지, 퇴화 저작 3종 거절, 비수 궤적 무회귀, 셀 바인딩·잿불 SO/트리거 거절).
- 사용자 Play 확인 — 부메랑 왕복·넉백 체감(2026-08-17), VFX(수리검·평면 회전).
- ⚠ **PlayMode 는 이 작업과 무관하게 광범위히 빨갛다**(씬 부트스트랩이 로비로 감 · 트윈 콜백
  에러 · 드래그 UI · 배치 오라 수치 · 적이 5타일 앞에서 정지). 같은 브랜치의 타 작업 커밋과
  함께 나타났고 이 spec 이 닿는 코드가 아니다. **기준선을 잡아 확인하지는 않았다.**

## Notes — 되돌리면 안 되는 것

1. **`ProjectileState.direction` 은 왕복에서 «발사 축이고 불변» 이다.** 궤적 함수의 **입력**이라
   「돌아오는 중이니 뒤집자」로 갱신하면 다음 프레임이 `origin − axis*(…)` 를 내고 **발사점 뒤로
   날아간다**(초판 설계의 실제 결함). 궤도 arm 이 바로 위에서 매 프레임 접선을 쓰지만 그건
   **파생값**이라 되먹임이 없다 — 그 arm 을 복제하지 말 것.
2. **「지금 어느 다리인가」를 어디에도 저장하지 않는다.** 넉백 방향은 그 프레임 스윕
   (`pos − prevPos`), 화면 facing 은 뷰의 직전 위치 차이. 「밀었다 당김」은 그 결과다.
3. **넉백은 피해가 실제로 들어간 순간에만 나간다.** 재타격 쿨타임에 막힌 프레임에도 쏘면
   스치는 내내 매 프레임 밀려 적이 날아간다.
4. **궤도는 주인이 사라지면 즉시 소멸한다** — content-4 계약 2 의 뒷문장(«자기 수명을 산다»)을
   뒤집은 것이다(2026-08-17 사용자 결정). 화면에서 주인 없는 자리에서 혼자 도는 구슬이었다.
   **궤도에만** 건다 — 직선·왕복·호밍은 던지면 제 갈 길을 가는 것이 사양이다.
5. **`tileRange` 는 부메랑 축에서 «날아가는 거리» 다**(다른 축에서는 재조준 반경). 겸직이 아니라
   대체다 — 방향 바인딩에는 겨눌 대상 엔티티가 없어 재조준이 성립하지 않는다. `retargetTileRange`
   를 0 으로 명시하는 줄을 지우지 말 것(지금은 사전 스캔이 호밍으로 좁혀져 무해하지만 **우연한**
   무해다).
6. **잿불의 통행 층은 «킬 시점에» 굽는다.** 브리지에서 killer 를 읽으면 동귀어진일 때 이미
   파괴돼 0 으로 새고, 0 은 **무제한 통과**라 지상 전용 유닛의 불씨가 비행 적을 태운다.
7. **패턴 카드는 «직선탄 host 인데 자기 패턴이 없는» 조합을 loud 거절한다.** 그 host 의 기본
   공격이 0번 패턴 슬롯을 읽기 때문에, 통과시키면 **카드가 그 유닛의 기본 공격을 바꿔친다.**
8. **카드 값 일부는 시트가 소유한다.** `trigger.period`·`magnitude`·`tileRange`·`duration`·
   `periodSeconds` 는 `DcMechanics` 탭이 이기고 **로비 진입마다 덮어쓴다**. SO/파일만 고치면
   되돌아간다(실제로 겪었다). 탄·패턴·해저드 SO 는 시트 밖이라 안전하다.

## Follow-up

- **PlayMode 기준선** — 위 실패 13건이 이 작업 것인지 가리려면 변경을 뺀 상태의 같은 실행이 필요.
- **부메랑 실아트** — 지금은 통합된 수리검 프리팹 재사용(guid 유지 교체 관례).
- **발사 명세의 방향 바인딩 판정이 `Directional` 하드코딩** — 패턴에 부메랑 탄을 물리면 조용히
  무효 판정. 착수 조건은 README 후속 후보 참조.
- 3장 실아트 · 밸런스 재점검(불나방떼 초당 24 는 팽이·부메랑과 같이 저울에 올릴 것).
