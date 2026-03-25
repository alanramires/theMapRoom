using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Novo TutorialDatabase", menuName = "Game/Tutorial/Tutorial Database")]
public class TutorialDatabase : ScriptableObject
{
    public List<TutorialData> tutorials = new List<TutorialData>();

    public TutorialData GetTutorialData(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        for (int i = 0; i < tutorials.Count; i++)
        {
            if (tutorials[i] != null && tutorials[i].id == id)
                return tutorials[i];
        }
        return null;
    }
}
