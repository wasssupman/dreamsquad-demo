using Unity.Mathematics;
using Wassup.Battle.Units;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 7d — **분열.** 슬라임을 잡으면 **그 칸에서** 자식이 퍼진다.
    //
    // ⚠ **계기는 「피해로 죽음」이다**(rev 3 정정 4 · H7 — `OnSlain`). 옛 전투는 적의 피해 사망 분기(`HP ≤ 0`)가
    // 처치 사건을 냈고 킬러를 따지지 않았다(`DamageApplicationSystem.cs:535` — 지속 피해·운석처럼 출처 없는 피해로
    // 죽어도 갈라졌다). 그래서 귀속된 처치 사건(`UnitSlain` — 킬러가 있을 때만)을 듣지 않고 **사망 seam** 을 쓴다:
    // 이번 틱 피해 단계가 표시한 죽음만 여기 온다. 치명 타이머·유출·순찰 수명은 이 문을 안 지난다(`OnDeath` 로 옮기면
    // 분열 조건이 넓어진다 — H7).
    //
    // ⚠ **자식은 전멸 판정 앞에 태어난다**(X2 ①). 사망 seam 은 전투 단계 안이고 웨이브 예약(`WaveScheduler`)은 그 뒤
    // 담당자 단계라, 부모만 죽은 틱에 「필드가 비었다」로 다음 웨이브가 당겨지지 않는다.
    //
    // 배치(E1 — 옛 `SpawnSplitChildren`):
    //   · 기준점 = **부모 칸의 중심**(연속 좌표에 더하면 자식이 옆 칸에 태어나 골이면 「처치했는데 유출」).
    //   · 배치각 = `2π·c/count` — **인덱스 결정론**(난수 금지).
    //   · 첫 분열 슬롯만 · 상한 = `MovementTuningDef.SplitMaxChildren`(빌더가 자르고 여기서 한 번 더) · 자기순환 차단(`SplitChain.Validate` 는 bake 가, 자기 자신은 여기서 한 번 더).
    //   · 자식은 부모의 레인·경로를 **안 물려받는다**(옛 `CreateEnemyEntity(child, pos)` 그대로 — 자기 저작 경로 · 칸의 흐름).
    //
    // 매니저가 아니다: 상태가 없고 판정은 「이번 틱에 피해로 죽은 분열체」 하나다. 조립은 `EnemySpawn` 한 문을 지난다.
    public static class EnemySplit
    {
        /// <summary>사망 seam 핸들러(`BattleMatch` 조립 시점 등록).</summary>
        public static void Run(TickContext ctx)
        {
            var map = ctx.Map;
            if (map == null || map.Snapshot.CellCount == 0) return;
            var units = ctx.World.Units;
            // 자식이 목록 끝에 붙는다 — 이번 틱에 태어난 자식은 죽지 않았으므로 다시 걸리지 않는다. 범위를 먼저 고정한다.
            int n = units.Count;
            for (int i = 0; i < n; i++)
            {
                var u = units[i];
                if (u.Kind != UnitKind.Enemy || !u.Dead || u.DeathTick != ctx.Tick) continue;
                if (u.DefIndex < 0 || u.DefIndex >= ctx.Def.Enemies.Length) continue;
                ref var d = ref ctx.Def.Enemies[u.DefIndex];
                if (d.SplitCount <= 0) continue;
                int child = d.SplitChildDefIndex;
                if (child < 0 || child >= ctx.Def.Enemies.Length)
                {
                    ctx.Warn($"[Split] '{d.Id}' 의 분열 자식 줄 {child} 이 표 밖이다 — 갈라지지 않는다.");
                    continue;
                }
                if (child == u.DefIndex)
                {
                    // 런타임 최후 방어선 — 직접 자기순환은 킬마다 개체가 배가돼 판이 끝나지 않는다(옛 가드 그대로).
                    ctx.Warn($"[Split] '{d.Id}' 의 분열 자식이 자기 자신이다 — 건너뛴다(무한 분열 방지).");
                    continue;
                }
                int2 cell = map.CellOf(u.Position);
                float radius = ctx.Def.Movement.SplitSpreadFraction * map.TileSize;
                int count = d.SplitCount;
                // M2 — 상한은 정의표 값이다. 빌더가 이미 잘랐어도 고정구·헤드리스는 빌더를 안 지난다 — 여기서 한 번 더.
                int cap = ctx.Def.Movement.SplitMaxChildren;
                if (cap > 0 && count > cap)
                {
                    ctx.Warn($"[Split] '{d.Id}' 의 분열 수 {count} 이 상한 {cap} 을 넘는다 — {cap}기만 세운다.");
                    count = cap;
                }
                for (int c = 0; c < count; c++)
                {
                    float angle = (math.PI * 2f * c) / count;
                    var offset = new float2(math.cos(angle) * radius, math.sin(angle) * radius);
                    EnemySpawn.At(ctx, map, child, -1, cell, -1, ctx.Tick, offset);
                }
            }
        }
    }
}
