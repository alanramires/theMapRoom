using UnityEngine;

[CreateAssetMenu(menuName = "Game/Skills/Skill Data", fileName = "SkillData_")]
public class SkillData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("ID unico da skill (ex.: guerrilha, alpino, off-road).")]
    public string id;

    [Tooltip("Nome mostrado na UI/debug.")]
    public string displayName;

    [TextArea]
    public string description;
}
