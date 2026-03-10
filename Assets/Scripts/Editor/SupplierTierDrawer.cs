using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SupplierTier))]
public sealed class SupplierTierDrawer : PropertyDrawer
{
    private static readonly GUIContent[] Labels =
    {
        new GUIContent("Hub"),
        new GUIContent("Receiver")
    };

    private static readonly int[] Values =
    {
        (int)SupplierTier.Hub,
        (int)SupplierTier.Receiver
    };

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property == null || property.propertyType != SerializedPropertyType.Enum)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        int current = property.intValue;
        if (current == (int)SupplierTier.SelfSupplier)
            current = (int)SupplierTier.Receiver;

        int selectedIndex = current == (int)SupplierTier.Hub ? 0 : 1;
        selectedIndex = EditorGUI.IntPopup(position, label, selectedIndex, Labels, new[] { 0, 1 });

        property.intValue = selectedIndex == 0
            ? (int)SupplierTier.Hub
            : (int)SupplierTier.Receiver;
    }
}
