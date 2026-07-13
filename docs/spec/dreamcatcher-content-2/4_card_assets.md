# 4 — 카드 에셋 (SO 2종 + catalog + 아트) — **착수 대기(에디터+아트 필요)**

> 상태: **에셋 저작·import·catalog 완료 2026-07-14 (임시 placeholder 아트).** 두 SO 데이터/import/catalog 등록을 execute_code로 검증. 실아트는 사용자가 별도 배정 → 교체 후 재검증. 라이브 Play e2e는 최종 육안 확인으로 남김.

## 목적

두 카드를 실제 SO로 만들어 catalog에 등록하고, 손패/COLLECTION 노출 + 인게임 동작을 Play로 검증한다. 시트 roundtrip 없음(catalog-only, 사용자 결정 1).

## 변경 대상

- `Assets/_Project/Data/Dreamcatcher/Card_NightmareAfterglow.asset(.meta)` 신규
- `Assets/_Project/Data/Dreamcatcher/Card_EyeOnTheEnd.asset(.meta)` 신규
- `Assets/_Project/Art/DreamcatcherCards/dreamcatcher_card_21.png(.meta)`, `dreamcatcher_card_22.png(.meta)` 신규 (2:3, 1024×1536, Single Sprite, mipmap off)
- `Assets/_Project/Data/Dreamcatcher/DreamcatcherCardCatalog.asset` — 2종 등록
- `Assets/_Project/Tests/PlayMode/` — Afterglow refresh/expiry + Eye e2e (선택)

## 저작 스펙 (검증된 enum 값 — DevouringCraving 대조)

DreamcatcherCard script guid = `cdcd617d396824acd8882a45466b4886`. 공통: `axis: 3`(All), `category: 1`(Unique), `type: 1`(Unit).

### Card_NightmareAfterglow (악몽의 여운 — 0-code, OnKill×SelfStatBuff)

```yaml
  id: nightmare_afterglow
  displayName: "악몽의 여운"          # YAML 저장 시 \uXXXX 이스케이프
  axis: 3
  category: 1
  effects: []
  art: {fileID: 21300000, guid: <dreamcatcher_card_21 sprite guid>, type: 3}
  mechanics:
  - trigger: { kind: 6, period: 0, periodSeconds: 0, fraction: 0 }   # 6 = OnKill
    payload:
      kind: 12                    # 12 = SelfStatBuff
      magnitude: 15               # +15%
      projectile: {fileID: 0}
      tileRange: 0
      duration: 5                 # 5s TTL (유한)
      auraPrefab: {fileID: 0}
      auraScale: 0
      ccKind: 0
      stackKind: 0
      buffStat: 0                 # 0 = CardBuffKind.AttackDamage (devouring=1=AttackSpeed)
  attackMods: []
  type: 1
  description: "이 유닛에게 처치가 귀속되면 5초 동안 공격력 +15%. 다시 처치하면 지속시간이 갱신된다."
```

### Card_EyeOnTheEnd (끝을 보는 눈 — FrontmostTarget attackMod)

```yaml
  id: eye_on_the_end
  displayName: "끝을 보는 눈"
  axis: 3
  category: 1
  effects: []
  art: {fileID: 21300000, guid: <dreamcatcher_card_22 sprite guid>, type: 3}
  mechanics: []
  attackMods:
  - kind: 2                       # 2 = DcAttackModKind.FrontmostTarget
    count: 0                      # 미사용
    tileRange: 0                  # 미사용
    damageMul: 1.2                # 주 대상 직접 피해 +20%
  type: 1
  description: "기본 공격은 사거리 안에서 목표 지점에 가장 가까운 악몽을 우선 노린다. 그 주 대상에게 주는 직접 피해 +20%."
```

## 저작 절차 (MCP 복구 후)

1. 아트 21/22 PNG 확보(아티스트 또는 임시 placeholder) → `Art/DreamcatcherCards/`에 import, Single Sprite·mipmap off·1024×1536.
2. 두 SO 생성(`manage_scriptable_object` 또는 위 YAML). `art`에 21/22 sprite 연결.
3. `DreamcatcherCardCatalog.asset`에 두 SO 등록(가용 카드 풀). 기본 10장 덱·씬 미변경 — `DcSheetRuntimeRefresher`가 자동 열거.
4. `read_console`로 import 에러 0 확인.

## 완료 기준

- [x] 두 SO import 에러 0, catalog 등록(total 25), ID 중복 없음, art != null. execute_code 검증: nightmare_afterglow=OnKill×SelfStatBuff/AttackDamage/15/5, eye_on_the_end=FrontmostTarget/1.2. — 2026-07-14
- [x] baked 값이 문안 수치(15%/5s/20%)와 일치. — 2026-07-14
- [~] 아트: **임시 placeholder(dreamcatcher_card_21/22, 1024×1536 Single Sprite, mipmap off, "PLACEHOLDER" 표기)**. 실아트 사용자 별도 배정 → 교체 후 재검증.
- [ ] 라이브 Play e2e(최종 육안): 덱빌더 COLLECTION/손패 노출, 악몽의 여운 refresh/expiry, 끝을 보는 눈 flow-타겟+1.2배, 무카드 무회귀. → 런타임 동작은 EditMode 20종으로 대리 검증됨(units 1~3). Afterglow arm은 devouring_craving(오늘 Play 검증)과 동일 arm.
