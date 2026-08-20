# 8 — 저작 플랜의 레인 축 · 1웨이브 재구성

## 목적

**첫 웨이브를 한 줄로 뭉쳐서 아래 레인 하나로만 보낸다.** 지금은 15마리가 양쪽 레인으로
갈려 1.8타일 길이의 두 줄로 들어온다 — 배치 스킬 한 번이 덮을 수 있는 그림이 아니다.
unit 9 의 배치 스킬 두 개(스턴 2타일 · 산탄 4타일)가 **한 덩어리를 통째로** 처리하는
장면을 만드는 것이 이 작업의 전부다.

## 변경 대상

- `Assets/_Project/Scripts/Data/WavePlanAsset.cs` — `AuthoredSpawnGroup.laneIndex`
- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — `FromPlanAsset` 전달
- `Assets/_Project/Scripts/Data/WavePlans/WavePlan_FirstRunTutorial.asset`
- `Assets/_Project/Data/Projectiles/Pattern_Shotgunner_Blast.asset`

## 구현

**저작 플랜에는 레인 칸이 없다.** 런타임 `WaveSpawnGroup` 은 이미 `laneIndex`(-1=무지정)를
갖고 `ExpandWave`/`ResolveAuthoredLane` 이 그것을 존중하는데, `AuthoredSpawnGroup` 이
그 축을 노출하지 않아 `FromPlanAsset` 이 3인자 생성자로만 부른다. 그래서 **필드 하나를
열고 그대로 넘기는 것**이 코드 변경의 전부다.

```csharp
// AuthoredSpawnGroup
[Tooltip("스폰 지점 인덱스. -1 = 무지정(펼침 순번 % 레인 수 라운드로빈).")]
[Min(-1)] public int laneIndex = -1;

// FromPlanAsset
groups.Add(new WaveSpawnGroup(grp.unit, grp.count, math.max(0f, grp.triggerTimeSec), grp.laneIndex));
```

기본값 -1 이라 **기존 저작 플랜은 전부 무변화**다.

### 에셋 값 (반영 완료)

| 대상 | 전 | 후 | 이유 |
|---|---|---|---|
| 1웨이브 `count` | 15 | **10** | 배치 스킬 한 번에 덮이는 양 (6 → 10, 2026-08-20 Play 확인 후 상향) |
| 1웨이브 `intervalSec` | 0.1 | **0.05** | 0.45초 안에 다 나와 ~0.6타일 덩어리. 0 으로 두지 않은 건 같은 칸 동시 스폰이 밀어냄끼리 싸울 여지가 있어서 — 육안으로는 동시다 |
| 1웨이브 `laneIndex` | (없음) | **0** | 아래 레인. `SiegeSpawnOffsets[0] = (0,-1)` → Duel 은 적 마음(18,5)에서 **(18,4)** |
| `Pattern_Shotgunner_Blast.damage` | 40 | **100** | 60HP 기본몹을 스치는 족족 처치 |

**관통은 손대지 않는다 — 이미 무제한이다.** `Projectile_ShotgunBlast` 는
`flightMode: Directional`(경로 스윕) · `pierceCount: 99` · `rehitCooldownSec: 0` 이다.
다중 피격을 막고 있던 것은 관통이 아니라 **데미지**였다: 99마리를 훑고 지나가도 40 이라
60HP 가 안 죽어 「스쳤는데 살아 있는」 그림이 났다. 부채 기하도 이미 충분하다 —
5발이 ±40° 로 4타일까지 나가고 인접 탄 간격이 4타일 지점에서 ~1.37타일인데 스윕 반경이
1.2 라 거의 겹친다(근거리일수록 더).

⚠ `Pattern_Shotgunner_Blast` 의 소비자는 `Ability_OnPlaceBlast_Shotgunner` 하나뿐이지만
그것은 **실전 샷건맨의 배치 스킬**이다. 이 값 변경은 튜토리얼 국한이 아니라 본 게임
밸런스 변경이다. (시트 임포터는 유닛 스탯만 소유하고 패턴 에셋은 건드리지 않는다.)

## 알아둘 것

- **레인 고정은 배치 위치 판단에 영향이 없다.** `Enemy_Basic.waypointPathIndex: -1` 이라
  웨이포인트(10,5)를 안 쓰고 골(2,5)로 직행한다 → x≈7 지점에선 어차피 골 행으로
  수렴해 있다. 레인이 바꾸는 것은 **스폰 쪽 그림**이다.
- **타이밍 예산은 적 수량과 무관하다.** 접근 대기는 선두 적의 이동 거리가 결정한다
  (x18→x7, 1.3타일/초). 10마리든 15마리든 같다.
- **계약 14 가 뒤집힌다.** 그 계약은 "레인을 박으면 한쪽으로 몰린다"를 근거로 저작을
  금지했는데, 지금은 **그 몰림이 의도**다. README 에서 개정한다.

## 완료 기준

- [x] compile 통과 · 기존 저작 플랜(테스트 모드 카탈로그) 스폰 결과 무변화
- [x] Play — 1웨이브 10마리가 **아래 레인 한 곳에서** 덩어리로 나온다
- [x] Play — 샷건맨 배치 스킬 한 번이 덩어리 대부분을 처리한다

⚠ 10마리 = 600HP 이고 산탄은 5발 × 100 이다. 관통이 겹쳐 안쪽은 두 발 이상 맞아 확실히 죽지만,
**부채 가장자리는 남을 수 있다** — 6마리 시절의 «전멸» 과 다르다. 남는 것이 문제면 수량이 아니라
`Pattern_Shotgunner_Blast` 의 각도 폭(±40°)이나 발 수를 본다.

**확인**: 2026-08-20 사용자 Play 확인 — 전 구간 통과. `dotnet build Wassup.Runtime` 오류 0.
