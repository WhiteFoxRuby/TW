using UnityEngine;

/// Инструмент: каким жестом работает и как выглядит на столе.
[CreateAssetMenu(menuName = "DustMan/Tool Definition", fileName = "ToolDefinition")]
public class ToolDefinition : ScriptableObject
{
    public string id = "tool";
    public string displayName = "Инструмент";
    public GestureKind gesture = GestureKind.Click;

    [Header("Плейсхолдер-визуал")]
    public Color color = Color.white;
}
