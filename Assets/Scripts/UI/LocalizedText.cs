using TMPro;
using UnityEngine;

/// <summary>Localizes a fixed TMP label when its screen opens. Layout stays authored in Unity.</summary>
[AddComponentMenu("UI/Localized Text")]
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public sealed class LocalizedText : MonoBehaviour
{
    [SerializeField, Tooltip("Arraste uma ficha UI Text Data. Sem ficha, preserva o texto do TMP.")]
    private UITextData textData;
    private TMP_Text target;

    private void OnEnable() => Refresh();
    private void Start() => Refresh();

    public void Refresh()
    {
        if (textData == null) return;
        if (target == null) target = GetComponent<TMP_Text>();
        if (target != null) target.text = textData.LocalizedMessage;
    }
}
