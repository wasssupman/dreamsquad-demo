using System.Collections.Generic;
using Wassup.Battle.Effects;
using Wassup.Data;

namespace Wassup.BattleCoreUnity
{
    // battle-core-rebuild unit 5a — **정의표 줄 번호 → 저작 에셋** 되찾기 표.
    //
    // 정의표는 plain 이라 프리팹·머티리얼·스켈레톤을 모른다(계약 6). 그런데 뷰는 그것이 있어야
    // 무엇을 그릴지 안다. 사건이 나르는 `DefIndex` 로 되찾으려면 **정의표를 만든 그 순회가
    // 매긴 번호**와 같은 순서의 에셋 목록이 필요하다.
    //
    // ⚠ 이 목록을 뷰 쪽에서 **다시 모으지 않는다.** 두 벌이 되는 순간 「표엔 있는데 아무도
    // 안 가리키는 탄」과 그 반대가 조용히 생기고, 그 어긋남은 탄이 엉뚱한 프리팹으로 날아가는
    // 모습으로만 드러난다. 그래서 번호를 매긴 쪽이 목록도 같이 내놓는다.
    //
    // 선택 인자다 — 넘기지 않으면 빌더는 아무것도 채우지 않고 지금까지와 똑같이 돈다
    // (헤드리스 lane·EditMode 테스트는 뷰가 없어 이 표가 필요 없다).
    public sealed class MatchViewAssets
    {
        private readonly List<ProjectileData> _projectiles = new List<ProjectileData>();
        private readonly List<StructureData> _structures = new List<StructureData>();
        // unit 6c — 판 위에 깔리는 것. 같은 규율이다: **`BoardEffectDefinitionBuilder.Fill` 이
        // 줄 번호를 매긴 그 순회**가 이 목록도 채운다(존 = `Hazards`, 길막 = `BlockingHazards`).
        private readonly List<HazardSO> _hazards = new List<HazardSO>();
        private readonly List<BlockingHazardSO> _blockers = new List<BlockingHazardSO>();

        public IReadOnlyList<ProjectileData> Projectiles => _projectiles;
        public IReadOnlyList<StructureData> Structures => _structures;
        public IReadOnlyList<HazardSO> Hazards => _hazards;
        public IReadOnlyList<BlockingHazardSO> Blockers => _blockers;

        public void SetProjectiles(List<ProjectileData> rows)
        {
            _projectiles.Clear();
            if (rows != null) _projectiles.AddRange(rows);
        }

        public void SetStructures(List<StructureData> rows)
        {
            _structures.Clear();
            if (rows != null) _structures.AddRange(rows);
        }

        public void SetHazards(HazardSO[] rows)
        {
            _hazards.Clear();
            if (rows != null) _hazards.AddRange(rows);
        }

        public void SetBlockers(List<BlockingHazardSO> rows)
        {
            _blockers.Clear();
            if (rows != null) _blockers.AddRange(rows);
        }

        public HazardSO Hazard(int defIndex)
            => defIndex >= 0 && defIndex < _hazards.Count ? _hazards[defIndex] : null;

        public BlockingHazardSO Blocker(int defIndex)
            => defIndex >= 0 && defIndex < _blockers.Count ? _blockers[defIndex] : null;

        public ProjectileData Projectile(int defIndex)
            => defIndex >= 0 && defIndex < _projectiles.Count ? _projectiles[defIndex] : null;

        public StructureData Structure(int defIndex)
            => defIndex >= 0 && defIndex < _structures.Count ? _structures[defIndex] : null;

        // ── unit 7c — 카드와 규칙 줄의 그림 ─────────────────────────────────────
        //
        // 같은 규율이다: **카드 줄 번호를 매긴 순회**(`CardDefinitionBuilder.Fill` — 빈 칸을 건너뛴다)가 카드 에셋 목록을,
        // **규칙 줄 번호를 매긴 순회**(`BindingDefinitionBuilder.Bake`)가 줄별 연출 프리팹을 채운다. 뷰는 사건이 나른
        // `DefIndex`(카드 줄 · 규칙 줄)로 되찾기만 한다 — 뷰가 덱 목록을 다시 모으면 빈 칸 하나에 번호가 밀린다.
        private readonly List<DreamcatcherCard> _cards = new List<DreamcatcherCard>();
        private readonly Dictionary<int, (UnityEngine.GameObject prefab, float scale)> _bindingAuras
            = new Dictionary<int, (UnityEngine.GameObject, float)>();
        private readonly List<UnityEngine.GameObject> _skillVfx = new List<UnityEngine.GameObject>();

        public IReadOnlyList<DreamcatcherCard> Cards => _cards;

        public void SetCards(List<DreamcatcherCard> rows)
        {
            _cards.Clear();
            if (rows != null) _cards.AddRange(rows);
        }

        public DreamcatcherCard Card(int cardIndex)
            => cardIndex >= 0 && cardIndex < _cards.Count ? _cards[cardIndex] : null;

        /// <summary>규칙 줄이 선언한 **부착 오라**(옛 `DcAuraVisualPool.Register` — 메커닉 저작 `auraPrefab`). 새 판마다 비운다.</summary>
        public void ClearBindingVisuals()
        {
            _bindingAuras.Clear();
            _skillVfx.Clear();
        }

        public void SetBindingAura(int row, UnityEngine.GameObject prefab, float scale)
        {
            if (prefab == null || row < 0) return;
            _bindingAuras[row] = (prefab, scale);
        }

        public bool TryGetBindingAura(int row, out UnityEngine.GameObject prefab, out float scale)
        {
            if (_bindingAuras.TryGetValue(row, out var v)) { prefab = v.prefab; scale = v.scale; return true; }
            prefab = null; scale = 0f;
            return false;
        }

        /// <summary>
        /// 스킬 연출 표(빔 — 옛 `GetOrCreateSkillVfxIndex`). 규칙 줄의 `DataIndex` 가 이 번호를 싣는다(`SkillVisual.DefIndex`).
        /// 같은 프리팹은 같은 번호다.
        /// </summary>
        public int RegisterSkillVfx(UnityEngine.GameObject prefab)
        {
            if (prefab == null) return -1;
            int at = _skillVfx.IndexOf(prefab);
            if (at >= 0) return at;
            _skillVfx.Add(prefab);
            return _skillVfx.Count - 1;
        }

        public UnityEngine.GameObject SkillVfx(int index)
            => index >= 0 && index < _skillVfx.Count ? _skillVfx[index] : null;
    }
}
