using System.Collections.Generic;
using UnityEngine;

/// Модуль предмета: допустимые дефекты и параметры плейсхолдер-отрисовки.
[CreateAssetMenu(menuName = "DustMan/Module Definition", fileName = "ModuleDefinition")]
public class ModuleDefinition : ScriptableObject
{
    public string id = "module";
    public string displayName = "Модуль";
    public List<DefectDefinition> possibleDefects = new List<DefectDefinition>();

    [Header("Плейсхолдер-визуал")]
    public Color color = Color.gray;
    public Vector2 size = Vector2.one;
    /// Позиция модуля в разобранном (exploded) виде на верстаке.
    public Vector2 explodedOffset = Vector2.zero;
}
