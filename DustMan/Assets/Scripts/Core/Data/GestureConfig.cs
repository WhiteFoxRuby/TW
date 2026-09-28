using UnityEngine;

/// Все пороги жестов в одном месте — их крутят на плейтестах (mvp-scope §5).
[CreateAssetMenu(menuName = "DustMan/Gesture Config", fileName = "GestureConfig")]
public class GestureConfig : ScriptableObject
{
    [Header("Вращение")]
    /// Угловая дельта за кадр меньше порога считается дрожанием и игнорируется.
    public float rotationDeadzoneDegPerFrame = 0.5f;
    /// Ближе этого радиуса к центру вращения угол нестабилен — шаг не считается.
    public float rotationMinRadius = 0.15f;
    /// Сколько полных оборотов выкручивает винт.
    public int screwTurnsToRemove = 2;
    /// На сколько оборотов сверх нормы можно перетянуть до штрафа.
    public int overtightenExtraTurns = 2;
    /// Радиус захвата вокруг винта, в мировых единицах.
    public float screwEngageRadius = 0.7f;

    [Header("Протирка")]
    public int wipeGridSize = 8;
    /// Радиус кисти в мировых единицах.
    public float wipeBrushRadius = 0.3f;
    /// Доля очищенных ячеек, при которой дефект считается снятым.
    [Range(0.5f, 1f)] public float wipeCompleteFraction = 0.9f;

    [Header("Качание")]
    /// Минимальная амплитуда хода в мировых единицах.
    public float rockMinAmplitude = 0.35f;
    /// Сколько качаний (туда-обратно) нужно для завершения.
    public int rocksRequired = 6;
    /// Радиус зоны качания вокруг цели.
    public float rockEngageRadius = 0.9f;

    [Header("Drag")]
    /// Радиус снапа к слоту.
    public float dragSnapRadius = 0.6f;

    [Header("Зум")]
    public float zoomMin = 2f;
    public float zoomMax = 6f;
    public float zoomStep = 0.5f;

    [Header("Осмотр")]
    /// Градусы поворота фонаря на одну мировую единицу горизонтального drag.
    public float inspectRotateDegPerUnit = 40f;

    [Header("Лупа")]
    /// Сколько секунд держать лупу над дефектом, чтобы он проявился.
    public float magnifierRevealSeconds = 0.4f;
    /// Радиус действия лупы.
    public float magnifierRadius = 0.8f;
}
