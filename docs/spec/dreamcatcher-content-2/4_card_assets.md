# 4 — 카드 에셋 (SO 2종 + catalog + 아트) — **착수 대기(에디터+아트 필요)**

> 상태: 코드(units 0~3) 완료·검증·커밋. 이 unit은 **라이브 Unity 에디터(MCP)** 와 **카드 아트 2장**이 필요해 미착수. 아래는 다음 세션이 바로 실행할 수 있는 저작 스펙.

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

## 완료 기준 (Play 검증)

- [ ] 두 SO import 에러 0, catalog에 등록, ID 중복 없음, art != null.
- [ ] 덱빌더 COLLECTION + 전투 손패에 두 카드 노출(기존 UI 자동 소비).
- [ ] **악몽의 여운 e2e**: 부착 유닛이 킬 크레딧 → DamageMul +0.15, 같은 슬롯 재처치 시 +0.30 안 됨(TTL만 5s 갱신), 만료 후 baseline. Afterglow 투사체 처치도 발동.
- [ ] **끝을 보는 눈 e2e**: 곡선 경로에서 world-근접이 아니라 flow-잔여 최소 적을 주 대상 선택·고정, 그 대상 직접 피해 1.2배. 무카드 유닛 무회귀.
- [ ] 카피 수치(15%/5s/20%) 문안 ↔ 데이터 일치 육안 확인.
