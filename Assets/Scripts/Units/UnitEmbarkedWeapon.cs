using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class UnitEmbarkedWeapon
{
    [Tooltip("Arma escolhida do catalogo (WeaponData).")]
    public WeaponData weapon;

    [Header("Squad Setup")]
    [Tooltip("Municao disponivel para esta arma nesta unidade.")]
    [FormerlySerializedAs("squadAttack")]
    [Min(0)]
    public int squadAmmunition = 0;

    [Header("Range Setup")]
    [Tooltip("Alcance operacional minimo desta arma nesta unidade.")]
    [Min(0)]
    public int operationRangeMin = 0;

    [Tooltip("Alcance operacional maximo desta arma nesta unidade.")]
    [Min(0)]
    public int operationRangeMax = 0;

    [Header("Trajectory Override")]
    [Tooltip("Trajetoria escolhida para esta arma nesta unidade (override por operador).")]
    public WeaponTrajectoryType selectedTrajectory = WeaponTrajectoryType.Straight;

    [Header("Layer Fire Restriction")]
    [Tooltip("Can be fire only at domain/heigh. Vazio = sem restricao de camada para disparo desta arma.")]
    public List<UnitLayerMode> canBeFireOnlyAtDomainHeigh = new List<UnitLayerMode>();

    [SerializeField, HideInInspector] private WeaponData lastSyncedWeapon;

    public void SyncFromWeaponDefaultsIfNeeded()
    {
        if (canBeFireOnlyAtDomainHeigh == null)
            canBeFireOnlyAtDomainHeigh = new List<UnitLayerMode>();

        bool weaponChanged = lastSyncedWeapon != weapon;
        if (weapon == null)
        {
            if (weaponChanged)
            {
                operationRangeMin = 0;
                operationRangeMax = 0;
                selectedTrajectory = WeaponTrajectoryType.Straight;
            }

            lastSyncedWeapon = null;
            return;
        }

        if (weaponChanged)
        {
            operationRangeMin = Mathf.Max(0, weapon.operationRangeMin);
            operationRangeMax = Mathf.Max(operationRangeMin, weapon.operationRangeMax);
        }
        else if (operationRangeMax < operationRangeMin)
        {
            operationRangeMax = operationRangeMin;
        }

        EnsureValidSelectedTrajectory();
        lastSyncedWeapon = weapon;
    }

    public int GetRangeMin()
    {
        return Mathf.Max(0, operationRangeMin);
    }

    public int GetRangeMax()
    {
        return Mathf.Max(GetRangeMin(), operationRangeMax);
    }

    public IReadOnlyList<WeaponTrajectoryType> GetAvailableTrajectories()
    {
        if (weapon == null || weapon.trajectories == null || weapon.trajectories.Count == 0)
            return new[] { WeaponTrajectoryType.Straight };

        return weapon.trajectories;
    }

    public int GetWeaponBasicAttack()
    {
        return weapon != null ? weapon.basicAttack : 0;
    }

    public void EnsureValidSelectedTrajectory()
    {
        IReadOnlyList<WeaponTrajectoryType> available = GetAvailableTrajectories();
        if (available == null || available.Count == 0)
        {
            selectedTrajectory = WeaponTrajectoryType.Straight;
            return;
        }

        for (int i = 0; i < available.Count; i++)
        {
            if (available[i] == selectedTrajectory)
                return;
        }

        selectedTrajectory = available[0];
    }

    public bool CanFireAtLayer(Domain domain, HeightLevel heightLevel)
    {
        if (canBeFireOnlyAtDomainHeigh == null || canBeFireOnlyAtDomainHeigh.Count <= 0)
            return true;

        for (int i = 0; i < canBeFireOnlyAtDomainHeigh.Count; i++)
        {
            UnitLayerMode mode = canBeFireOnlyAtDomainHeigh[i];
            if (mode.domain == domain && mode.heightLevel == heightLevel)
                return true;
        }

        return false;
    }
}
