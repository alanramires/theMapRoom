using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controlador do painel Panel_newGame na cena Tela de Entrada.
/// Hierarquia esperada:
///   divisao_playerConfig / linha_J1..4 com Toggle_H, Toggle_I (e Toggle_O para J3/J4)
///   divisao_regras / linha_regras (TMP_Dropdown), descricao_regras (TMP_Text), Toggle (cmd auto)
///   divisao_botoes / Button_voltar, Button_iniciar
/// </summary>
public class NewGamePanelController : MonoBehaviour
{
    private const string Scene2Players = "Battle Map";
    private const string Scene3Players = "Triple Trouble";
    private const string Scene4Players = "Team Island";

    private enum PlayerMode { Humano, IA, Off }

    // Times fixos por slot
    private static readonly TeamId[] SlotTeams =
    {
        TeamId.Green,
        TeamId.Red,
        TeamId.Blue,
        TeamId.Yellow
    };

    // ── Jogadores ─────────────────────────────────────────────────────────
    [Header("J1")]
    [SerializeField] private Toggle toggleH1;
    [SerializeField] private Toggle toggleI1;

    [Header("J2")]
    [SerializeField] private Toggle toggleH2;
    [SerializeField] private Toggle toggleI2;

    [Header("J3")]
    [SerializeField] private Toggle toggleH3;
    [SerializeField] private Toggle toggleI3;
    [SerializeField] private Toggle toggleO3;

    [Header("J4")]
    [SerializeField] private Toggle toggleH4;
    [SerializeField] private Toggle toggleI4;
    [SerializeField] private Toggle toggleO4;

    // ── Regras ────────────────────────────────────────────────────────────
    [Header("Regras")]
    [SerializeField] private TMP_Dropdown dropPreset;
    [SerializeField] private TMP_Text descricaoRegras;
    [SerializeField] private Toggle toggleCmdAuto;

    // ── Botões ────────────────────────────────────────────────────────────
    [Header("Botões")]
    [SerializeField] private Button btnVoltar;
    [SerializeField] private Button btnIniciar;

    // ── Prancheta (cena Tela de Entrada) ──────────────────────────────────
    [Header("Prancheta")]
    [SerializeField] private MatchController draftMatchController;
    [SerializeField] private SaveGameManager draftSaveGameManager;

    // ── Estado runtime ────────────────────────────────────────────────────
    private PlayerMode[] modes = new PlayerMode[4];
    private bool suppressToggleCallbacks;
    private CursorController cursorController;
    private GameObject lastSelectedObject;
    private int lastConfirmSfxFrame = -1;

    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        PopulatePresetDropdown();
        WireToggleCallbacks();
        ConfigureNavigation();
        btnVoltar?.onClick.AddListener(OnVoltarClicked);
        btnIniciar?.onClick.AddListener(OnIniciarClicked);
        dropPreset?.onValueChanged.AddListener(_ => { RefreshDescricaoRegras(); RefreshDraftMatchController(); });
    }

    private void OnEnable()
    {
        if (cursorController == null)
            cursorController = FindAnyObjectByType<CursorController>();

        ApplyInitialModes();
        RefreshDescricaoRegras();
        RefreshDraftMatchController();
        // foca o primeiro elemento para navegação por teclado funcionar imediatamente
        GameObject first = toggleH1 != null ? toggleH1.gameObject : null;
        EventSystem.current?.SetSelectedGameObject(first);
        lastSelectedObject = first;
    }

    private void Update()
    {
        if (!gameObject.activeInHierarchy)
            return;

        // Som de cursor quando a seleção muda (setas ou WASD) — suprimido se confirm tocou neste frame
        GameObject current = EventSystem.current?.currentSelectedGameObject;
        if (current != lastSelectedObject)
        {
            if (current != null && lastConfirmSfxFrame != Time.frameCount)
                cursorController?.PlayCursorMoveSfx();
            lastSelectedObject = current;
        }

        // Som de confirm ao pressionar Enter / Space
        if (WasSubmitPressed() && current != null)
            PlayConfirmSfx();
    }

    private void PlayConfirmSfx()
    {
        lastConfirmSfxFrame = Time.frameCount;
        cursorController?.PlayConfirmSfx();
    }

    private static bool WasSubmitPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.enterKey.wasPressedThisFrame
                || Keyboard.current.numpadEnterKey.wasPressedThisFrame
                || Keyboard.current.spaceKey.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetKeyDown(KeyCode.Space);
    }

    // ── Navegação por teclado ─────────────────────────────────────────────

    // cor de highlight quando o elemento está selecionado pelo teclado
    private static readonly Color SelectedHighlightColor = new Color(0.290f, 0.353f, 0.263f); // #4A5A43

    private void ConfigureNavigation()
    {
        ApplySelectionHighlight(
            toggleH1, toggleI1,
            toggleH2, toggleI2,
            toggleH3, toggleI3, toggleO3,
            toggleH4, toggleI4, toggleO4,
            dropPreset, toggleCmdAuto,
            btnVoltar, btnIniciar);
        // Ordem de navegação vertical:
        // toggleH1 ↔ toggleI1
        // toggleH2 ↔ toggleI2
        // toggleH3 ↔ toggleI3 ↔ toggleO3
        // toggleH4 ↔ toggleI4 ↔ toggleO4
        // dropPreset
        // toggleCmdAuto
        // btnVoltar ↔ btnIniciar

        // linha J1: H ←→ I, desce para H2
        LinkHorizontal(toggleH1, toggleI1);
        LinkVertical(toggleH1, toggleI1, toggleH2, toggleI2);

        // linha J2: H ←→ I, desce para H3
        LinkHorizontal(toggleH2, toggleI2);
        LinkVertical(toggleH2, toggleI2, toggleH3, toggleI3);

        // linha J3: H ←→ I ←→ O, desce para H4
        LinkHorizontalChain(toggleH3, toggleI3, toggleO3);
        LinkVertical(toggleH3, toggleO3, toggleH4, toggleO4);
        SetNav(toggleO3, up: toggleI2);    // O3 sobe para I2 (ida só)
        SetNav(toggleI3, down: toggleI4); // meio da cadeia
        SetNav(toggleI4, up: toggleI3);   // meio da cadeia

        // linha J4: H ←→ I ←→ O, desce para dropdown
        LinkHorizontalChain(toggleH4, toggleI4, toggleO4);
        LinkVerticalSingle(toggleH4, toggleO4, dropPreset, dropPreset);
        SetNav(toggleI4, down: dropPreset); // meio desce para dropdown

        // dropdown: desce para toggleCmdAuto
        LinkVerticalSingle(dropPreset, dropPreset, toggleCmdAuto, toggleCmdAuto);

        // toggleCmdAuto: desce para btnVoltar
        LinkVerticalSingle(toggleCmdAuto, toggleCmdAuto, btnVoltar, btnIniciar);

        // botões: ←→ entre si
        LinkHorizontal(btnVoltar, btnIniciar);
        SetNav(btnVoltar,  up: toggleCmdAuto);
        SetNav(btnIniciar, up: toggleCmdAuto);
    }

    // Liga dois Selectables horizontalmente (esquerda/direita)
    private static void LinkHorizontal(Selectable a, Selectable b)
    {
        if (a != null) SetNav(a, right: b);
        if (b != null) SetNav(b, left: a);
    }

    // Liga três Selectables em cadeia horizontal
    private static void LinkHorizontalChain(Selectable a, Selectable b, Selectable c)
    {
        if (a != null) SetNav(a, right: b);
        if (b != null) { SetNav(b, left: a, right: c); }
        if (c != null) SetNav(c, left: b);
    }

    // Conecta vertical entre dois grupos (leftA↔leftB para cima/baixo, rightA↔rightB idem)
    private static void LinkVertical(Selectable leftA, Selectable rightA, Selectable leftB, Selectable rightB)
    {
        if (leftA  != null) SetNav(leftA,  down: leftB);
        if (rightA != null) SetNav(rightA, down: rightB ?? leftB);
        if (leftB  != null) SetNav(leftB,  up: leftA);
        if (rightB != null) SetNav(rightB, up: rightA ?? leftA);
    }

    // Conecta vertical de um grupo para um único elemento abaixo/acima
    private static void LinkVerticalSingle(Selectable groupLeft, Selectable groupRight, Selectable below, Selectable belowRight)
    {
        if (groupLeft  != null) SetNav(groupLeft,  down: below);
        if (groupRight != null) SetNav(groupRight, down: belowRight ?? below);
        if (below      != null) SetNav(below,      up: groupLeft);
        if (belowRight != null && belowRight != below) SetNav(belowRight, up: groupRight ?? groupLeft);
    }

    private static void ApplySelectionHighlight(params Selectable[] selectables)
    {
        foreach (Selectable s in selectables)
        {
            if (s == null) continue;
            ColorBlock cb = s.colors;
            cb.selectedColor = SelectedHighlightColor;
            s.colors = cb;
        }
    }

    private static void SetNav(Selectable s,
        Selectable up    = null,
        Selectable down  = null,
        Selectable left  = null,
        Selectable right = null)
    {
        if (s == null) return;
        Navigation nav = s.navigation;
        nav.mode = Navigation.Mode.Explicit;
        if (up    != null) nav.selectOnUp    = up;
        if (down  != null) nav.selectOnDown  = down;
        if (left  != null) nav.selectOnLeft  = left;
        if (right != null) nav.selectOnRight = right;
        s.navigation = nav;
    }

    // ── Estado inicial ────────────────────────────────────────────────────

    private void ApplyInitialModes()
    {
        // J1 = Humano, J2 = IA, J3 = Off, J4 = Off
        SetMode(0, PlayerMode.Humano);
        SetMode(1, PlayerMode.IA);
        SetMode(2, PlayerMode.Off);
        SetMode(3, PlayerMode.Off);
    }

    // ── Toggles ───────────────────────────────────────────────────────────

    private void WireToggleCallbacks()
    {
        toggleH1?.onValueChanged.AddListener(on => { if (on) SetMode(0, PlayerMode.Humano); });
        toggleI1?.onValueChanged.AddListener(on => { if (on) SetMode(0, PlayerMode.IA); });

        toggleH2?.onValueChanged.AddListener(on => { if (on) SetMode(1, PlayerMode.Humano); });
        toggleI2?.onValueChanged.AddListener(on => { if (on) SetMode(1, PlayerMode.IA); });

        toggleH3?.onValueChanged.AddListener(on => { if (on) SetMode(2, PlayerMode.Humano); });
        toggleI3?.onValueChanged.AddListener(on => { if (on) SetMode(2, PlayerMode.IA); });
        toggleO3?.onValueChanged.AddListener(on => { if (on) SetMode(2, PlayerMode.Off); });

        toggleH4?.onValueChanged.AddListener(on => { if (on) SetMode(3, PlayerMode.Humano); });
        toggleI4?.onValueChanged.AddListener(on => { if (on) SetMode(3, PlayerMode.IA); });
        toggleO4?.onValueChanged.AddListener(on => { if (on) SetMode(3, PlayerMode.Off); });
    }

    private void SetMode(int slot, PlayerMode mode)
    {
        if (suppressToggleCallbacks)
            return;

        modes[slot] = mode;
        suppressToggleCallbacks = true;

        switch (slot)
        {
            case 0:
                SetToggle(toggleH1, mode == PlayerMode.Humano);
                SetToggle(toggleI1, mode == PlayerMode.IA);
                break;
            case 1:
                SetToggle(toggleH2, mode == PlayerMode.Humano);
                SetToggle(toggleI2, mode == PlayerMode.IA);
                break;
            case 2:
                SetToggle(toggleH3, mode == PlayerMode.Humano);
                SetToggle(toggleI3, mode == PlayerMode.IA);
                SetToggle(toggleO3, mode == PlayerMode.Off);
                break;
            case 3:
                SetToggle(toggleH4, mode == PlayerMode.Humano);
                SetToggle(toggleI4, mode == PlayerMode.IA);
                SetToggle(toggleO4, mode == PlayerMode.Off);
                break;
        }

        suppressToggleCallbacks = false;
        RefreshDraftMatchController();
    }

    private static void SetToggle(Toggle t, bool value)
    {
        if (t != null && t.isOn != value)
            t.isOn = value;
    }

    // ── Preset dropdown ───────────────────────────────────────────────────

    private void PopulatePresetDropdown()
    {
        if (dropPreset == null)
            return;

        dropPreset.ClearOptions();
        dropPreset.AddOptions(new List<string>
        {
            "Game Boy Clássico",
            "Física Básica",
            "A Montanha Avacalha",
            "Neblina Leve",
            "Fog of War Total"
        });
        dropPreset.value = (int)MatchController.GameSetupPreset.FogOfWarTotal;
    }

    // ── Descrição de regras ───────────────────────────────────────────────

    private void RefreshDescricaoRegras()
    {
        if (descricaoRegras == null)
            return;

        MatchController.GameSetupPreset preset = dropPreset != null
            ? (MatchController.GameSetupPreset)dropPreset.value
            : MatchController.GameSetupPreset.FogOfWarTotal;

        descricaoRegras.text = BuildDescricao(preset);
    }

    private static string BuildDescricao(MatchController.GameSetupPreset preset)
    {
        bool ldt, los, spotter, stealth, fow;

        switch (preset)
        {
            case MatchController.GameSetupPreset.GameBoyClassic:
                ldt = false; los = false; spotter = false; stealth = false; fow = false;
                break;
            case MatchController.GameSetupPreset.FisicaBasica:
                ldt = true;  los = false; spotter = false; stealth = false; fow = false;
                break;
            case MatchController.GameSetupPreset.AMontanhaAvacalha:
                ldt = true;  los = true;  spotter = false; stealth = false; fow = false;
                break;
            case MatchController.GameSetupPreset.NeblinaLeve:
                ldt = true;  los = true;  spotter = true;  stealth = false; fow = false;
                break;
            default: // FogOfWarTotal
                ldt = true;  los = true;  spotter = true;  stealth = true;  fow = true;
                break;
        }

        List<string> on  = new List<string>();
        List<string> off = new List<string>();

        Classify(ldt,     "Linha de Tiro",  on, off);
        Classify(los,     "Linha de Visão", on, off);
        Classify(spotter, "Spotter",        on, off);
        Classify(stealth, "Stealth",        on, off);
        Classify(fow,     "Total War",      on, off);

        string onPart  = on.Count  > 0 ? string.Join(", ", on)  : "nenhum";
        string offPart = off.Count > 0 ? string.Join(", ", off) : "nenhum";

        return $"Ativado: {onPart} | Desativado: {offPart}";
    }

    private static void Classify(bool active, string label, List<string> on, List<string> off)
    {
        if (active) on.Add(label);
        else        off.Add(label);
    }

    // ── Iniciar ───────────────────────────────────────────────────────────

    private void OnIniciarClicked()
    {
        PlayConfirmSfx();
        List<TeamId> teams = new List<TeamId>();
        List<bool> isAI   = new List<bool>();
        List<bool> flipX  = new List<bool>();

        for (int i = 0; i < 4; i++)
        {
            if (modes[i] == PlayerMode.Off)
                continue;

            TeamId team = SlotTeams[i];
            teams.Add(team);
            isAI.Add(modes[i] == PlayerMode.IA);
            flipX.Add(team == TeamId.Red || team == TeamId.Yellow);
        }

        if (teams.Count < 2)
        {
            Debug.LogWarning("[NewGame] Minimo de 2 jogadores nao-OFF necessario para iniciar.");
            return;
        }

        MatchController.GameSetupPreset preset = dropPreset != null
            ? (MatchController.GameSetupPreset)dropPreset.value
            : MatchController.GameSetupPreset.FogOfWarTotal;

        bool cmdAuto = toggleCmdAuto != null && toggleCmdAuto.isOn;
        string target = SceneForPlayerCount(teams.Count);

        string saveDir = draftSaveGameManager != null
            ? draftSaveGameManager.GetResolvedSaveDirectory()
            : string.Empty;
        SaveGameManager.SetupForNewGame(saveDir);

        PartidaConfig.Set(teams.Count, teams.ToArray(), isAI.ToArray(), flipX.ToArray(), preset, cmdAuto, target);
        SceneManager.LoadScene(target);
    }

    // ── Prancheta ─────────────────────────────────────────────────────────

    private void RefreshDraftMatchController()
    {
        if (draftMatchController == null)
            return;

        List<int> teamIds  = new List<int>();
        List<bool> flipXs  = new List<bool>();
        List<int> zeros    = new List<int>();
        List<bool> falses  = new List<bool>();

        for (int i = 0; i < 4; i++)
        {
            if (modes[i] == PlayerMode.Off)
                continue;

            TeamId team = SlotTeams[i];
            teamIds.Add((int)team);
            flipXs.Add(team == TeamId.Red || team == TeamId.Yellow);
            zeros.Add(0);
            falses.Add(false);
        }

        draftMatchController.ImportPlayersState(teamIds, flipXs, zeros, zeros, zeros, falses, false);

        MatchController.GameSetupPreset preset = dropPreset != null
            ? (MatchController.GameSetupPreset)dropPreset.value
            : MatchController.GameSetupPreset.FogOfWarTotal;
        draftMatchController.SetGameSetupPreset(preset);

        draftMatchController.CommandServiceAutomatic = toggleCmdAuto != null && toggleCmdAuto.isOn;
    }

    private static string SceneForPlayerCount(int count)
    {
        switch (count)
        {
            case 3:  return Scene3Players;
            case 4:  return Scene4Players;
            default: return Scene2Players;
        }
    }

    private void OnVoltarClicked()
    {
        PlayConfirmSfx();
        MainMenuStateController stateController = FindInActiveScene<MainMenuStateController>();
        stateController?.RequestState(MainMenuState.RootMenu);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static T FindInActiveScene<T>() where T : Component
    {
        Scene active = SceneManager.GetActiveScene();
        T[] all = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].gameObject.scene == active)
                return all[i];
        }
        return null;
    }
}
