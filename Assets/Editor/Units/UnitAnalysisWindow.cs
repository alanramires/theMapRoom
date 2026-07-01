using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools/Units/Unit Analysis
/// Escolhe UM atacante e simula o disparo contra TODAS as unidades do banco,
/// pontuando cada matchup (dano causado, seguranca, troca de valor, TTK, veredito).
///
/// Fonte de verdade: usa o mesmo AICombatHpSimulator que a IA consulta em jogo.
/// Assim a scorecard e literalmente a previsao do que a IA vai calcular, e serve
/// tanto pro balanceamento quanto pra compor o manual.
/// </summary>
public class UnitAnalysisWindow : EditorWindow
{
    [SerializeField] private UnitDatabase unitDatabase;
    [SerializeField] private RPSDatabase rpsDatabase;
    [SerializeField] private DPQMatchupDatabase dpqMatchupDatabase;
    [SerializeField] private WeaponPriorityData weaponPriorityData;

    [SerializeField] private int attackerIndex;
    [SerializeField] private int distance = 1;
    [SerializeField] private DPQQualidadeDePosicao chosenDpq = DPQQualidadeDePosicao.Default;
    [SerializeField] private DPQQualidadeDePosicao opponentDpq = DPQQualidadeDePosicao.Default;
    [SerializeField] private bool attackerAtMaxHp = true;
    [SerializeField] private int attackerHpOverride = 10;
    [SerializeField] private bool includeUnreachable = false;
    [SerializeField] private bool includeSelf = false;
    [SerializeField] private bool sortByTradeValue = true;
    [SerializeField] private bool hideNoCounter = false;
    [SerializeField] private bool onlyNoCounter = false;
    [SerializeField] private bool showIncoming = false;
    [SerializeField] private bool showMatrix = false;
    [SerializeField] private WeaponSlot weaponSlot = WeaponSlot.Auto;
    [SerializeField] private string outputRelativePath = "docs/UNIT_ANALYSIS.csv";

    private readonly List<UnitData> units = new List<UnitData>();
    private readonly List<Row> rows = new List<Row>();
    private readonly List<IncomingRow> incomingRows = new List<IncomingRow>();
    private readonly List<MatrixCell> matrixCells = new List<MatrixCell>();
    private Vector2 scroll;
    private string status = "Selecione um atacante e clique em Analisar.";

    [MenuItem("Tools/Units/Unit Analysis")]
    private static void Open()
    {
        UnitAnalysisWindow window = GetWindow<UnitAnalysisWindow>("Unit Analysis");
        window.minSize = new Vector2(760f, 420f);
        window.AutoAssign();
        window.RefreshUnits();
    }

    private void OnEnable()
    {
        AutoAssign();
        RefreshUnits();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Unit Analysis — 1 atacante vs todos", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Usa o AICombatHpSimulator (mesma formula/fonte da IA). Dano causado no bucket: 0-2 fraco, 3-4 razoavel, 5-7 forte, 8-10 counter natural.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        unitDatabase = (UnitDatabase)EditorGUILayout.ObjectField("Unit Database", unitDatabase, typeof(UnitDatabase), false);
        if (EditorGUI.EndChangeCheck())
            RefreshUnits();

        rpsDatabase = (RPSDatabase)EditorGUILayout.ObjectField("RPS Database", rpsDatabase, typeof(RPSDatabase), false);
        dpqMatchupDatabase = (DPQMatchupDatabase)EditorGUILayout.ObjectField("DPQ Matchup DB", dpqMatchupDatabase, typeof(DPQMatchupDatabase), false);
        weaponPriorityData = (WeaponPriorityData)EditorGUILayout.ObjectField("Weapon Priority", weaponPriorityData, typeof(WeaponPriorityData), false);

        if (units.Count == 0)
        {
            EditorGUILayout.HelpBox("Unit Database vazio ou nao atribuido.", MessageType.Warning);
            return;
        }

        EditorGUILayout.Space(4f);
        string[] labels = new string[units.Count];
        for (int i = 0; i < units.Count; i++)
            labels[i] = GetLabel(units[i]);
        attackerIndex = Mathf.Clamp(attackerIndex, 0, units.Count - 1);
        attackerIndex = EditorGUILayout.Popup("Atacante", attackerIndex, labels);

        distance = Mathf.Max(1, EditorGUILayout.IntField("Distancia (hex)", distance));
        weaponSlot = (WeaponSlot)EditorGUILayout.EnumPopup("Arma do atacante", weaponSlot);

        EditorGUILayout.BeginHorizontal();
        chosenDpq = (DPQQualidadeDePosicao)EditorGUILayout.EnumPopup("DPQ da unidade", chosenDpq);
        opponentDpq = (DPQQualidadeDePosicao)EditorGUILayout.EnumPopup("DPQ do oponente", opponentDpq);
        EditorGUILayout.EndHorizontal();

        attackerAtMaxHp = EditorGUILayout.ToggleLeft("Atacante com HP maximo", attackerAtMaxHp);
        if (!attackerAtMaxHp)
            attackerHpOverride = Mathf.Clamp(EditorGUILayout.IntField("HP atacante", attackerHpOverride), 1, 10);

        includeUnreachable = EditorGUILayout.ToggleLeft("Listar alvos inalcancaveis", includeUnreachable);
        includeSelf = EditorGUILayout.ToggleLeft("Incluir a propria unidade como alvo", includeSelf);
        sortByTradeValue = EditorGUILayout.ToggleLeft("Ordenar por troca de valor (counters no topo)", sortByTradeValue);
        bool prevHide = hideNoCounter;
        hideNoCounter = EditorGUILayout.ToggleLeft("Ocultar alvos que nao revidam", hideNoCounter);
        if (hideNoCounter && !prevHide) onlyNoCounter = false;

        bool prevOnly = onlyNoCounter;
        onlyNoCounter = EditorGUILayout.ToggleLeft("Exibir apenas os que nao revidam (ex: artilharia)", onlyNoCounter);
        if (onlyNoCounter && !prevOnly) hideNoCounter = false;

        showIncoming = EditorGUILayout.ToggleLeft("Mostrar quem atira nesta unidade (e o revide dela)", showIncoming);

        showMatrix = EditorGUILayout.ToggleLeft("Matriz arma x classe (cobertura p/ Shopping)", showMatrix);

        EditorGUILayout.Space(4f);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Analisar", GUILayout.Height(28f)))
            Analyze();
        using (new EditorGUI.DisabledScope(rows.Count == 0 && matrixCells.Count == 0))
        {
            if (GUILayout.Button("Exportar CSV", GUILayout.Height(28f), GUILayout.Width(120f)))
                ExportCsv();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField(status, EditorStyles.miniLabel);

        bool hasMatrix = showMatrix && matrixCells.Count > 0;
        bool hasIncoming = showIncoming && incomingRows.Count > 0;
        if (rows.Count == 0 && !hasIncoming && !hasMatrix)
            return;

        EditorGUILayout.Space(6f);
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));

        if (hasMatrix)
        {
            EditorGUILayout.LabelField("Matriz arma x classe (cobertura):", EditorStyles.boldLabel);
            DrawMatrixHeader();
            for (int i = 0; i < matrixCells.Count; i++)
                DrawMatrixRow(matrixCells[i]);
        }
        else if (rows.Count > 0)
        {
            EditorGUILayout.LabelField("Esta unidade atira em:", EditorStyles.boldLabel);
            DrawHeader();
            for (int i = 0; i < rows.Count; i++)
                DrawRow(rows[i]);
        }

        if (hasIncoming)
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Quem atira nesta unidade (e o revide dela):", EditorStyles.boldLabel);
            DrawIncomingHeader();
            for (int i = 0; i < incomingRows.Count; i++)
                DrawIncomingRow(incomingRows[i]);
        }

        EditorGUILayout.EndScrollView();
    }

    // ---- Analise ----

    private void Analyze()
    {
        rows.Clear();
        if (unitDatabase == null || units.Count == 0)
        {
            status = "Sem unidades.";
            return;
        }

        UnitData attacker = units[Mathf.Clamp(attackerIndex, 0, units.Count - 1)];
        if (attacker == null)
        {
            status = "Atacante invalido.";
            return;
        }

        int chosenPoints = DPQData.GetPontosPadrao(chosenDpq);
        int oppPoints = DPQData.GetPontosPadrao(opponentDpq);
        int chosenDef = DPQData.GetDefesaPadrao(chosenDpq);
        int oppDef = DPQData.GetDefesaPadrao(opponentDpq);
        int atkHp = attackerAtMaxHp ? Mathf.Max(1, attacker.maxHP) : Mathf.Clamp(attackerHpOverride, 1, attacker.maxHP);

        for (int i = 0; i < units.Count; i++)
        {
            UnitData defender = units[i];
            if (defender == null)
                continue;
            if (!includeSelf && defender == attacker)
                continue;

            int defMaxHp = Mathf.Max(1, defender.maxHP);

            if (!TrySimulateOffensive(
                    attacker, defender, atkHp, defMaxHp,
                    chosenPoints, oppPoints, chosenDef, oppDef,
                    out AICombatHpSimulator.AICombatHpResult result, out WeaponCategory usedCategory))
            {
                if (includeUnreachable)
                    rows.Add(Row.Unreachable(GetLabel(defender)));
                continue;
            }

            Metrics m = ComputeMetrics(attacker, defender, atkHp, result);
            if (hideNoCounter && m.received == 0)
                continue;
            if (onlyNoCounter && m.received != 0)
                continue;

            rows.Add(new Row
            {
                target = GetLabel(defender),
                reachable = true,
                dealt = m.dealt,
                received = m.received,
                survives = m.survives,
                kill = m.kill,
                tradeScore = m.tradeScore,
                netValue = m.netValue,
                ttk = m.ttk,
                verdict = m.verdict,
                targetClass = defender.unitClass,
                weaponCategory = usedCategory
            });
        }

        if (sortByTradeValue)
            rows.Sort((a, b) =>
            {
                if (a.reachable != b.reachable) return a.reachable ? -1 : 1;
                int t = b.tradeScore.CompareTo(a.tradeScore);
                if (t != 0) return t;
                return b.dealt.CompareTo(a.dealt);
            });

        BuildIncoming(attacker, atkHp, chosenPoints, oppPoints, chosenDef, oppDef);

        if (showMatrix)
            BuildMatrix(attacker, atkHp, chosenPoints, oppPoints, chosenDef, oppDef);
        else
            matrixCells.Clear();

        status = $"{GetLabel(attacker)}: {rows.Count} ataques / {incomingRows.Count} incoming / {matrixCells.Count} celulas | dist={distance} | DPQ unidade={chosenDpq}/oponente={opponentDpq} | HP={atkHp}.";
    }

    // Visao defensiva: cada outra unidade atira na unidade escolhida (agora defensora),
    // e vemos quanto ela toma e quanto devolve no revide.
    private void BuildIncoming(UnitData chosen, int chosenHp, int chosenPoints, int oppPoints, int chosenDef, int oppDef)
    {
        incomingRows.Clear();
        if (chosen == null)
            return;

        for (int i = 0; i < units.Count; i++)
        {
            UnitData other = units[i];
            if (other == null)
                continue;
            if (!includeSelf && other == chosen)
                continue;

            int otherHp = Mathf.Max(1, other.maxHP);

            if (!TrySimulateIncoming(
                    other, chosen, otherHp, chosenHp,
                    oppPoints, chosenPoints, oppDef, chosenDef,
                    out AICombatHpSimulator.AICombatHpResult result))
            {
                if (includeUnreachable)
                    incomingRows.Add(IncomingRow.Unreachable(GetLabel(other)));
                continue;
            }

            int taken = Mathf.Clamp(chosenHp - result.defenderHpAfter, 0, chosenHp);
            int revide = Mathf.Clamp(otherHp - result.attackerHpAfter, 0, otherHp);
            bool chosenSurvives = !result.killGuaranteed;

            incomingRows.Add(new IncomingRow
            {
                attacker = GetLabel(other),
                reachable = true,
                taken = taken,
                revide = revide,
                survives = chosenSurvives,
                kill = result.killGuaranteed,
                verdict = IncomingVerdict(taken, revide, chosenSurvives)
            });
        }

        incomingRows.Sort((a, b) =>
        {
            if (a.reachable != b.reachable) return a.reachable ? -1 : 1;
            int t = b.taken.CompareTo(a.taken);
            if (t != 0) return t;
            return a.revide.CompareTo(b.revide);
        });
    }

    // ---- Selecao de arma / simulacao com override ----

    // Espelha o pick de arma do AICombatHpSimulator (mesmas chamadas de sensor).
    private WeaponData ResolveAutoAttackWeapon(UnitData attacker, UnitData defender)
    {
        if (attacker == null || defender == null || attacker.embarkedWeapons == null)
            return null;

        WeaponData fallback = null;
        for (int i = 0; i < attacker.embarkedWeapons.Count; i++)
        {
            if (!IsWeaponUsableAt(attacker.embarkedWeapons[i], defender, distance, out WeaponData weapon))
                continue;
            if (fallback == null)
                fallback = weapon;
            if (PodeMirarSensor.IsPreferredWeaponForTarget(weaponPriorityData, weapon, defender.unitClass))
                return weapon;
        }
        return fallback;
    }

    private WeaponCategory ResolveUsedWeaponCategory(UnitData attacker, UnitData defender)
    {
        WeaponData w = ResolveAutoAttackWeapon(attacker, defender);
        return w != null ? w.WeaponCategory : WeaponCategory.AntiInfantaria;
    }

    private bool IsWeaponUsableAt(UnitEmbarkedWeapon embarked, UnitData target, int dist, out WeaponData weapon)
    {
        weapon = null;
        if (embarked == null || embarked.weapon == null || target == null)
            return false;
        if (!PodeMirarSensor.TryResolveWeaponRangeCandidate(
                embarked, SensorMovementMode.MoveuParado, requireAmmo: false, out int minRange, out int maxRange))
            return false;
        if (dist < minRange || dist > maxRange)
            return false;
        if (!embarked.weapon.SupportsOperationOn(target.domain, target.heightLevel))
            return false;
        weapon = embarked.weapon;
        return true;
    }

    // Revide forcado: mesma regra do sensor (so a distancia 1, arma com alcance minimo 1, dominio compativel).
    private bool IsValidForcedCounter(UnitEmbarkedWeapon embarked, UnitData attacker, out WeaponData weapon)
    {
        weapon = null;
        if (distance != 1 || embarked == null || embarked.weapon == null || attacker == null)
            return false;
        if (!PodeMirarSensor.TryResolveWeaponRangeCandidate(
                embarked, SensorMovementMode.MoveuParado, requireAmmo: false, out int minRange, out _))
            return false;
        if (minRange != 1)
            return false;
        if (!embarked.weapon.SupportsOperationOn(attacker.domain, attacker.heightLevel))
            return false;
        weapon = embarked.weapon;
        return true;
    }

    private WeaponData ResolveAutoCounter(UnitData defender, UnitData attacker)
    {
        if (PodeMirarSensor.TryResolveCounterAttackFromData(
                defender, attacker, distance, weaponPriorityData, out WeaponData counter, out _, out _))
            return counter;
        return null;
    }

    private bool TryGetForcedWeapon(UnitData unit, out UnitEmbarkedWeapon embarked)
    {
        embarked = null;
        int idx = weaponSlot == WeaponSlot.Principal ? 0 : 1;
        if (unit == null || unit.embarkedWeapons == null || idx < 0 || idx >= unit.embarkedWeapons.Count)
            return false;
        embarked = unit.embarkedWeapons[idx];
        return embarked != null && embarked.weapon != null;
    }

    // Ofensiva: a unidade escolhida ataca. weaponSlot decide a arma dela.
    private bool TrySimulateOffensive(
        UnitData attacker, UnitData defender, int atkHp, int defHp,
        int atkPts, int defPts, int atkDefBonus, int defDefBonus,
        out AICombatHpSimulator.AICombatHpResult result, out WeaponCategory usedCategory)
    {
        result = AICombatHpSimulator.AICombatHpResult.Invalid;
        usedCategory = WeaponCategory.AntiInfantaria;

        if (weaponSlot == WeaponSlot.Auto)
        {
            result = AICombatHpSimulator.Simulate(
                attacker, defender, atkHp, defHp, distance,
                rpsDatabase, dpqMatchupDatabase, weaponPriorityData,
                atkPts, defPts, atkDefBonus, defDefBonus);
            if (result.isValid)
                usedCategory = ResolveUsedWeaponCategory(attacker, defender);
            return result.isValid;
        }

        if (!TryGetForcedWeapon(attacker, out UnitEmbarkedWeapon forcedEmbarked))
            return false;
        if (!IsWeaponUsableAt(forcedEmbarked, defender, distance, out WeaponData forced))
            return false;

        WeaponData counter = ResolveAutoCounter(defender, attacker);
        result = AICombatHpSimulator.SimulateWithWeapons(
            attacker, defender, forced, counter, atkHp, defHp,
            rpsDatabase, dpqMatchupDatabase,
            atkPts, defPts, atkDefBonus, defDefBonus, false, false);
        usedCategory = forced.WeaponCategory;
        return result.isValid;
    }

    // Defensiva: outra unidade ataca a escolhida; weaponSlot decide a arma de REVIDE da escolhida.
    private bool TrySimulateIncoming(
        UnitData other, UnitData chosen, int otherHp, int chosenHp,
        int otherPts, int chosenPts, int otherDefBonus, int chosenDefBonus,
        out AICombatHpSimulator.AICombatHpResult result)
    {
        result = AICombatHpSimulator.AICombatHpResult.Invalid;

        if (weaponSlot == WeaponSlot.Auto)
        {
            result = AICombatHpSimulator.Simulate(
                other, chosen, otherHp, chosenHp, distance,
                rpsDatabase, dpqMatchupDatabase, weaponPriorityData,
                otherPts, chosenPts, otherDefBonus, chosenDefBonus);
            return result.isValid;
        }

        WeaponData otherAttack = ResolveAutoAttackWeapon(other, chosen);
        if (otherAttack == null)
            return false;

        WeaponData chosenCounter = null;
        if (TryGetForcedWeapon(chosen, out UnitEmbarkedWeapon forcedEmbarked))
            IsValidForcedCounter(forcedEmbarked, other, out chosenCounter);

        result = AICombatHpSimulator.SimulateWithWeapons(
            other, chosen, otherAttack, chosenCounter, otherHp, chosenHp,
            rpsDatabase, dpqMatchupDatabase,
            otherPts, chosenPts, otherDefBonus, chosenDefBonus, false, false);
        return result.isValid;
    }

    // Metricas de um matchup ofensivo (custos ja aplicados corretamente aqui).
    private Metrics ComputeMetrics(UnitData attacker, UnitData defender, int atkHp, AICombatHpSimulator.AICombatHpResult result)
    {
        WeaponCategory category = ResolveUsedWeaponCategory(attacker, defender);
        UnitCounterEvaluator.Evaluation evaluation = UnitCounterEvaluator.FromSimulation(
            attacker, defender, atkHp, distance, category, result);

        return new Metrics
        {
            dealt = evaluation.Dealt,
            received = evaluation.Received,
            survives = evaluation.Survives,
            kill = evaluation.Kill,
            tradeScore = evaluation.TradeScore,
            netValue = evaluation.NetValue,
            ttk = evaluation.Ttk,
            verdict = evaluation.Verdict
        };
    }

    // Matriz conjunta arma-usada x classe-do-alvo.
    // Cobertura = soma das notas (0..1) dos alvos alcancaveis daquela classe/arma,
    // dividida pelo TOTAL de unidades da classe (inalcancaveis contam como zero).
    private void BuildMatrix(UnitData attacker, int atkHp, int chosenPts, int oppPts, int chosenDef, int oppDef)
    {
        matrixCells.Clear();

        // Denominador: tamanho de cada classe no roster (inalcancaveis incluidos).
        Dictionary<GameUnitClass, int> classSize = new Dictionary<GameUnitClass, int>();
        for (int i = 0; i < units.Count; i++)
        {
            UnitData u = units[i];
            if (u == null || (!includeSelf && u == attacker))
                continue;
            classSize.TryGetValue(u.unitClass, out int c);
            classSize[u.unitClass] = c + 1;
        }

        Dictionary<string, MatrixAccum> map = new Dictionary<string, MatrixAccum>();
        List<string> order = new List<string>();
        HashSet<GameUnitClass> classesWithReach = new HashSet<GameUnitClass>();

        for (int i = 0; i < units.Count; i++)
        {
            UnitData defender = units[i];
            if (defender == null || (!includeSelf && defender == attacker))
                continue;

            int defHp = Mathf.Max(1, defender.maxHP);
            if (!TrySimulateOffensive(attacker, defender, atkHp, defHp,
                    chosenPts, oppPts, chosenDef, oppDef,
                    out AICombatHpSimulator.AICombatHpResult result, out WeaponCategory usedCategory))
                continue; // inalcancavel: nao entra no numerador, mas conta no classSize (=zero)

            Metrics m = ComputeMetrics(attacker, defender, atkHp, result);
            float note = NoteFromVerdict(m.verdict);
            classesWithReach.Add(defender.unitClass);

            string key = usedCategory + "|" + defender.unitClass;
            if (!map.TryGetValue(key, out MatrixAccum acc))
            {
                acc = new MatrixAccum { weapon = usedCategory.ToString(), cls = defender.unitClass };
                order.Add(key);
            }
            acc.reach++;
            acc.sumNote += note;
            if (!m.survives) acc.deaths++;
            map[key] = acc;
        }

        for (int i = 0; i < order.Count; i++)
        {
            MatrixAccum acc = map[order[i]];
            int size = classSize.TryGetValue(acc.cls, out int s) ? Mathf.Max(1, s) : 1;
            matrixCells.Add(new MatrixCell
            {
                weapon = acc.weapon,
                cls = acc.cls.ToString(),
                coverage = acc.sumNote / size,
                reach = acc.reach,
                classSize = size,
                deaths = acc.deaths
            });
        }

        // Classes 100% inalcancaveis: cobertura 0 explicita (importante pro Shopping).
        foreach (KeyValuePair<GameUnitClass, int> kv in classSize)
        {
            if (classesWithReach.Contains(kv.Key))
                continue;
            matrixCells.Add(new MatrixCell
            {
                weapon = "—",
                cls = kv.Key.ToString(),
                coverage = 0f,
                reach = 0,
                classSize = kv.Value,
                deaths = 0
            });
        }

        matrixCells.Sort((a, b) => b.coverage.CompareTo(a.coverage));
    }

    // ---- Scoring ----

    private static int DamageStars(int d)
    {
        if (d <= 0) return 0;
        if (d <= 2) return 1;   // fraco
        if (d <= 4) return 2;   // razoavel
        if (d <= 7) return 4;   // forte
        return 5;               // counter natural
    }

    private static int SafetyStars(int received)
    {
        if (received <= 0) return 5;
        if (received <= 2) return 4;
        if (received <= 4) return 3;
        if (received <= 6) return 2;
        if (received <= 8) return 1;
        return 0;
    }

    private static int TradeScore(float valueDestroyed, float valueLost)
        => UnitCounterEvaluator.TradeScore(valueDestroyed, valueLost);

    private static string Verdict(int dealt, int received, bool survives, int tradeScore, float netValue)
        => UnitCounterEvaluator.Verdict(dealt, received, survives, tradeScore, netValue);

    private static string IncomingVerdict(int taken, int revide, bool survives)
    {
        if (!survives) return "MORRE";
        if (revide <= 0) return "Sem revide";   // toma dano sem poder revidar (indireto/fora de alcance)
        if (revide > taken) return "Devolve+";
        if (revide == taken) return "Troca";
        return "Apanha";
    }

    // Nota estavel por matchup (base da cobertura do grupo).
    private static float NoteFromVerdict(string verdict)
        => UnitCounterEvaluator.NoteFromVerdict(verdict);

    private static string Stars(int litCount)
    {
        int lit = Mathf.Clamp(litCount, 0, 5);
        StringBuilder sb = new StringBuilder(5);
        for (int i = 0; i < 5; i++)
            sb.Append(i < lit ? '★' : '☆');
        return sb.ToString();
    }

    // ---- Desenho ----

    private static void DrawHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Alvo", EditorStyles.miniBoldLabel, GUILayout.Width(180f));
        GUILayout.Label("Dano", EditorStyles.miniBoldLabel, GUILayout.Width(110f));
        GUILayout.Label("Recebe", EditorStyles.miniBoldLabel, GUILayout.Width(110f));
        GUILayout.Label("Troca $", EditorStyles.miniBoldLabel, GUILayout.Width(90f));
        GUILayout.Label("TTK", EditorStyles.miniBoldLabel, GUILayout.Width(44f));
        GUILayout.Label("Vive", EditorStyles.miniBoldLabel, GUILayout.Width(40f));
        GUILayout.Label("Veredito", EditorStyles.miniBoldLabel, GUILayout.Width(130f));
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawRow(Row row)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(row.target, GUILayout.Width(180f));

        if (!row.reachable)
        {
            GUILayout.Label("— nao alcanca", EditorStyles.miniLabel, GUILayout.Width(514f));
            EditorGUILayout.EndHorizontal();
            return;
        }

        GUILayout.Label($"{Stars(DamageStars(row.dealt))} {row.dealt}", GUILayout.Width(110f));
        GUILayout.Label($"{Stars(SafetyStars(row.received))} {row.received}", GUILayout.Width(110f));
        GUILayout.Label($"{(row.tradeScore >= 0 ? "+" : "")}{row.tradeScore} ({row.netValue})", GUILayout.Width(90f));
        GUILayout.Label(row.ttk > 0 ? row.ttk.ToString() : "-", GUILayout.Width(44f));
        GUILayout.Label(row.survives ? "sim" : "NAO", GUILayout.Width(40f));
        GUILayout.Label(row.verdict, GUILayout.Width(130f));
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawMatrixHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Arma", EditorStyles.miniBoldLabel, GUILayout.Width(150f));
        GUILayout.Label("Classe", EditorStyles.miniBoldLabel, GUILayout.Width(130f));
        GUILayout.Label("Cobertura", EditorStyles.miniBoldLabel, GUILayout.Width(90f));
        GUILayout.Label("Alcance", EditorStyles.miniBoldLabel, GUILayout.Width(70f));
        GUILayout.Label("Mortes", EditorStyles.miniBoldLabel, GUILayout.Width(60f));
        GUILayout.Label("Nota", EditorStyles.miniBoldLabel, GUILayout.Width(120f));
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawMatrixRow(MatrixCell c)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(c.weapon, GUILayout.Width(150f));
        GUILayout.Label(c.cls, GUILayout.Width(130f));
        GUILayout.Label(c.coverage.ToString("0.00"), GUILayout.Width(90f));
        GUILayout.Label($"{c.reach}/{c.classSize}", GUILayout.Width(70f));
        GUILayout.Label(c.deaths > 0 ? c.deaths.ToString() : "-", GUILayout.Width(60f));
        GUILayout.Label(CoverageLabel(c.coverage), GUILayout.Width(120f));
        EditorGUILayout.EndHorizontal();
    }

    private static string CoverageLabel(float coverage)
    {
        if (coverage >= 0.90f) return "counter natural";
        if (coverage >= 0.70f) return "counter economico";
        if (coverage >= 0.55f) return "forte";
        if (coverage >= 0.35f) return "parcial/troca";
        if (coverage >= 0.15f) return "neutro";
        return "desvantagem/gap";
    }

    private static void DrawIncomingHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Atacante", EditorStyles.miniBoldLabel, GUILayout.Width(180f));
        GUILayout.Label("Toma", EditorStyles.miniBoldLabel, GUILayout.Width(110f));
        GUILayout.Label("Revide", EditorStyles.miniBoldLabel, GUILayout.Width(110f));
        GUILayout.Label("Vive", EditorStyles.miniBoldLabel, GUILayout.Width(40f));
        GUILayout.Label("Veredito", EditorStyles.miniBoldLabel, GUILayout.Width(120f));
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawIncomingRow(IncomingRow row)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(row.attacker, GUILayout.Width(180f));

        if (!row.reachable)
        {
            GUILayout.Label("— nao alcanca", EditorStyles.miniLabel, GUILayout.Width(380f));
            EditorGUILayout.EndHorizontal();
            return;
        }

        GUILayout.Label($"{Stars(SafetyStars(row.taken))} {row.taken}", GUILayout.Width(110f));
        GUILayout.Label($"{Stars(DamageStars(row.revide))} {row.revide}", GUILayout.Width(110f));
        GUILayout.Label(row.survives ? "sim" : "NAO", GUILayout.Width(40f));
        GUILayout.Label(row.verdict, GUILayout.Width(120f));
        EditorGUILayout.EndHorizontal();
    }

    // ---- CSV ----

    private void ExportCsv()
    {
        StringBuilder csv = new StringBuilder(4096);
        UnitData attacker = units[Mathf.Clamp(attackerIndex, 0, units.Count - 1)];
        string atkLabel = GetLabel(attacker);

        if (showMatrix && matrixCells.Count > 0)
        {
            csv.AppendLine("Atacante;Arma;Classe;Cobertura;Alcance;ClassSize;Mortes;Nota");
            for (int i = 0; i < matrixCells.Count; i++)
            {
                MatrixCell c = matrixCells[i];
                csv.AppendLine($"{atkLabel};{c.weapon};{c.cls};{c.coverage.ToString("0.00")};{c.reach};{c.classSize};{c.deaths};{CoverageLabel(c.coverage)}");
            }
        }
        else
        {
            csv.AppendLine("Atacante;Alvo;DanoCausado;DanoRecebido;TrocaValorScore;NetValor;TTK;Vive;Kill;Veredito");
            for (int i = 0; i < rows.Count; i++)
            {
                Row r = rows[i];
                if (!r.reachable)
                {
                    csv.AppendLine($"{atkLabel};{r.target};;;;;;;;nao alcanca");
                    continue;
                }
                csv.AppendLine($"{atkLabel};{r.target};{r.dealt};{r.received};{r.tradeScore};{r.netValue};{r.ttk};{(r.survives ? "sim" : "nao")};{(r.kill ? "sim" : "nao")};{r.verdict}");
            }
        }

        string abs = ResolveAbsolutePath(outputRelativePath);
        string dir = Path.GetDirectoryName(abs);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(abs, csv.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();
        status = $"CSV exportado: {abs}";
        Debug.Log($"[UnitAnalysis] {status}");
    }

    private static string ResolveAbsolutePath(string relative)
    {
        string text = string.IsNullOrWhiteSpace(relative) ? "docs/UNIT_ANALYSIS.csv" : relative.Trim();
        if (Path.IsPathRooted(text))
            return text;
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.Combine(root, text.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));
    }

    // ---- Infra ----

    private void RefreshUnits()
    {
        units.Clear();
        if (unitDatabase == null || unitDatabase.Units == null)
            return;

        HashSet<UnitData> seen = new HashSet<UnitData>();
        for (int i = 0; i < unitDatabase.Units.Count; i++)
        {
            UnitData u = unitDatabase.Units[i];
            if (u == null || string.IsNullOrWhiteSpace(u.id) || !seen.Add(u))
                continue;
            units.Add(u);
        }
    }

    private void AutoAssign()
    {
        if (unitDatabase == null) unitDatabase = FindFirst<UnitDatabase>();
        if (rpsDatabase == null) rpsDatabase = FindFirst<RPSDatabase>();
        if (dpqMatchupDatabase == null) dpqMatchupDatabase = FindFirst<DPQMatchupDatabase>();
        if (weaponPriorityData == null) weaponPriorityData = FindFirst<WeaponPriorityData>();
    }

    private static T FindFirst<T>() where T : Object
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        if (guids == null || guids.Length == 0)
            return null;
        return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private static string GetLabel(UnitData unit)
    {
        if (unit == null) return "(null)";
        if (!string.IsNullOrWhiteSpace(unit.displayName)) return unit.displayName.Trim();
        if (!string.IsNullOrWhiteSpace(unit.apelido)) return unit.apelido.Trim();
        return !string.IsNullOrWhiteSpace(unit.id) ? unit.id.Trim() : unit.name;
    }

    private struct Row
    {
        public string target;
        public bool reachable;
        public int dealt;
        public int received;
        public bool survives;
        public bool kill;
        public int tradeScore;
        public int netValue;
        public int ttk;
        public string verdict;
        public GameUnitClass targetClass;
        public WeaponCategory weaponCategory;

        public static Row Unreachable(string target)
        {
            return new Row { target = target, reachable = false, verdict = "nao alcanca" };
        }
    }

    private struct IncomingRow
    {
        public string attacker;
        public bool reachable;
        public int taken;
        public int revide;
        public bool survives;
        public bool kill;
        public string verdict;

        public static IncomingRow Unreachable(string attacker)
        {
            return new IncomingRow { attacker = attacker, reachable = false, verdict = "nao alcanca" };
        }
    }

    private struct Metrics
    {
        public int dealt;
        public int received;
        public bool survives;
        public bool kill;
        public int tradeScore;
        public int netValue;
        public int ttk;
        public string verdict;
    }

    private struct MatrixCell
    {
        public string weapon;
        public string cls;
        public float coverage;
        public int reach;
        public int classSize;
        public int deaths;
    }

    private struct MatrixAccum
    {
        public string weapon;
        public GameUnitClass cls;
        public int reach;
        public int deaths;
        public float sumNote;
    }

    private enum WeaponSlot
    {
        Auto = 0,       // deixa a Weapon Priority escolher (comportamento padrao)
        Principal = 1,  // forca embarkedWeapons[0]
        Secundaria = 2  // forca embarkedWeapons[1]
    }
}
