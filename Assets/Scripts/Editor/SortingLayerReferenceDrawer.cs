using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SortingLayerReference))]
public class SortingLayerReferenceDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty idProperty = property.FindPropertyRelative("id");
        if (idProperty == null)
        {
            EditorGUI.LabelField(position, label.text, "SortingLayerReference: id nao encontrado");
            return;
        }

        SortingLayer[] layers = SortingLayer.layers;
        if (layers == null || layers.Length == 0)
        {
            EditorGUI.LabelField(position, label.text, "Sem Sorting Layers");
            return;
        }

        string[] layerNames = new string[layers.Length];
        int[] layerIds = new int[layers.Length];
        for (int i = 0; i < layers.Length; i++)
        {
            layerNames[i] = layers[i].name;
            layerIds[i] = layers[i].id;
        }

        int currentIndex = 0;
        for (int i = 0; i < layerIds.Length; i++)
        {
            if (layerIds[i] == idProperty.intValue)
            {
                currentIndex = i;
                break;
            }
        }

        int selectedIndex = EditorGUI.Popup(position, label.text, currentIndex, layerNames);
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Min(layerIds.Length, layerNames.Length) - 1);
        idProperty.intValue = layerIds[selectedIndex];
    }
}
