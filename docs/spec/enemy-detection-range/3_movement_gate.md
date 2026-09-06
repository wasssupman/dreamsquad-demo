# 3 — 이동 게이트 교체

## 목적

감지가 성립한 적이 **골 경로 대신 사냥판을 따르게** 한다. 이 unit 에서 처음으로 화면이 바뀐다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs` — `hunting` 판정 한 곳
- `Assets/_Project/Scripts/Battle/Effects/DefenderFieldSystem.cs` — 주석만(코드 무변)

## 구현

오늘 `MovementSystem` 의 사냥 게이트는 이렇다:

```csharp
bool hunting = hasHuntField && huntField.IsCreated
    && hunterLookup.HasComponent(entity)
    && huntField.dist[idx] != int.MaxValue;
```

「태그를 가졌나」를 「이번 프레임 감지가 성립했나」로 바꾼다:

```csharp
bool hunting = hasHuntField && huntField.IsCreated
    && detectedLookup.HasComponent(entity) && detectedLookup[entity].hunting != 0
    && huntField.dist[idx] != int.MaxValue;
```

무제한 감지(`detectionRange < 0`)는 `DetectionSystem` 이 항상 `hunting = 1` 을 주므로 **오늘 보스의
거동이 그대로 재현된다** — 이 등가가 이 unit 의 무회귀 근거다.

**바뀌지 않는 것들** — 이 게이트 하나만 바꾸면 나머지가 공짜로 따라온다:

- `dist == MaxValue`(도달 불가) 거부 · 방어유닛 0 기일 때 골 복귀(`DefenderFieldSystem` 계약 5) ·
  `distance-based-range` unit 4c 의 「도착했는데 사거리 밖」 접근 보정 — **전부 `hunting` 변수를
  읽으므로 자동으로 감지에 붙는다.**
- 어그로 우선: `Chasing` 분기가 **이 코드보다 위**에 있어 먼저 `continue` 한다(계약 2).
- `Engaging` 분기도 위에 있다 — 사거리에 들면 감지와 무관하게 기존 `engageMovement` 가 지배한다.

⚠⚠ **하나는 «공짜»가 아니라 규칙 변경이다 — 반드시 분리한다.** 골 도달 판정이

```csharp
if (!hunting && !patrolling && field.IsGoalCell(cell))
    ecb.AddComponent<PastGoalTag>(entity);
```

라서, `hunting` 을 감지로 바꾸면 **감지 중인 적은 골 칸을 밟아도 공성으로 전환하지 않는다.**
그 뒤 사슬은 `GoalReachedEvent` → 마음 HP 감소 → `StressMath` → 스트레스 100 = 판 종료이므로,
감지가 **이 게임의 유일한 패배 통로의 조절기**가 된다.

leak-proof 는 **보스·보너스 적의 성질로 저작된 것**이다(`boss-defender-field` — 전멸시켜야 골에
간다). 매 웨이브 수십 기의 잡몹에게 상속시키는 것은 **별개 결정**이고 이 spec 의 검증 질문
(교전 밀도)과 무관하다. 그러므로 **두 개념을 가른다**:

```csharp
bool hunting   = ... detected.hunting != 0 ...;   // 이동 소스 선택
bool leakProof = ...;                             // 골 전환 면제 = **무제한 감지 전용**(아래)
if (!leakProof && !patrolling && field.IsGoalCell(cell)) { ... }
```

⚠⚠ **`leakProof` 를 `hunting` 에 묶으면 안 된다**(ECS 리뷰 H2 — 초판이 그렇게 썼다가 고쳤다).
`hunting` 은 감지 타이머(관성·막힘 해제·억제)로 매 프레임 꺼질 수 있고, 그 틈에 **무제한
사냥꾼이 골을 유출한다.** `Enemy_DreamShard` 는 비보스라 CC 면역이 없어 **자장가 한 번으로 그
틈이 열리고**, `BonusWaveData` 가 보너스 적에게 무제한을 강제하는 이유(「이 적은 골로 안 간다」)가
조용히 깨진다. 그래서 **옛 술어를 그대로 쓰되 무제한으로만 한정한다**:

```csharp
bool leakProof = hasHuntField && huntField.IsCreated
    && detectionTiles < 0f
    && huntField.dist[idx] != int.MaxValue;   // ← 옛 `hunterLookup` 술어와 같은 모양
```

오늘 `Unlimited` 인 4종은 옛 `DefenderHunterTag` 부착 4종(보스 3 + `DreamShard`)과 **정확히 같은
집합**이라 이 형태가 계약 7 의 무회귀를 산술적으로 만족한다. 유한 반경 감지는 골 전환을 안 건드린다.

⚠ 이 분리를 **unit 3 에서 한다**(뒤로 미루지 않는다). 미루면 units 3~5 동안 「감지 적이 안
샌다」가 라이브 거동이 되고, 그 상태로 잰 재측정은 밸런스에 대해 거짓말을 한다.

⚠ **`hunterLookup` 을 지우지 말 것.** 이 게이트에서 소비처가 0 이 되지만 `OnUpdate` 의
`GetComponentLookup` 호출은 **남긴다**. 이 프로젝트에서 「소비처 0 이 된 lookup 을 지웠더니 Burst 가
조용히 깨져 NRE」가 **네 번 재발했다**(memory: burst-lookup-removal-nre). 소비처 0 임을 주석으로
적고 존치한다.

### 사냥판이 «두 가지»를 안 보는 이유 — 설계가 아니라 상속받은 공백

`DefenderFieldSystem` 은 소스를 모을 때 **사격 가능성(legality)도, 통행 층도** 안 본다.
둘 다 의도된 배제가 아니라 **스펙 두 개의 시야가 어긋난 자리**다:

- `boss-defender-field` 는 **층 축이 생기기 전**에 만들어졌다. 그 spec 의 후속 후보에도
  「R-별 필드 분리」는 있지만 층 이야기는 없다 — 당시엔 통행 개념이 하나뿐이었으니 없는 게 맞다.
- `traversal-layers` 가 층 축을 도입하며 골 라우팅(슬롯 소비처 8곳)·충돌(unit 5)·순찰(unit 3)을
  훑었는데 **사냥판은 그 목록에 없다** — 그 spec 폴더 전체에 `DefenderField` 문자열이 **0건**이다.

그래서 라우팅 6곳 중 5곳만 층을 알게 됐고 사냥판 하나가 남았다.

**왜 오늘까지 안 드러났나**: 사냥판 소비자가 보스 3종 + `DreamShard` **4기뿐이었고 전부 지상**이라
층을 물을 일이 없었다. 이 spec 이 감지를 그 레인 위에 얹으면서 처음으로 비행이 후보가 됐고,
그때 드러났다. 「소비자가 하나일 때 무해하던 가정이 소비자가 늘면서 결함이 된다」는 형태이고,
아래 legality 공백과 **같은 계열**이다.

⚠ **`DefenderFieldSystem` 은 코드를 바꾸지 않는다.** unit 1 이 태그 부착 조건만 바꿨으므로 재빌드
게이트(`hunterQuery.IsEmpty`)와 소스 반경 R 산출은 그대로 동작한다. 다만 **의미가 넓어졌다** —
R 은 이제 「동시에 감지 중인 적들의 최소 사거리」로 접히므로, 사거리가 크게 다른 적들에게 감지를
켜면 소스 디스크가 짧은 쪽으로 내려간다. 오늘 무해하고(감지 적 4건) 실측도 무해했지만, unit 6 에서
감지를 켜는 순간 이 결합이 살아난다 — 그 문서의 관찰 항목이자 README 후속 후보다.

## 완료 기준

- compile 통과 · EditMode 전체 초록(선행 실패 2건 제외).
- EditMode 신규: `hunting == 0` 인 적은 골 흐름장을 따르고, `1` 인 적은 사냥판을 따른다
  (합성 월드에서 한 스텝 뒤 위치의 방향으로 판정).
- EditMode 신규: `Aggroed` + `hunting == 1` 인 적은 **가디언 쪽**으로 간다(계약 2 회귀 가드).
- EditMode 신규 — **유출/공성 분리 가드(가장 중요)**:
  - `detectionRange = 3`(유한) + `hunting == 1` 인 적이 골 셀을 밟으면 **`PastGoalTag` 가 붙는다**
    (= 오늘과 같이 공성 전환).
  - `detectionRange < 0`(무제한) + `hunting == 1` 인 적은 골 셀을 밟아도 **안 붙는다**(오늘 보스 거동).
  이 두 단언이 없으면 「감지가 패배 통로를 조용히 막는」 회귀가 테스트를 통과한다.
- **거동 무변** — ⚠ 골든 `Verify` 는 이 판정에 못 쓴다. 코퍼스가 이 spec 이전부터 stale 이고
  (unit 1 완료 기준 참조) `configHash` 도 스키마 변경으로 이미 움직였다. 대신 **이 unit 의
  변경 한 줄만 임시로 끄고 verify 를 돌려 켠 실행과 이벤트/킬을 대조한다** — 같으면 무변이다.
  감지 저작이 아직 보스 3종 + `DreamShard` 뿐이고 그들은 무제한이라 `hunting` 이 항상 1 이다.
  A/B 가 갈리면 등가가 깨진 것이므로 **재베이크하지 말고 원인을 찾는다.**
- Play 육안: 보스가 예전처럼 방어유닛을 찾아다닌다(무회귀 확인).
