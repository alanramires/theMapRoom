using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ordem de exibicao dos setores nos dropdowns do Editor: BASES primeiro, depois
/// None, depois os setores de campanha.
///
/// Por que aqui e nao no enum: o Unity monta o popup com Enum.GetNames, que ordena
/// pelo VALOR binario — reordenar a declaracao em ConstructionSector.cs nao muda
/// nada, e renumerar Base0..Base4 (100..104) quebraria todo predio de base ja
/// gravado em cena e save. Entao a ordem e cosmetica e mora do lado do Editor.
///
/// A lista se monta sozinha por reflexao: acrescentar Base5 ou um setor grego novo
/// aparece na posicao certa sem tocar neste arquivo.
/// </summary>
public static class ConstructionSectorOrder
{
    /// <summary>Valores indexados por enumValueIndex (Enum.GetValues ja vem ordenado por valor).</summary>
    private static readonly ConstructionSector[] ByDeclarationIndex =
        (ConstructionSector[])Enum.GetValues(typeof(ConstructionSector));

    /// <summary>Para cada posicao exibida, o enumValueIndex correspondente.</summary>
    private static readonly int[] DeclarationIndexByDisplay;

    /// <summary>Para cada enumValueIndex, a posicao exibida.</summary>
    private static readonly int[] DisplayByDeclarationIndex;

    /// <summary>Rotulos na ordem de exibicao — pronto pra EditorGUI.Popup.</summary>
    public static readonly string[] Labels;

    static ConstructionSectorOrder()
    {
        var bases = new List<int>();
        var none = new List<int>();
        var campaign = new List<int>();

        for (int i = 0; i < ByDeclarationIndex.Length; i++)
        {
            ConstructionSector sector = ByDeclarationIndex[i];
            if (ConstructionSectorHelper.IsBase(sector)) bases.Add(i);
            else if (sector == ConstructionSector.None) none.Add(i);
            else campaign.Add(i);
        }

        var ordered = new List<int>(ByDeclarationIndex.Length);
        ordered.AddRange(bases);
        ordered.AddRange(none);
        ordered.AddRange(campaign);

        DeclarationIndexByDisplay = ordered.ToArray();
        DisplayByDeclarationIndex = new int[ByDeclarationIndex.Length];
        Labels = new string[ordered.Count];

        for (int display = 0; display < ordered.Count; display++)
        {
            int decl = ordered[display];
            DisplayByDeclarationIndex[decl] = display;
            Labels[display] = ByDeclarationIndex[decl].ToString();
        }
    }

    public static int ToDisplayIndex(int enumValueIndex)
    {
        if (enumValueIndex < 0 || enumValueIndex >= DisplayByDeclarationIndex.Length)
            return -1;
        return DisplayByDeclarationIndex[enumValueIndex];
    }

    public static int ToEnumValueIndex(int displayIndex)
    {
        if (displayIndex < 0 || displayIndex >= DeclarationIndexByDisplay.Length)
            return -1;
        return DeclarationIndexByDisplay[displayIndex];
    }

    public static ConstructionSector SectorAtDisplayIndex(int displayIndex)
    {
        int decl = ToEnumValueIndex(displayIndex);
        return decl >= 0 ? ByDeclarationIndex[decl] : ConstructionSector.None;
    }

    public static int DisplayIndexOf(ConstructionSector sector)
    {
        for (int i = 0; i < ByDeclarationIndex.Length; i++)
            if (ByDeclarationIndex[i] == sector)
                return DisplayByDeclarationIndex[i];
        return -1;
    }

    /// <summary>Popup ordenado para campos que nao passam por SerializedProperty.</summary>
    public static ConstructionSector Popup(string label, ConstructionSector current)
    {
        int display = Mathf.Max(0, DisplayIndexOf(current));
        int next = EditorGUILayout.Popup(label, display, Labels);
        return SectorAtDisplayIndex(next);
    }
}

/// <summary>
/// Aplica a ordem acima em todo campo ConstructionSector desenhado pelo inspector
/// padrao (ConstructionFieldEntry, SectorObjective, mission sector da unidade...).
/// </summary>
[CustomPropertyDrawer(typeof(ConstructionSector))]
public sealed class ConstructionSectorDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.Enum)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        EditorGUI.BeginProperty(position, label, property);

        Rect field = EditorGUI.PrefixLabel(
            position, GUIUtility.GetControlID(FocusType.Passive), label);

        int display = ConstructionSectorOrder.ToDisplayIndex(property.enumValueIndex);
        bool prevMixed = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        // PrefixLabel ja consumiu o recuo; sem zerar, o popup indenta de novo.
        int prevIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        EditorGUI.BeginChangeCheck();
        int next = EditorGUI.Popup(field, Mathf.Max(0, display), ConstructionSectorOrder.Labels);
        if (EditorGUI.EndChangeCheck() && next != display)
        {
            int decl = ConstructionSectorOrder.ToEnumValueIndex(next);
            if (decl >= 0)
                property.enumValueIndex = decl;
        }

        EditorGUI.indentLevel = prevIndent;
        EditorGUI.showMixedValue = prevMixed;
        EditorGUI.EndProperty();
    }
}
