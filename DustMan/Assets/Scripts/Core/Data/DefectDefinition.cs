using UnityEngine;

public enum DefectVisibility
{
    /// Виден сразу при осмотре.
    Immediate = 0,
    /// Виден только через лупу.
    Magnifier = 1,
    /// Виден только после разборки.
    AfterDisassembly = 2,
}

/// Дефект: где виден, чем и каким жестом чинится, симптом на проверке.
[CreateAssetMenu(menuName = "DustMan/Defect Definition", fileName = "DefectDefinition")]
public class DefectDefinition : ScriptableObject
{
    public string id = "defect";
    public string displayName = "Дефект";
    public DefectVisibility visibility = DefectVisibility.Immediate;

    [Header("Ремонт")]
    /// Инструмент; null = замена детали из запаса (drag без инструмента).
    public ToolDefinition tool;
    public GestureKind gesture = GestureKind.Drag;

    [Header("Проверка")]
    /// Пустая строка = дефект не влияет на работоспособность (только на цену).
    public string symptom = "";
    public bool blocksFunction = true;
    /// Меньше = симптом показывается раньше при нескольких дефектах.
    public int symptomPriority = 10;

    [Header("Описание")]
    /// Как дефект выглядит в списке на рынке (для видимых сразу).
    public string marketNote = "";
}
