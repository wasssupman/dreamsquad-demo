using System.Collections.Generic;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Data.MapGrid;
using Wassup.Logging;

namespace Wassup.Core
{
    public enum GamePhase { None, Draft, Placement, Battle, Result }

    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] private BattleLogger logger;
        [SerializeField] private BattleBridge battleBridge;
        [SerializeField] private DraftController draftController;
        [SerializeField] private CostRuntime costRuntime;
        [SerializeField] private CostConfig costConfig;
        [SerializeField] private SkillLoadoutController skillLoadout;
        // squad-loadout Unit 3 — squad carry-in source (drives the squad branch
        // in Start; null/empty → existing draft path).
        [SerializeField] private PlayerProfileSO profileSO;
        [SerializeField] private DefenderCatalog catalog;
        // dreamstone-loadout Unit 3 — resolves SquadSave.stoneIds to assets for carry-in.
        [SerializeField] private DreamstoneCatalog stoneCatalog;

        // match-seed-unification — 단일 매치 시드 소유. 맵·웨이브가 여기서 파생된다.
        [Header("Match Seed")]
        [Tooltip("0 이면 매 판 새 시드. 0 이 아니면 재현용 고정 — 맵·웨이브가 매 판 동일.")]
        [SerializeField] private int debugFixedMatchSeed = 0;
        public int MatchSeed { get; private set; }
        public bool MatchSeedFixed => debugFixedMatchSeed != 0;

        public BattleLogger Logger => logger;
        public DraftController DraftController => draftController;
        public CostRuntime CostRuntime => costRuntime;
        public CostConfig CostConfig => costConfig;
        public SkillLoadoutController SkillLoadout => skillLoadout;
        public bool IsAiming { get; set; }
        public DefenderUnitData SelectedDefender { get; set; }

        public GamePhase CurrentPhase { get; private set; } = GamePhase.None;
        public event System.Action<GamePhase> PhaseChanged;

        // squad-loadout Unit 3 — raised when squad mode is ready for placement
        // (no draft). PlacementPhaseView subscribes, mirroring DraftConfirmed.
        public event System.Action PlacementRequested;

        // squad map-setup — raised after the squad map is built so the player can
        // freely adjust map settings before placement (replaces the draft stage's
        // map panel). SquadPrepView subscribes and calls RequestPlacement() on
        // confirm. If nothing subscribes (headless), squad mode goes straight to
        // placement.
        public event System.Action MapSetupRequested;
        public void RequestPlacement() => PlacementRequested?.Invoke();

        // Fired whenever a UI layer wants all *other* aim-style selections to
        // cancel (e.g. picking a defender should cancel any active skill aim,
        // picking a skill slot should clear the pending defender selection).
        // Subscribers clear their own local aim state; publisher does not know
        // who listens. Keeps input modes mutually exclusive — last click wins.
        public event System.Action AimCanceled;
        public void RaiseAimCanceled() => AimCanceled?.Invoke();

        public void SetPhase(GamePhase phase)
        {
            if (CurrentPhase == phase) return;
            CurrentPhase = phase;
            PhaseChanged?.Invoke(phase);
        }

        private void Awake()
        {
            Screen.SetResolution(1920, 1080, true);
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // outgame-scene-and-flow Unit 3 — GameManager is battle-scoped and
            // non-persistent. Loading OutgameScene must tear it down so each
            // battle re-entry starts fresh (no stale singleton / state leak).
            // (Previously DontDestroyOnLoad — meaningless in the old single-scene
            // setup, harmful in the two-scene flow.)

            if (logger == null)
            {
                Debug.LogWarning("[GameManager] BattleLogger reference missing. Attempting GetComponentInChildren.");
                logger = GetComponentInChildren<BattleLogger>();
            }
            if (costRuntime == null) costRuntime = GetComponentInChildren<CostRuntime>();
            if (costRuntime != null && costConfig != null)
            {
                costRuntime.Configure(costConfig.startingCost, costConfig.maxCost, costConfig.regenPerSec);
            }
            if (skillLoadout == null) skillLoadout = GetComponentInChildren<SkillLoadoutController>();
            if (skillLoadout == null)
            {
                skillLoadout = gameObject.AddComponent<SkillLoadoutController>();
                Debug.LogWarning("[GameManager] SkillLoadoutController missing; added fallback component on GameManager.", this);
            }
        }

        private void OnEnable()
        {
            if (logger != null) logger.StartSession();
        }

        // match-seed-unification — 매치당 1회: 고정 노브 우선, 아니면 새 랜덤 시드.
        // BattleBridge 에 주입하면 맵·웨이브가 이 시드에서 파생된다(작업 2/3).
        private void EnsureMatchSeed()
        {
            MatchSeed = debugFixedMatchSeed != 0 ? debugFixedMatchSeed : Wassup.Core.MatchSeed.GenerateRandom();
            if (battleBridge != null) battleBridge.SetMatchSeed(MatchSeed);
            if (logger != null) logger.SetMatchSeeds(MatchSeed, MatchSeedFixed);
            Debug.Log($"[GameManager] matchSeed={MatchSeed} (fixed={debugFixedMatchSeed != 0})");
        }

        // Start runs after all Awake/OnEnable of peers, so DraftView has subscribed
        // to DraftController events before we emit DraftStarted.
        private void Start()
        {
            // match-seed-unification — 맵을 빌드하는 PrepareDraftMap 보다 먼저 매치 시드를
            // 확정·주입한다. Draft·Squad 양 경로 공통으로 여기서 1회 보장.
            EnsureMatchSeed();

            // wave-authoring-test-mode unit 3 — 테스트 모드 최상위 분기. 작성 플랜 +
            // 디펜더 프리셋으로 드래프트/스쿼드를 모두 건너뛴다. 비활성이면 무변경.
            if (TestModeContext.Active && battleBridge != null)
            {
                StartTestModeMatch();
                return;
            }

            // squad-loadout Unit 3 — squad mode takes priority. Empty/unset squad
            // falls through to the existing draft path (A non-destructive).
            var squad = (profileSO != null && profileSO.profile != null) ? profileSO.profile.SelectedSquad() : null;
            if (squad != null && !squad.IsEmpty() && battleBridge != null && catalog != null)
            {
                StartSquadMatch(squad);
                return;
            }

            if (draftController != null)
            {
                // draft-stage-map-prebuild Unit 2 — build the map before entering Draft
                // so the playfield renders behind the card fan. Option toggles trigger
                // RebuildDraftMap via DraftController (Unit 3).
                logger?.SetEntryMode("draft");
                if (battleBridge != null) battleBridge.PrepareDraftMap();

                SetPhase(GamePhase.Draft);
                draftController.BeginDraft();
            }
            else if (battleBridge != null)
            {
                Debug.LogWarning("[GameManager] draftController unset; starting battle with inspector defenderPool fallback.");
                logger?.SetEntryMode("direct");
                SetPhase(GamePhase.Battle);
                battleBridge.StartBattle();
            }
        }

        // squad-loadout Unit 3 — skip the draft and bring the selected squad
        // straight into placement. Deterministic: exactly the saved squad units
        // (rev 2026-06-05 — no random fill).
        private void StartSquadMatch(SquadSave squad)
        {
            logger?.SetEntryMode("squad");
            battleBridge.SetMapGenerationOptions(MapGenerationOptions.Default);
            // squad-loadout regression fix — build the themed map (tile style +
            // background props) before placement, exactly as the draft path does
            // in Start(). Without this the BeginPlacement fallback left the map
            // unstyled and prop-less. BeginPlacement then sees the map created and
            // skips its rebuild.
            battleBridge.PrepareDraftMap();

            var ids = SquadDraw.Resolve(squad.unitIds);
            var units = new List<DefenderUnitData>(ids.Count);
            foreach (var id in ids)
            {
                var u = catalog.ById(id);
                if (u != null) units.Add(u);
            }
            if (units.Count == 0)
            {
                Debug.LogWarning("[GameManager] squad resolved to no units; falling back to draft.");
                if (draftController != null)
                {
                    battleBridge.PrepareDraftMap();
                    SetPhase(GamePhase.Draft);
                    draftController.BeginDraft();
                }
                return;
            }
            battleBridge.SetDefenderPool(units.ToArray());
            LogSquadCarryIn(squad, units);

            // dreamstone-loadout Unit 3 — set-then-apply: stage the squad's equipped
            // stones now; BattleBridge applies them once BeginPlacement clears+reapplies
            // its match-effect registry (see BattleBridge.BeginPlacement/SetDreamstones).
            battleBridge.SetDreamstones(ResolveEquippedStones(squad));
            // dreamstone-loadout Unit 6 — CostRate stones route to CostRuntime instead
            // of the entity registry above. Match-entry is one of the only two call
            // sites allowed to touch this multiplier (see CostRuntime's contract
            // comment) — ResetToStart/Configure (called every placement entry,
            // including mid-match Restart) must never set it.
            if (costRuntime != null) costRuntime.SetRegenRateMultiplier(ResolveCostRateMultiplier(squad));
            LogDreamstoneCarryIn(squad);

            // Skills stay independent of units — roll a fresh loadout like draft does.
            if (skillLoadout != null)
            {
                skillLoadout.Roll();
                LogSkillLoadout();
                if (skillLoadout.Picked.Count > 0)
                {
                    var arr = new SkillData[skillLoadout.Picked.Count];
                    for (int i = 0; i < arr.Length; i++) arr[i] = skillLoadout.Picked[i];
                    battleBridge.SetSkillLoadout(arr);
                }
            }

            // squad map-setup — let the player adjust the map first if a prep view
            // is present; otherwise go straight to placement (headless/tests).
            if (MapSetupRequested != null) MapSetupRequested.Invoke();
            else PlacementRequested?.Invoke();
        }

        // wave-authoring-test-mode unit 3 — 드래프트/스쿼드를 스킵하고 작성 플랜 +
        // 디펜더 프리셋으로 바로 placement 진입. StartSquadMatch 미러.
        private void StartTestModeMatch()
        {
            logger?.SetEntryMode("test", testMode: true);
            var plan = TestModeContext.Plan;
            var fallbackPreset = TestModeContext.DefenderPreset;
            TestModeContext.Clear(); // 1회 소비

            battleBridge.SetMapGenerationOptions(MapGenerationOptions.Default);
            battleBridge.PrepareDraftMap();

            battleBridge.SetAuthoredWavePlan(plan);

            // 디펜더는 기존에 저장된 스쿼드를 그대로 반입(StartSquadMatch 와 동일 해석).
            // 스쿼드가 비어 있을 때만 TestModeConfig 프리셋으로 폴백.
            var defenders = ResolveSquadDefenders();
            if ((defenders == null || defenders.Length == 0) && fallbackPreset != null && fallbackPreset.Length > 0)
                defenders = fallbackPreset;

            if (defenders != null && defenders.Length > 0)
            {
                battleBridge.SetDefenderPool(defenders);
                var squadForLog = (profileSO != null && profileSO.profile != null) ? profileSO.profile.SelectedSquad() : null;
                LogSquadCarryIn(squadForLog, new List<DefenderUnitData>(defenders));
            }
            else
                Debug.LogWarning("[GameManager] 테스트 모드 디펜더 없음 — 저장 스쿼드/프리셋 모두 비어 있음.");

            // dreamstone-loadout Unit 3 — StartSquadMatch 미러. 스톤은 스쿼드 소속이라
            // 디펜더가 프리셋으로 폴백해도(위 defenders) 저장 스쿼드의 장착 스톤은 그대로 반입한다.
            var stoneSquad = (profileSO != null && profileSO.profile != null) ? profileSO.profile.SelectedSquad() : null;
            battleBridge.SetDreamstones(ResolveEquippedStones(stoneSquad));
            // dreamstone-loadout Unit 6 — StartSquadMatch 미러 (CostRate 분리 적용).
            if (costRuntime != null) costRuntime.SetRegenRateMultiplier(ResolveCostRateMultiplier(stoneSquad));
            LogDreamstoneCarryIn(stoneSquad);

            // Skills stay independent of units — roll a fresh loadout like draft does.
            if (skillLoadout != null)
            {
                skillLoadout.Roll();
                LogSkillLoadout();
                if (skillLoadout.Picked.Count > 0)
                {
                    var arr = new SkillData[skillLoadout.Picked.Count];
                    for (int i = 0; i < arr.Length; i++) arr[i] = skillLoadout.Picked[i];
                    battleBridge.SetSkillLoadout(arr);
                }
            }

            Debug.Log($"[GameManager] 테스트 모드 진입 — plan='{(plan != null ? plan.displayName : "NULL")}' defenders={(defenders != null ? defenders.Length : 0)}.");

            // squad 와 동일하게 MAP SETUP 스텝이 있으면 거치고, 없으면 바로 placement.
            if (MapSetupRequested != null) MapSetupRequested.Invoke();
            else PlacementRequested?.Invoke();
        }

        // wave-authoring-test-mode — 저장 스쿼드를 디펜더 배열로 해석(StartSquadMatch 미러).
        // 스쿼드 없음/빈 경우 null 반환(호출부가 프리셋 폴백 판단).
        private DefenderUnitData[] ResolveSquadDefenders()
        {
            var squad = (profileSO != null && profileSO.profile != null) ? profileSO.profile.SelectedSquad() : null;
            if (squad == null || squad.IsEmpty() || catalog == null) return null;

            var ids = SquadDraw.Resolve(squad.unitIds);
            var units = new List<DefenderUnitData>(ids.Count);
            foreach (var id in ids)
            {
                var u = catalog.ById(id);
                if (u != null) units.Add(u);
            }
            return units.ToArray();
        }

        private void LogSquadCarryIn(SquadSave squad, List<DefenderUnitData> units)
        {
            if (logger == null || units == null) return;

            var ids = new List<string>();
            if (squad != null && squad.unitIds != null)
                foreach (var id in squad.unitIds)
                    if (!string.IsNullOrEmpty(id)) ids.Add(id);

            var names = new List<string>();
            foreach (var unit in units)
                if (unit != null) names.Add(unit.displayName);

            logger.SetSquad(squad != null ? squad.id : string.Empty, squad != null ? squad.name : string.Empty, ids, names);
        }

        private void LogDreamstoneCarryIn(SquadSave squad)
        {
            if (logger == null) return;

            var records = new List<DreamstoneRecord>();
            if (squad != null && squad.stoneIds != null && stoneCatalog != null)
            {
                for (int i = 0; i < squad.stoneIds.Count; i++)
                {
                    var id = squad.stoneIds[i];
                    if (string.IsNullOrEmpty(id)) continue;
                    var stone = stoneCatalog.ById(id);
                    if (stone == null) continue;
                    records.Add(new DreamstoneRecord
                    {
                        id = stone.id,
                        name = stone.displayName,
                        grade = stone.grade.ToString(),
                        kind = stone.effect.kind.ToString(),
                        percent = stone.effect.percent,
                        slotIndex = i,
                    });
                }
            }

            logger.SetDreamstones(records);
        }

        private void LogSkillLoadout()
        {
            if (logger == null || skillLoadout == null) return;

            var pickedIds = new List<string>();
            foreach (var skill in skillLoadout.Picked)
                if (skill != null) pickedIds.Add(skill.id);
            logger.SetSkillLoadout(pickedIds);

            var poolIds = new List<string>();
            foreach (var skill in skillLoadout.Pool)
                if (skill != null) poolIds.Add(skill.id);
            logger.SetSkillPool(poolIds, skillLoadout.Seed);
        }

        // dreamstone-loadout Unit 3 — resolve a squad's equipped stoneIds to assets
        // via the catalog, for the entity-buff path (BattleBridge.SetDreamstones).
        // Missing catalog/squad/list, or an id the catalog no longer has (asset
        // deleted), are skipped — same "resolve at read time, don't fail storage"
        // policy as ResolveSquadDefenders/SquadDraw use for unitIds.
        // dreamstone-loadout Unit 6 — CostRate stones are excluded here; they have
        // no entity stat and route to CostRuntime instead (ResolveCostRateMultiplier).
        private List<DreamstoneData> ResolveEquippedStones(SquadSave squad)
        {
            var stones = new List<DreamstoneData>();
            if (squad == null || squad.stoneIds == null || stoneCatalog == null) return stones;
            foreach (var id in squad.stoneIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                var stone = stoneCatalog.ById(id);
                if (stone != null && stone.effect.kind != CardBuffKind.CostRate) stones.Add(stone);
            }
            return stones;
        }

        // dreamstone-loadout Unit 6 — sum equipped CostRate stone percents into a
        // CostRuntime regen multiplier (1 + Σ%/100). Same resolve policy as
        // ResolveEquippedStones (skip missing catalog/squad/list/unresolved ids).
        // Only match-entry call sites (StartSquadMatch/StartTestModeMatch) call
        // this — see CostRuntime.SetRegenRateMultiplier for the full ownership
        // contract (ResetToStart/Configure must never touch the multiplier).
        private float ResolveCostRateMultiplier(SquadSave squad)
        {
            float sum = 0f;
            if (squad == null || squad.stoneIds == null || stoneCatalog == null) return 1f;
            foreach (var id in squad.stoneIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                var stone = stoneCatalog.ById(id);
                if (stone != null && stone.effect.kind == CardBuffKind.CostRate) sum += stone.effect.percent;
            }
            return 1f + sum / 100f;
        }

        private void OnDisable()
        {
            if (battleBridge != null) battleBridge.StopBattle();
            if (logger != null) logger.EndSession();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
