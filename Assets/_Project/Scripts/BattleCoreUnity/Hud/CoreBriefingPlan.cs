using System.Collections.Generic;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCore.Wave;
using Somnia.Battle.Data;

namespace Somnia.Battle.BattleCoreUnity.Hud
{
    // battle-core-rebuild unit 8a — 메뉴 웨이브 브리핑의 **입력 어댑터**(구현 5).
    //
    // 옛 메뉴는 `GameManager.BuildBriefingWavePlan`(→ 브리지 → 옛 생성기 `WavePatternGenerator.Generate`)
    // 으로 브리핑용 플랜을 **다시 생성**했다(`MenuPopup.cs:87~89`). 여기서는 다시 만들지 않고
    // **코어 `WaveScheduler` 가 이 판에 실제로 쓰는 플랜**을 스트립의 입력 모양(`GeneratedWavePlan`)으로
    // 옮겨 적기만 한다 — 예고와 실전이 같은 값에서 나온다.
    //
    // ⚠ 스트립의 덱 경로(`WavePatternStripView.RebuildFromDeck` → 옛 생성기)는 **부르지 않는다**.
    // 그래야 unit 9 가 옛 생성기의 소비처를 셀 수 있다.
    //
    // 적은 코어에서 **정의표 인덱스**다. 스트립은 저작 에셋(`AttackUnitData`)을 그리므로 드라이버의
    // `EnemyAssets`(같은 빌더 함수가 같은 순서로 모은 목록)로 되찾는다 — 번호를 매긴 쪽과 목록을
    // 내놓는 쪽이 같다는 규칙은 뷰 풀과 같다. 규칙은 하나도 없다: 옮겨 적기만 한다.
    public static class CoreBriefingPlan
    {
        /// <summary>
        /// 코어 플랜 → 스트립 입력. 웨이브가 없으면 `default`(스트립은 빈 프리뷰를 그린다).
        /// 플랜 전역 스칼라(시드·간격)는 스트립이 읽지 않아 0 으로 둔다 — 스트립의 입력은 웨이브 목록뿐이다.
        /// </summary>
        public static GeneratedWavePlan From(WaveScheduler waves, IReadOnlyList<AttackUnitData> enemies)
        {
            if (waves == null || waves.WaveCount <= 0) return default;
            var list = new List<GeneratedWave>(waves.WaveCount);
            for (int i = 0; i < waves.WaveCount; i++)
            {
                var w = waves.WaveAt(i);
                int n = w.Groups != null ? w.Groups.Length : 0;
                var groups = new List<WaveSpawnGroup>(n);
                for (int g = 0; g < n; g++)
                {
                    var src = w.Groups[g];
                    var unit = enemies != null && src.EnemyIndex >= 0 && src.EnemyIndex < enemies.Count
                        ? enemies[src.EnemyIndex] : null;
                    groups.Add(new WaveSpawnGroup(unit, src.Count, src.TriggerOffsetSec, src.LaneIndex, src.PathIndex));
                }
                list.Add(new GeneratedWave(w.WaveIndex, w.TriggerTimeSec, groups, w.SpawnIntervalSec,
                    w.Layout == WaveLayout.Timeline ? WaveExpandMode.PerGroupTimeline : WaveExpandMode.RoundRobin,
                    w.ConceptLabel));
            }
            return new GeneratedWavePlan(0, 0, 0f, 0f, 0f, list);
        }
    }
}
