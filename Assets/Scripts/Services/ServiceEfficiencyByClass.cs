using UnityEngine;
using UnityEngine.Serialization;

public enum ArmorClass
{
    Light = 0,
    Medium = 1,
    Heavy = 2
}

public enum ArmorWeaponClass
{
    Light = 0,
    Medium = 1,
    Heavy = 2
}

[System.Serializable]
public class ServiceEfficiencyByClass
{
    [FormerlySerializedAs("armorClass")]
    [Tooltip("Classe alvo (armor/weapon) para esta eficiencia.")]
    public ArmorWeaponClass armorWeaponClass = ArmorWeaponClass.Light;

    [Tooltip("Valor numerico de eficiencia para a blindagem selecionada.")]
    public float value = 1f;
}
