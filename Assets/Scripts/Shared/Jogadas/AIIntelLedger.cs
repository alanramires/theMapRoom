using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AIIntelContact
{
    public int uid;
    public int enemyTeam;
    public string sigla;
    public int lastSeenTurn;
    public Vector3Int lastKnownCell;
    public float confidence;
    public string source;
    public bool destroyed;
    public float recentDamageDealt;
    public float recentKills;
    public float recentDestroyedValue;
}

[Serializable]
public class AIIntelLedgerSaveData
{
    public int observerTeam;
    public int lastProcessedJogadaId;
    public List<AIIntelContact> contacts = new List<AIIntelContact>();
    public List<AIIntelThreatSignal> threatSignals = new List<AIIntelThreatSignal>();
    public AIElitePurchaseCommitment elitePurchaseCommitment;
}

[Serializable]
public class AIElitePurchaseCommitment
{
    public string unitId;
    public UnitRole role;
    public int eliteLevel;
    public int targetCost;
    public int committedTurn;
    public bool counterEscalation;
    public WeaponCategory counterCategory;
    public bool counterHasTargetClass;
    public GameUnitClass counterTargetClass;
}

[Serializable]
public class AIIntelThreatSignal
{
    public int jogadaId;
    public int turn;
    public WeaponCategory weaponCategory;
    public WeaponTrajectoryType trajectory;
    public int damage;
    public int kills;
    public float destroyedValue;
}

public static class AIIntelLedger
{
    private sealed class TeamLedger
    {
        public TeamId Observer;
        public int LastProcessedJogadaId;
        public readonly Dictionary<int, AIIntelContact> Contacts =
            new Dictionary<int, AIIntelContact>();
        public readonly List<AIIntelThreatSignal> ThreatSignals =
            new List<AIIntelThreatSignal>();
        public AIElitePurchaseCommitment ElitePurchaseCommitment;
    }

    private static readonly Dictionary<TeamId, TeamLedger> ledgers =
        new Dictionary<TeamId, TeamLedger>();

    public static IReadOnlyCollection<AIIntelContact> UpdateAndGetContacts(
        AIWorldSnapshot snapshot)
    {
        if (snapshot == null)
            return Array.Empty<AIIntelContact>();
        TeamLedger ledger = GetOrCreate(snapshot.AITeam);
        ProcessObservableCombatEvents(ledger);
        UpdateVisibleContacts(ledger, snapshot);
        return ledger.Contacts.Values;
    }

    public static IReadOnlyList<AIIntelThreatSignal> GetThreatSignals(TeamId observer)
        => GetOrCreate(observer).ThreatSignals;

    public static void RecordVisibleContactsForTeam(
        TeamId observer,
        int turn,
        MatchController match)
    {
        if (observer == TeamId.Neutral || match == null)
            return;

        TeamLedger ledger = GetOrCreate(observer);
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked
                || enemy.TeamId == observer
                || !match.IsUnitVisibleForTeam(enemy, observer)
                || !enemy.TryGetUnitData(out UnitData data) || data == null)
                continue;

            RecordVisibleContact(ledger, enemy, data, turn);
        }
    }

    public static List<AIIntelLedgerSaveData> BuildSaveData()
    {
        var result = new List<AIIntelLedgerSaveData>();
        foreach (TeamLedger ledger in ledgers.Values)
        {
            var saved = new AIIntelLedgerSaveData
            {
                observerTeam = (int)ledger.Observer,
                lastProcessedJogadaId = ledger.LastProcessedJogadaId,
                elitePurchaseCommitment = Clone(ledger.ElitePurchaseCommitment),
            };
            foreach (AIIntelContact contact in ledger.Contacts.Values)
                saved.contacts.Add(Clone(contact));
            foreach (AIIntelThreatSignal signal in ledger.ThreatSignals)
                saved.threatSignals.Add(Clone(signal));
            result.Add(saved);
        }
        return result;
    }

    public static void Restore(List<AIIntelLedgerSaveData> savedLedgers)
    {
        ledgers.Clear();
        if (savedLedgers == null)
            return;
        foreach (AIIntelLedgerSaveData saved in savedLedgers)
        {
            if (saved == null || !Enum.IsDefined(typeof(TeamId), saved.observerTeam))
                continue;
            TeamLedger ledger = GetOrCreate((TeamId)saved.observerTeam);
            ledger.LastProcessedJogadaId = Mathf.Max(0, saved.lastProcessedJogadaId);
            ledger.ElitePurchaseCommitment = Clone(saved.elitePurchaseCommitment);
            if (saved.contacts != null)
                foreach (AIIntelContact contact in saved.contacts)
                    if (contact != null && contact.uid > 0)
                        ledger.Contacts[contact.uid] = Clone(contact);
            if (saved.threatSignals != null)
                foreach (AIIntelThreatSignal signal in saved.threatSignals)
                    if (signal != null)
                        ledger.ThreatSignals.Add(Clone(signal));
        }
    }

    public static void Clear() => ledgers.Clear();

    public static AIElitePurchaseCommitment GetElitePurchaseCommitment(TeamId observer)
        => Clone(GetOrCreate(observer).ElitePurchaseCommitment);

    public static void SetElitePurchaseCommitment(
        TeamId observer, AIElitePurchaseCommitment commitment)
    {
        GetOrCreate(observer).ElitePurchaseCommitment = Clone(commitment);
    }

    public static void ClearElitePurchaseCommitment(TeamId observer)
    {
        GetOrCreate(observer).ElitePurchaseCommitment = null;
    }

    private static TeamLedger GetOrCreate(TeamId observer)
    {
        if (!ledgers.TryGetValue(observer, out TeamLedger ledger))
        {
            ledger = new TeamLedger { Observer = observer };
            ledgers.Add(observer, ledger);
        }
        return ledger;
    }

    private static void UpdateVisibleContacts(TeamLedger ledger, AIWorldSnapshot snapshot)
    {
        if (snapshot.EnemyUnits == null)
            return;
        foreach (UnitManager enemy in snapshot.EnemyUnits)
        {
            if (enemy == null || enemy.IsDead
                || !enemy.TryGetUnitData(out UnitData data) || data == null)
                continue;
            RecordVisibleContact(ledger, enemy, data, snapshot.TurnNumber);
        }
    }

    private static void RecordVisibleContact(
        TeamLedger ledger,
        UnitManager enemy,
        UnitData data,
        int turn)
    {
        AIIntelContact contact = GetOrCreateContact(ledger, enemy.InstanceId);
        contact.enemyTeam = (int)enemy.TeamId;
        contact.sigla = data.apelido;
        contact.lastSeenTurn = turn;
        contact.lastKnownCell = enemy.CurrentCellPosition;
        contact.confidence = 1f;
        contact.source = "sensor";
        contact.destroyed = false;
    }

    private static void ProcessObservableCombatEvents(TeamLedger ledger)
    {
        JogadasLog log = JogadasManager.Instance != null ? JogadasManager.Instance.log : null;
        if (log?.jogadas == null)
            return;
        foreach (Jogada play in log.jogadas)
        {
            if (play == null || play.jogadaId <= ledger.LastProcessedJogadaId)
                continue;
            ledger.LastProcessedJogadaId = Mathf.Max(ledger.LastProcessedJogadaId, play.jogadaId);
            if (!play.hasCombatResult
                || !string.Equals(play.acao, "Ataque", StringComparison.OrdinalIgnoreCase))
                continue;

            bool attackerFriendly = play.team == (int)ledger.Observer;
            bool defenderFriendly = play.team2 == (int)ledger.Observer;
            if (defenderFriendly)
            {
                if (play.attackerVisibleToDefender)
                    ObserveCombatant(
                        ledger, play.uid, play.team, play.unidadeSigla,
                        play.cx, play.cy, play.turno,
                        Mathf.Max(0, play.hp2Antes - play.hp2Depois),
                        play.hp2Antes > 0 && play.hp2Depois <= 0,
                        play.hpAntes > 0 && play.hpDepois <= 0);
                else if (play.hasAttackIntel)
                    RecordAnonymousThreatSignal(ledger, play);
            }
            if (attackerFriendly)
                ObserveCombatant(
                    ledger, play.uid2, play.team2, play.unidadeSigla2,
                    play.dx, play.dy, play.turno,
                    Mathf.Max(0, play.hpAntes - play.hpDepois),
                    play.hpAntes > 0 && play.hpDepois <= 0,
                    play.hp2Antes > 0 && play.hp2Depois <= 0);
        }
    }

    private static void RecordAnonymousThreatSignal(TeamLedger ledger, Jogada play)
    {
        for (int i = 0; i < ledger.ThreatSignals.Count; i++)
            if (ledger.ThreatSignals[i].jogadaId == play.jogadaId)
                return;

        int killed = play.hp2Antes > 0 && play.hp2Depois <= 0 ? 1 : 0;
        float destroyedValue = killed > 0
            ? Mathf.Max(0, play.defenderCost) * (1f + Mathf.Max(0, play.defenderEliteLevel) * 0.5f)
            : 0f;
        if (killed > 0)
        {
            UnitData defenderData = destroyedValue <= 0f
                ? ResolveUnitDataBySigla(play.unidadeSigla2)
                : null;
            if (defenderData != null)
                destroyedValue = Mathf.Max(0, defenderData.cost)
                    * (1f + Mathf.Max(0, defenderData.eliteLevel) * 0.5f);
        }
        if (play.combatCargo != null)
            foreach (CombatCargoResult cargo in play.combatCargo)
                if (cargo != null && cargo.team == (int)ledger.Observer
                    && cargo.hpAntes > 0 && cargo.hpDepois <= 0)
                {
                    killed++;
                    destroyedValue += Mathf.Max(0, cargo.cost);
                }

        ledger.ThreatSignals.Add(new AIIntelThreatSignal
        {
            jogadaId = play.jogadaId,
            turn = play.turno,
            weaponCategory = play.attackWeaponCategory,
            trajectory = play.attackTrajectory,
            damage = Mathf.Max(0, play.hp2Antes - play.hp2Depois),
            kills = killed,
            destroyedValue = destroyedValue,
        });
        ledger.ThreatSignals.RemoveAll(signal =>
            signal == null || signal.turn < play.turno - 8);
    }

    private static UnitData ResolveUnitDataBySigla(string sigla)
    {
        if (string.IsNullOrWhiteSpace(sigla))
            return null;
        foreach (UnitManager unit in UnitManager.AllActive)
            if (unit != null && unit.TryGetUnitData(out UnitData data) && data != null
                && string.Equals(data.apelido, sigla, StringComparison.OrdinalIgnoreCase))
                return data;
        return null;
    }

    private static void ObserveCombatant(
        TeamLedger ledger, int uid, int team, string sigla,
        int x, int y, int turn, int damageDealt, bool killed, bool destroyed)
    {
        if (uid <= 0)
            return;
        AIIntelContact contact = GetOrCreateContact(ledger, uid);
        contact.enemyTeam = team;
        if (!string.IsNullOrWhiteSpace(sigla))
            contact.sigla = sigla;
        contact.lastSeenTurn = turn;
        contact.lastKnownCell = new Vector3Int(x, y, 0);
        contact.confidence = Mathf.Max(contact.confidence, 0.8f);
        contact.source = "combate";
        contact.destroyed = destroyed;
        contact.recentDamageDealt += damageDealt;
        if (killed)
            contact.recentKills += 1f;
    }

    private static AIIntelContact GetOrCreateContact(TeamLedger ledger, int uid)
    {
        if (!ledger.Contacts.TryGetValue(uid, out AIIntelContact contact))
        {
            contact = new AIIntelContact { uid = uid };
            ledger.Contacts.Add(uid, contact);
        }
        return contact;
    }

    private static AIIntelContact Clone(AIIntelContact source)
    {
        return new AIIntelContact
        {
            uid = source.uid,
            enemyTeam = source.enemyTeam,
            sigla = source.sigla,
            lastSeenTurn = source.lastSeenTurn,
            lastKnownCell = source.lastKnownCell,
            confidence = source.confidence,
            source = source.source,
            destroyed = source.destroyed,
            recentDamageDealt = source.recentDamageDealt,
            recentKills = source.recentKills,
            recentDestroyedValue = source.recentDestroyedValue,
        };
    }

    private static AIIntelThreatSignal Clone(AIIntelThreatSignal source)
    {
        return new AIIntelThreatSignal
        {
            jogadaId = source.jogadaId,
            turn = source.turn,
            weaponCategory = source.weaponCategory,
            trajectory = source.trajectory,
            damage = source.damage,
            kills = source.kills,
            destroyedValue = source.destroyedValue,
        };
    }

    private static AIElitePurchaseCommitment Clone(AIElitePurchaseCommitment source)
    {
        if (source == null)
            return null;
        return new AIElitePurchaseCommitment
        {
            unitId = source.unitId,
            role = source.role,
            eliteLevel = source.eliteLevel,
            targetCost = source.targetCost,
            committedTurn = source.committedTurn,
            counterEscalation = source.counterEscalation,
            counterCategory = source.counterCategory,
            counterHasTargetClass = source.counterHasTargetClass,
            counterTargetClass = source.counterTargetClass,
        };
    }
}
