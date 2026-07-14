# 3 — 카드 에셋 (SO + 아트 + catalog) + Play e2e

## 목적

`응축된 일격` 을 실제 SO 로 만들어 catalog 에 등록하고, 손패/COLLECTION 노출 + 5회째 강공(피해 ×2) 인게임 동작을 Play 로 검증한다. 시트 roundtrip 없음(catalog-only, content-2 선례).

## 변경 대상

- `Assets/_Project/Art/DreamcatcherCards/dreamcatcher_card_23.png(.meta)` 신규 (2:3, 1024×1536, Single Sprite, mipmap off). 실아트 없으면 placeholder("PLACEHOLDER 응축된 일격").
- `Assets/_Project/Data/Dreamcatcher/Card_HeavyStrike.asset(.meta)` 신규.
- `Assets/_Project/Data/Dreamcatcher/DreamcatcherCardCatalog.asset` — 1종 등록(total 26).

## 저작 스펙

DreamcatcherCard script guid = `cdcd617d396824acd8882a45466b4886`. 공통: `axis: 3`(All), `category: 1`(Unique), `type: 1`(Unit).

```yaml
  id: heavy_strike
  displayName: "응축된 일격"
  axis: 3
  category: 1
  effects: []
  art: {fileID: 21300000, guid: <dreamcatcher_card_23 sprite guid>, type: 3}
  mechanics:
  - trigger: { kind: 1, period: 5, periodSeconds: 0, fraction: 0 }   # 1 = AttackN, 5회마다
    payload:
      kind: 13                    # 13 = HeavyStrike
      magnitude: 2                # ×2 배율
      projectile: {fileID: 0}
      tileRange: 0
      duration: 0
      auraPrefab: {fileID: 0}
      auraScale: 0
      ccKind: 0
      stackKind: 0
      buffStat: 0
  attackMods: []
  type: 1
  description: "다섯 번째 공격마다 짓누르는 강공 — 그 일격의 피해가 2배가 된다."
```

## 저작 절차

1. placeholder PNG 1024×1536 생성 → `Art/DreamcatcherCards/dreamcatcher_card_23.png` import, Single Sprite·mipmap off.
2. SO 생성(`manage_scriptable_object` 우선; execute_code 고장 시 YAML). `art`=23 sprite.
3. `DreamcatcherCardCatalog.asset` 에 등록. 기본 덱·씬 미변경(런타임 리프레셔 자동 열거).
4. `read_console` import 에러 0 확인.

## 완료 기준

- [x] SO import 에러 0(Unity 콘솔 clean), `assetType=Wassup.Data.DreamcatcherCard`, guid `55b4f3ae2e2646b3a1963e2f9170583a`. catalog 등록(total 26), id 중복 없음. — 2026-07-14
- [x] SO 값 = 문안: `trigger.kind=1`(AttackN)/`period=5`, `payload.kind=13`(HeavyStrike)/`magnitude=2`, type=1(Unit)/axis=3(All)/category=1(Unique). (bake 값은 attach 시점 = Play 에서 확인)
- [x] 아트: placeholder `dreamcatcher_card_23.png` 1024×1536, Single Sprite(textureType 8/spriteMode 1)/mipmap off(guid `a2546ca7be13ed84aa75f0181a61a219`). 실아트는 후속 교체.
- [x] **Play + 로그 검증 — 2026-07-14**: 사용자 Play 후 BattleLogger 로그(`GameLogs/session-20260714-100512`) 분석. 배스티온(근접) 공격 #1~9=31.0 평타, **#10=62.0 정확히 ×2.00** (부착 후 5회째 — period=5 from attach 일치). 전체 로그 유일한 exact-2.0×, 타 유닛 버프배율(1.24/1.75)만·무회귀. 투사체(캐논)는 로그상 base 기록(hit-site 적용 = unit 2 설계, 로그 미포함) → 화면 숫자로 육안 확인.
