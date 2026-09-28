using System.Collections.Generic;
using UnityEngine;

/// Предмет: список модулей, цены, границы количества дефектов.
[CreateAssetMenu(menuName = "DustMan/Item Definition", fileName = "ItemDefinition")]
public class ItemDefinition : ScriptableObject
{
    public string id = "item";
    public string displayName = "Предмет";
    public List<ModuleDefinition> modules = new List<ModuleDefinition>();

    [Header("Цены")]
    public int baseRepairedPrice = 60;
    public int marketPriceMin = 15;
    public int marketPriceMax = 25;

    [Header("Генерация дефектов")]
    public int minDefects = 2;
    public int maxDefects = 3;
}
