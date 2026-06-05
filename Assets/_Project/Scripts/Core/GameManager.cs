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

        // Start runs after all Awake/OnEnable of peers, so DraftView has subscribed
        // to DraftController events before we emit DraftStarted.
        private void Start()
        {
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
                if (battleBridge != null) battleBridge.PrepareDraftMap();

                SetPhase(GamePhase.Draft);
                draftController.BeginDraft();
            }
            else if (battleBridge != null)
            {
                Debug.LogWarning("[GameManager] draftController unset; starting battle with inspector defenderPool fallback.");
                SetPhase(GamePhase.Battle);
                battleBridge.StartBattle();
            }
        }

        // squad-loadout Unit 3 — skip the draft and bring the selected squad
        // straight into placement. Deterministic: exactly the saved squad units
        // (rev 2026-06-05 — no random fill).
        private void StartSquadMatch(SquadSave squad)
        {
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

            // Skills stay independent of units — roll a fresh loadout like draft does.
            if (skillLoadout != null)
            {
                skillLoadout.Roll();
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
