using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum WeaponTrajectoryType
{
    Straight = 0,
    Parabolic = 1
}

[System.Serializable]
public class WeaponForcedLayerAfterHit
{
    [Tooltip("Dominio forcado no alvo quando este efeito acerta.")]
    public Domain domain = Domain.Land;

    [Tooltip("Altura forcada no alvo quando este efeito acerta.")]
    public HeightLevel heightLevel = HeightLevel.Surface;

    [Min(1)]
    [Tooltip("Duracao do lock de camada em turnos do alvo.")]
    public int turns = 2;
}

[CreateAssetMenu(menuName = "Game/Weapons/Weapon Data", fileName = "WeaponData_")]
public class WeaponData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("ID unico usado para lookup e referencia.")]
    public string id;

    [Tooltip("Nome mostrado na UI/debug.")]
    public string displayName;
    [Tooltip("Apelido curto para tabelas/matrizes (ex.: MG, AT, AA).")]
    public string apelido;

    [TextArea]
    public string description;

    [Header("Native Domain")]
    [Tooltip("Dominio/altura nativo da arma.")]
    public Domain domain = Domain.Land;
    [Tooltip("Dominio/altura nativo da arma.")]
    public HeightLevel heightLevel = HeightLevel.Surface;

    [Header("Operation Domains")]
    [FormerlySerializedAs("additionalLayerModes")]
    [Tooltip("Dominios/alturas adicionais onde a arma pode operar.")]
    public List<WeaponLayerMode> aditionalDomainsAllowed = new List<WeaponLayerMode>();

    [Header("Layer Force After Hit")]
    [Tooltip("Forca o alvo a ir para um dominio/altura apos acerto e bloqueia retorno por alguns turnos.")]
    public List<WeaponForcedLayerAfterHit> forceOpponentToGoToDomainAfterHit = new List<WeaponForcedLayerAfterHit>();

    [Header("Combat")]
    [Tooltip("Ataque base da arma (antes de modificadores).")]
    public int basicAttack = 1;
    [Tooltip("Categoria tatica da arma (separada da WeaponClass usada na logistica).")]
    public WeaponCategory weaponCategory = WeaponCategory.AntiInfantaria;
    [SerializeField, HideInInspector] private WeaponClass weaponClass = WeaponClass.Light;
    [Tooltip("Alcance operacional minimo base da arma.")]
    [Min(0)]
    public int operationRangeMin = 1;
    [Tooltip("Alcance operacional maximo base da arma.")]
    [Min(0)]
    public int operationRangeMax = 1;

    [Header("Trajectory")]
    [Tooltip("Tipos de trajetoria suportados por esta arma.")]
    [FormerlySerializedAs("trajectory")]
    public List<WeaponTrajectoryType> trajectories = new List<WeaponTrajectoryType> { WeaponTrajectoryType.Straight };

    [Header("Visuals")]
    [Tooltip("Sprite da arma para manual/referencias futuras.")]
    public Sprite sprite;

    [Header("Ammunition Sprite")]
    [Tooltip("Sprite do token/projetil da municao em voo no tabuleiro.")]
    public Sprite ammunitionSprite;
    [Tooltip("Usa escala explicita para o projetil desta arma.")]
    public bool useExplicitProjectileScale;
    [Tooltip("Escala explicita do projetil quando o override estiver ativo.")]
    [Range(0.05f, 3f)]
    public float projectileScale = 1f;

    [Header("Audio")]
    [Tooltip("Som do disparo desta arma (toca junto com o projetil em voo).")]
    public AudioClip fireSfx;
    [Tooltip("Volume do som de disparo da arma.")]
    [Range(0f, 1f)]
    public float fireSfxVolume = 1f;

    public WeaponClass WeaponClass => weaponClass;
    public WeaponCategory WeaponCategory => weaponCategory;

    private void OnValidate()
    {
        SyncWeaponClassFromAttack();
        if (operationRangeMin < 0)
            operationRangeMin = 0;
        if (operationRangeMax < 0)
            operationRangeMax = 0;
        if (operationRangeMax < operationRangeMin)
            operationRangeMax = operationRangeMin;
        if (forceOpponentToGoToDomainAfterHit == null)
            forceOpponentToGoToDomainAfterHit = new List<WeaponForcedLayerAfterHit>();
        for (int i = 0; i < forceOpponentToGoToDomainAfterHit.Count; i++)
        {
            WeaponForcedLayerAfterHit entry = forceOpponentToGoToDomainAfterHit[i];
            if (entry == null)
                continue;
            entry.turns = Mathf.Max(1, entry.turns);
        }
        EnsureDefaultTrajectory();
        fireSfxVolume = Mathf.Clamp01(fireSfxVolume);
        projectileScale = Mathf.Clamp(projectileScale, 0.05f, 3f);
    }

    public bool SupportsOperationOn(Domain operationDomain, HeightLevel operationHeightLevel)
    {
        if (domain == operationDomain && heightLevel == operationHeightLevel)
            return true;

        if (aditionalDomainsAllowed == null || aditionalDomainsAllowed.Count == 0)
            return false;

        for (int i = 0; i < aditionalDomainsAllowed.Count; i++)
        {
            WeaponLayerMode mode = aditionalDomainsAllowed[i];
            if (mode.domain == operationDomain && mode.heightLevel == operationHeightLevel)
                return true;
        }

        return false;
    }

    public bool SupportsTrajectory(WeaponTrajectoryType trajectoryType)
    {
        if (trajectories == null || trajectories.Count == 0)
            return trajectoryType == WeaponTrajectoryType.Straight;

        for (int i = 0; i < trajectories.Count; i++)
        {
            if (trajectories[i] == trajectoryType)
                return true;
        }

        return false;
    }

    private void EnsureDefaultTrajectory()
    {
        if (trajectories == null)
            trajectories = new List<WeaponTrajectoryType>();

        if (trajectories.Count == 0)
            trajectories.Add(WeaponTrajectoryType.Straight);
    }

    private void SyncWeaponClassFromAttack()
    {
        if (basicAttack >= 9)
        {
            weaponClass = WeaponClass.Heavy;
            return;
        }

        if (basicAttack >= 7)
        {
            weaponClass = WeaponClass.Medium;
            return;
        }

        weaponClass = WeaponClass.Light;
    }
}
