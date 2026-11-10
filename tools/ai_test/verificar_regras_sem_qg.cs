// Execute via unity command script eval --file tools/ai_test/verificar_regras_sem_qg.cs (fora do Play).
if (UnityEngine.Application.isPlaying) throw new System.Exception("Execute fora do Play.");
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var statics = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
void Set(object o, string name, object value) => o.GetType().GetField(name, flags).SetValue(o, value);
object Get(object o, string name) => o.GetType().GetField(name, flags).GetValue(o);
object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, flags).Invoke(o, args);
void Check(bool ok, string message) { if (!ok) throw new System.Exception(message); }
var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Additive);
var oldBuildings = new System.Collections.Generic.List<ConstructionManager>(ConstructionManager.AllActive);
var oldUnits = new System.Collections.Generic.List<UnitManager>(UnitManager.AllActive);
var defeatedEvent = typeof(MatchController).GetField("OnTeamDefeated", statics);
var concludedEvent = typeof(MatchController).GetField("OnMatchConcluded", statics);
var oldDefeated = defeatedEvent.GetValue(null);
var oldConcluded = concludedEvent.GetValue(null);
var assets = new System.Collections.Generic.List<UnityEngine.Object>();
try
{
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
    ConstructionManager.AllActive.Clear(); UnitManager.AllActive.Clear();
    // Nao notifica os consumidores reais de campanha/audio durante o fixture.
    defeatedEvent.SetValue(null, null); concludedEvent.SetValue(null, null);
    var host = new UnityEngine.GameObject("FixtureMatch"); host.SetActive(false);
    var match = host.AddComponent<MatchController>();
    var entryType = typeof(MatchController).GetNestedType("PlayerEntry", System.Reflection.BindingFlags.NonPublic);
    var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(entryType);
    var entries = (System.Collections.IList)System.Activator.CreateInstance(listType);
    foreach (var team in new[] { TeamId.Blue, TeamId.Yellow, TeamId.Green })
    {
        var entry = System.Activator.CreateInstance(entryType);
        entryType.GetField("teamId").SetValue(entry, team);
        entries.Add(entry);
    }
    Set(match, "players", entries); Set(match, "currentTurn", 2);
    Set(match, "allowDefeatForZeroUnits", true); Set(match, "allowDefeatForHeadQuarterCapture", true);
    bool Defeated(int slot) => (bool)entryType.GetField("defeated").GetValue(entries[slot]);
    void ResetPlayers()
    {
        for (int i = 0; i < entries.Count; i++) { var e = entries[i]; entryType.GetField("defeated").SetValue(e, false); entries[i] = e; }
        Set(match, "hasVictoryWinner", false);
    }
    var offered = UnityEngine.ScriptableObject.CreateInstance<UnitData>(); assets.Add(offered);
    ConstructionManager Building(int slot, bool hq)
    {
        var go = new UnityEngine.GameObject("FixtureBuilding"); go.SetActive(false);
        var b = go.AddComponent<ConstructionManager>();
        Set(b, "matchController", match); Set(b, "slotIndex", slot);
        Set(b, "teamId", slot < 0 ? TeamId.Neutral : match.GetTeamIdForSlot(slot));
        Set(b, "originalOwnerInitialized", true); Set(b, "originalOwnerSlotIndex", slot);
        Set(b, "firstOwnerInitialized", false); Set(b, "firstOwnerSlotIndex", -1);
        Set(b, "siteRuntime", new ConstructionSiteRuntime { isPlayerHeadQuarter = hq, isCapturable = true, capturePointsMax = 20, sellingRule = ConstructionUnitMarketRule.FreeMarket, offeredUnits = new System.Collections.Generic.List<UnitData> { offered } });
        go.SetActive(true);
        if (!ConstructionManager.AllActive.Contains(b)) ConstructionManager.AllActive.Add(b);
        return b;
    }
    var enemyHq = Building(1, true); var thirdHq = Building(2, true); var neutral = Building(-1, false);
    Check(ConstructionManager.IsHeadQuarterlessTeam(TeamId.Blue), "Slot 0 deveria iniciar sem QG.");
    neutral.SetOwnerSlot(0);
    Check(!neutral.HasFirstOwner, "Ocupacao sem QG gravou FirstOwner.");
    Check(neutral.CanProduceUnitsForSlot(0), "FreeMarket bloqueou faccao sem QG.");
    var market = (ConstructionSiteRuntime)Get(neutral, "siteRuntime");
    market.sellingRule = ConstructionUnitMarketRule.FirstOwner;
    Check(!neutral.CanProduceUnitsForSlot(0), "FirstOwner liberou ocupante sem dono.");
    market.sellingRule = ConstructionUnitMarketRule.OriginalOwner;
    Check(!neutral.CanProduceUnitsForSlot(0), "OriginalOwner liberou ocupante de predio neutro.");
    market.sellingRule = ConstructionUnitMarketRule.Disabled;
    Check(!neutral.CanProduceUnitsForSlot(0), "Disabled liberou compra.");
    market.sellingRule = ConstructionUnitMarketRule.FreeMarket;
    Check(!neutral.CanProduceUnitsForSlot(1), "Compra passou para outro slot.");
    neutral.SetOwnerSlot(-1); neutral.SetTeamId(TeamId.Blue);
    Check(!neutral.HasFirstOwner, "SetTeamId gravou FirstOwner sem QG.");
    neutral.SetOwnerSlot(2);
    Check(neutral.HasFirstOwner && neutral.FirstOwnerSlotIndex == 2, "Primeiro dono com QG nao foi gravado.");
    enemyHq.SetOwnerSlot(0);
    Call(match, "ResolveHeadQuarterCapture", enemyHq, 1, 0, TeamId.Yellow, TeamId.Blue);
    Check(Defeated(1) && !Defeated(0) && !Defeated(2), "Captura nao eliminou apenas o dono do QG.");
    Check(!(bool)Get(match, "hasVictoryWinner"), "Captura encerrou partida com dois sobreviventes.");
    Check(!ConstructionManager.IsHeadQuarterlessTeam(TeamId.Blue), "Captura nao promoveu por derivacao.");
    Check(enemyHq.CanProduceUnitsForSlot(0), "FreeMarket falhou depois da promocao.");
    ResetPlayers(); enemyHq.SetOwnerSlot(1);
    neutral.SetOwnerSlot(0);
    Check((bool)Call(match, "TryDefeatSlotIfZeroUnits", PlayerSlotId.FromIndex(0)), "Slot sem QG e sem unidades nao foi derrotado.");
    Check(Defeated(0) && neutral.SlotIndex == -1, "Eliminacao nao neutralizou construcoes.");
    ResetPlayers(); Set(match, "currentTurn", 1);
    Check(!(bool)Call(match, "TryDefeatSlotIfZeroUnits", PlayerSlotId.FromIndex(0)), "Primeiro turno nao protegido.");
    Set(match, "currentTurn", 2); Set(match, "allowDefeatForZeroUnits", false);
    Check(!(bool)Call(match, "TryDefeatSlotIfZeroUnits", PlayerSlotId.FromIndex(0)), "Regra de cenario ignorada.");
    match.ApplyQuadrantMatchRules(null); Check(!match.AllowDefeatForZeroUnits, "Sem quadrante perdeu fallback da cena.");
    var q = new QuadranteData { allowDefeatForZeroUnits = true }; match.ApplyQuadrantMatchRules(q);
    Check(match.AllowDefeatForZeroUnits, "Quadrante nao aplicou regra.");
    q.allowDefeatForZeroUnits = false; match.ApplyQuadrantMatchRules(q);
    Check(!match.AllowDefeatForZeroUnits, "Quadrante nao desabilitou regra.");
    Set(match, "allowDefeatForZeroUnits", true);
    var tutorial = UnityEngine.ScriptableObject.CreateInstance<TutorialData>(); assets.Add(tutorial);
    Set(match, "activeTutorial", tutorial);
    Check(!match.UsesMatchEndRules && !(bool)Call(match, "TryDefeatSlotIfZeroUnits", PlayerSlotId.FromIndex(0)), "Tutorial eliminou por zero unidades.");
    enemyHq.SetOwnerSlot(0);
    Call(match, "ResolveHeadQuarterCapture", enemyHq, 1, 0, TeamId.Yellow, TeamId.Blue);
    Check(!Defeated(1) && !(bool)Get(match, "hasVictoryWinner"), "Tutorial eliminou por QG.");
    Set(match, "activeTutorial", null); ResetPlayers();
    // Somente um adversario vivo: observa o motivo no funil real e interrompe
    // antes da apresentacao (que buscaria audio/cursor da cena do autor).
    var e2 = entries[2]; entryType.GetField("defeated").SetValue(e2, true); entries[2] = e2;
    MatchController.VictoryReason? reason = null;
    System.Action<PlayerSlotId, TeamId, TeamId, MatchController.VictoryReason, int> observe = (slot, winner, loser, why, turn) => { reason = why; throw new System.Exception("fixture-stop-presentation"); };
    concludedEvent.SetValue(null, observe);
    try { Call(match, "ResolveHeadQuarterCapture", enemyHq, 1, 0, TeamId.Yellow, TeamId.Blue); }
    catch (System.Reflection.TargetInvocationException ex) { if (ex.InnerException?.Message != "fixture-stop-presentation") throw; }
    Check((bool)Get(match, "hasVictoryWinner") && reason == MatchController.VictoryReason.HeadQuarterCaptured, "1v1 perdeu motivo HeadQuarterCaptured.");
    // Mesmo funil chamado no inicio do turno e ao destruir a ultima unidade.
    ResetPlayers(); reason = null; enemyHq.SetOwnerSlot(1); neutral.SetOwnerSlot(0);
    Check(ConstructionManager.IsHeadQuarterlessTeam(TeamId.Blue), "A6 exige slot sem QG.");
    Check((bool)Call(match, "TryResolveArmyElimination", PlayerSlotId.FromIndex(0)), "A6 nao eliminou slot sem QG no inicio do turno.");
    Check(Defeated(0) && neutral.SlotIndex == -1 && !(bool)Get(match, "hasVictoryWinner") && reason == null, "A6 encerrou tres lados ou nao neutralizou o eliminado.");
    ResetPlayers(); reason = null;
    var thirdEntry = entries[2]; entries.RemoveAt(2);
    try
    {
        try { Call(match, "TryResolveArmyElimination", PlayerSlotId.FromIndex(0)); }
        catch (System.Reflection.TargetInvocationException ex) { if (ex.InnerException?.Message != "fixture-stop-presentation") throw; }
        Check(Defeated(0) && (bool)Get(match, "hasVictoryWinner") && (int)Get(match, "victoryWinnerSlotIndex") == 1 && reason == MatchController.VictoryReason.ArmyEliminated, "A6 1v1 nao coroou unico sobrevivente com ArmyEliminated.");
    }
    finally { entries.Add(thirdEntry); }
    return "PASS A: compra pelas quatro sellingRules; FirstOwner por slot/team; captura de QG com tres lados continua e promove; zero unidades derrota sem QG e neutraliza; turno, cenario e tutorial protegidos; quadrante/fallback; vitoria final preserva HeadQuarterCaptured; A6 exercito com tres slots continua, com dois conclui ArmyEliminated. Fixture fora do Play, sem animacao de captura.";
}
finally
{
    defeatedEvent.SetValue(null, oldDefeated); concludedEvent.SetValue(null, oldConcluded);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
    ConstructionManager.AllActive.Clear(); ConstructionManager.AllActive.AddRange(oldBuildings);
    UnitManager.AllActive.Clear(); UnitManager.AllActive.AddRange(oldUnits);
    foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset);
}
