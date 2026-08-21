# unit 2 — 저작 가드와 배치 페이즈 위생

## 목적

unit 1 이 연 방향 발사에는 **조용히 죽는 저작**이 여럿 있다. 이 프로젝트의 관례대로
저작 시점에 loud 하게 말해 준다. 겸사겸사 배치 페이즈에 밀린 실드 VFX 도 정리한다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `TryBuildPatternSlot` 검증 3건 ·
  과도기 충돌 경고 확장 · `StartBattle` 의 실드 부여 VFX 큐 정리
- (선택) `Assets/_Project/Tests/EditMode/` — bake 거절/통과 핀

## 구현

**① 방향 바인딩 패턴 검증 3건**

| 조건 | 처리 | 이유 |
|---|---|---|
| `tileRange <= 0` | **skip** | `maxDistance 0` → 즉시 착탄 판정, 스윕 길이 0 이라 넉백 조건도 거짓. 완전 무동작 |
| `damage <= 0` | 경고 | 평타 패턴은 output 이 덮어 0 이 정상이라 복제하면 밟기 쉽다. 넉백만 쓰는 저작이 가능하므로 skip 안 함 |
| `randomizeShotsPerTrigger` on | 경고 | 랜덤화(`PatternShotRandomizer`)는 **평타 경로에만** 있다. 켜 두면 침묵 no-op 인데 저작자는 "매번 다르게 퍼진다"고 믿는다 |

**② 과도기 충돌 경고를 확장한다.** 지금 경고는 `onPlaceEffect != None` 만 본다. 그런데
`onPlacePush*` 는 **독립 필드군**이라 되살아나도 조용하다 — 산탄과 반대 방향 밀치기가 같이 나가고
경고 한 줄 없다(README 계약 11 을 지켜 주는 것이 현재 아무것도 없다).
`onPlacePushDistance > 0 && UnitSkillAbility != null` 을 같은 경고에 넣는다.

**③ 실드 부여 VFX 큐를 `StartBattle` 에서 쓸어낸다.** 배치 페이즈에도 sim 이 돌아 실드는 즉시
붙는데 드레인은 `_running` 아래라, 부여 VFX 가 **반드시** 전투 시작에 몰려 터진다. 같은 자리에서
투사체 캐리어를 쓸어내는 선례가 있다(`DestroyEntitiesByType<ProjectileRequestCarrier>`).
⚠ 고치는 것은 **연출이 뒤늦게 터지는 것**뿐이다 — 「배치 페이즈 배치는 스킬을 잃는다」는 사양
그대로다(부모 spec 후속 후보 「배치 페이즈 발동 정책」).

**④ bake 게이트 주석의 stale 의도를 고친다.** `GrantShield` 의 미배선 조합 거절 옆 주석은
「HealthThreshold=tileRange 0, PeriodicTimer=tileRange>0 **만** 배선」이라는 화이트리스트 의도를
선언한다. 실제 코드는 블랙리스트 2조합이라 배치 실드가 통과하지만, 다음 사람이 주석대로 조이면
**실드셔틀 배치 실드가 조용히 죽는다.** 주석과 경고 문구에 `OnPlace × tileRange>0` = 배치 실드
(배선됨)를 추가한다. **동작 코드는 0줄.**

## 완료 기준

- [ ] `tileRange 0` 저작 → skip 로그가 뜨고 슬롯이 안 생긴다
- [ ] `damage 0` / `randomize on` 저작 → 경고는 뜨되 발사는 된다
- [ ] 샷건맨에 push 를 되살리고 능력 SO 를 함께 두면 충돌 경고가 뜬다
- [ ] 배치 페이즈에 실드셔틀 배치 → 전투 시작 시 실드 VFX 가 **몰려 터지지 않는다**(실드 자체는 유지)
- [ ] EditMode 전체 초록

> ✅ 사용자 Play 확인 2026-08-19. 커밋 `4aafe374`·`368c72b1`·`7aa1288b`·`71446582`·`09ca7a2b`·`13e19e9e`·`7851ac18`·`c280ea62`·`a6e42d3a`·`85fe2c13`·`765970fa`.
