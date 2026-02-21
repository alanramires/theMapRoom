using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class UnitTransportSlotRule
{
    [Tooltip("Nome do slot para debug (ex.: troop slot, tow slot).")]
    public string slotId = "slot";

    [Min(1)]
    [Tooltip("Capacidade total deste slot.")]
    public int capacity = 1;

    [Tooltip("Modos de dominio/altura permitidos para embarque neste slot. Se vazio, usa Land/Surface.")]
    public List<TransportSlotLayerMode> allowedLayerModes = new List<TransportSlotLayerMode>();
    [SerializeField, HideInInspector] private Domain legacyAllowedDomain = Domain.Land;
    [SerializeField, HideInInspector] private HeightLevel legacyAllowedHeight = HeightLevel.Surface;
    [SerializeField, HideInInspector] private List<UnitLayerMode> legacyAllowedLayerModes = new List<UnitLayerMode>();

    [Tooltip("Classes permitidas no slot. Se vazio, nao restringe classe.")]
    public List<GameUnitClass> allowedClasses = new List<GameUnitClass>();

    [Tooltip("Skills obrigatorias para embarque neste slot. Se vazio, nao exige skill.")]
    public List<SkillData> requiredSkills = new List<SkillData>();

    [Tooltip("Skills bloqueadas para este slot. Se a unidade tiver alguma delas, embarque e negado.")]
    public List<SkillData> blockedSkills = new List<SkillData>();

    public void EnsureDefaults()
    {
        capacity = Mathf.Max(1, capacity);
        if (allowedLayerModes == null)
            allowedLayerModes = new List<TransportSlotLayerMode>();
        if (allowedClasses == null)
            allowedClasses = new List<GameUnitClass>();
        if (requiredSkills == null)
            requiredSkills = new List<SkillData>();
        if (blockedSkills == null)
            blockedSkills = new List<SkillData>();

        if (allowedLayerModes.Count == 0 && legacyAllowedLayerModes != null && legacyAllowedLayerModes.Count > 0)
        {
            for (int i = 0; i < legacyAllowedLayerModes.Count; i++)
            {
                UnitLayerMode legacyMode = legacyAllowedLayerModes[i];
                allowedLayerModes.Add(new TransportSlotLayerMode(legacyMode.domain, legacyMode.heightLevel));
            }
        }

        if (allowedLayerModes.Count == 0)
            allowedLayerModes.Add(new TransportSlotLayerMode(legacyAllowedDomain, legacyAllowedHeight));
    }
}
