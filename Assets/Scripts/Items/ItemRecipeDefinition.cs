using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Data-driven carry-inventory recipe with exact item-definition ingredients and outputs.</summary>
[CreateAssetMenu(fileName = "ItemRecipeDefinition", menuName = "Items/Recipe Definition")]
public sealed class ItemRecipeDefinition : ScriptableObject
{
    [SerializeField] private List<ItemRecipeEntry> ingredients = new List<ItemRecipeEntry>();
    [SerializeField] private List<ItemRecipeEntry> outputs = new List<ItemRecipeEntry>();

    /// <summary>Gets the ingredient definitions and required quantities.</summary>
    public IReadOnlyList<ItemRecipeEntry> Ingredients => ingredients;

    /// <summary>Gets the output definitions and produced quantities.</summary>
    public IReadOnlyList<ItemRecipeEntry> Outputs => outputs;

    private void OnValidate()
    {
        if (ingredients == null)
        {
            ingredients = new List<ItemRecipeEntry>();
        }

        if (outputs == null)
        {
            outputs = new List<ItemRecipeEntry>();
        }
    }
}

/// <summary>One exact item-definition quantity in a recipe.</summary>
[Serializable]
public struct ItemRecipeEntry
{
    [SerializeField] private ItemDefinition definition;
    [SerializeField, Min(1)] private int quantity;

    /// <summary>Creates a recipe entry for an exact definition and positive quantity.</summary>
    public ItemRecipeEntry(ItemDefinition itemDefinition, int itemQuantity)
    {
        definition = itemDefinition;
        quantity = itemQuantity;
    }

    /// <summary>Gets the item definition used by this recipe entry.</summary>
    public ItemDefinition Definition => definition;

    /// <summary>Gets the required or produced quantity.</summary>
    public int Quantity => quantity;
}
