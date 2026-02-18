using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Game/Supplies/Supply Data", fileName = "SupplyData_")]
public class SupplyData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("ID unico usado para lookup e referencia.")]
    public string id;

    [Tooltip("Nome mostrado na UI/debug.")]
    public string displayName;

    [TextArea]
    public string description;

    [Header("Visuals")]
    public Sprite spriteDefault;

    [Header("Related Service")]
    [Tooltip("Servicos relacionados que consomem este suprimento.")]
    [FormerlySerializedAs("relatedService")]
    public List<ServiceData> relatedServices = new List<ServiceData>();

    private void OnValidate()
    {
        if (relatedServices == null)
            relatedServices = new List<ServiceData>();
    }
}
