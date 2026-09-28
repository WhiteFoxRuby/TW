using UnityEngine;

/// Алфавит жестов среза (mvp-scope §2).
public enum GestureKind
{
    Rotate = 0,
    Wipe = 1,
    Rock = 2,
    Drag = 3,
    Click = 4,
    Zoom = 5,
}

/// Вращение: накапливает угловую дельту вектора курсор→центр.
/// Против часовой — положительно (выкручивание), по часовой — отрицательно.
public class RotationTracker
{
    private Vector2 center;
    private float lastAngle;
    private bool rebasePending;

    public bool Engaged { get; private set; }
    /// Накопленный угол в градусах; CCW положителен.
    public float AccumulatedDeg { get; private set; }

    public float Turns => AccumulatedDeg / 360f;

    public void Engage(Vector2 rotationCenter, Vector2 cursor)
    {
        center = rotationCenter;
        lastAngle = AngleTo(cursor);
        rebasePending = false;
        Engaged = true;
    }

    public void Release()
    {
        Engaged = false;
    }

    /// Возвращает дельту за кадр (в градусах). Дельты меньше дедзоны копятся
    /// (референсный угол не сдвигается), поэтому медленное вращение не теряется.
    /// Вблизи центра (minRadius) угол нестабилен — шаг пропускается.
    public float Step(Vector2 cursor, float deadzoneDeg, float minRadius = 0f)
    {
        if (!Engaged) return 0f;

        Vector2 v = cursor - center;
        if (v.sqrMagnitude < minRadius * minRadius)
        {
            rebasePending = true;
            return 0f;
        }

        float angle = AngleTo(cursor);
        if (rebasePending)
        {
            lastAngle = angle;
            rebasePending = false;
            return 0f;
        }

        float delta = Mathf.DeltaAngle(lastAngle, angle);
        if (Mathf.Abs(delta) < deadzoneDeg) return 0f;
        lastAngle = angle;
        AccumulatedDeg += delta;
        return delta;
    }

    public void Reset(float accumulatedDeg = 0f)
    {
        AccumulatedDeg = accumulatedDeg;
    }

    private float AngleTo(Vector2 cursor)
    {
        Vector2 v = cursor - center;
        return Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
    }
}

/// Маска загрязнения: grid N×N поверх прямоугольника в мировых координатах.
public class WipeGrid
{
    private readonly bool[,] dirty;
    private readonly int n;
    private Rect worldRect;
    private int remaining;
    private readonly int total;

    /// (ix, iy) очищенной ячейки — для снятия визуала.
    public System.Action<int, int> CellCleared;

    public WipeGrid(int gridSize, Rect rect)
    {
        n = Mathf.Max(1, gridSize);
        worldRect = rect;
        dirty = new bool[n, n];
        for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
                dirty[x, y] = true;
        total = n * n;
        remaining = total;
    }

    public int GridSize => n;
    public Vector2 CellSize => new Vector2(worldRect.width / n, worldRect.height / n);
    public float CleanFraction => total == 0 ? 1f : 1f - (float)remaining / total;

    /// Сдвинуть прямоугольник (когда предмет переехал на столе).
    public void MoveTo(Rect rect)
    {
        worldRect = rect;
    }

    public Vector2 CellCenter(int ix, int iy)
    {
        var cell = CellSize;
        return new Vector2(
            worldRect.xMin + cell.x * (ix + 0.5f),
            worldRect.yMin + cell.y * (iy + 0.5f));
    }

    public bool IsDirty(int ix, int iy) => dirty[ix, iy];

    /// Протирка вдоль отрезка (интерполяция между кадрами — быстрые мазки
    /// не пропускают ячейки). Возвращает число очищенных.
    public int WipeSegment(Vector2 from, Vector2 to, float radius)
    {
        float step = Mathf.Max(0.01f, radius * 0.75f);
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / step));
        int cleared = 0;
        for (int i = 0; i <= steps; i++)
            cleared += WipeAt(Vector2.Lerp(from, to, (float)i / steps), radius);
        return cleared;
    }

    /// Заранее очищает count ячеек — восстановление частичного прогресса
    /// после повторного входа в сцену.
    public int PreClear(int count)
    {
        int cleared = 0;
        for (int x = 0; x < n && cleared < count; x++)
        {
            for (int y = 0; y < n && cleared < count; y++)
            {
                if (!dirty[x, y]) continue;
                dirty[x, y] = false;
                remaining--;
                cleared++;
                CellCleared?.Invoke(x, y);
            }
        }
        return cleared;
    }

    /// Очищает ячейки, чьи центры попали в радиус кисти. Возвращает число очищенных.
    public int WipeAt(Vector2 point, float radius)
    {
        float r2 = radius * radius;
        int cleared = 0;
        for (int x = 0; x < n; x++)
        {
            for (int y = 0; y < n; y++)
            {
                if (!dirty[x, y]) continue;
                if ((CellCenter(x, y) - point).sqrMagnitude > r2) continue;
                dirty[x, y] = false;
                remaining--;
                cleared++;
                CellCleared?.Invoke(x, y);
            }
        }
        return cleared;
    }
}

/// Качание: смена знака дельты по оси; ход ≥ минимальной амплитуды.
/// Одно «качание» = два хода (туда-обратно).
public class RockTracker
{
    private readonly float minAmplitude;
    private float last;
    private float anchor;
    private int direction;
    private bool armed;
    private bool hasLast;

    /// Завершённые ходы (в одну сторону).
    public int Strokes { get; private set; }
    /// Полные качания туда-обратно.
    public int Rocks => Strokes / 2;

    public RockTracker(float minAmplitude)
    {
        this.minAmplitude = Mathf.Max(0.01f, minAmplitude);
    }

    public void Step(float axisPosition)
    {
        if (!hasLast)
        {
            last = axisPosition;
            anchor = axisPosition;
            hasLast = true;
            return;
        }

        float delta = axisPosition - last;
        if (Mathf.Abs(delta) > 0.0001f)
        {
            int sign = delta > 0f ? 1 : -1;
            if (direction == 0)
            {
                direction = sign;
            }
            else if (sign != direction)
            {
                // Разворот: ход засчитан, если размах был достаточный.
                if (armed) Strokes++;
                direction = sign;
                anchor = last;
                armed = false;
            }
            if (Mathf.Abs(axisPosition - anchor) >= minAmplitude)
                armed = true;
        }
        last = axisPosition;
    }

    /// Вызывать при отпускании кнопки: досчитывает последний ход.
    public void Finish()
    {
        if (armed) Strokes++;
        armed = false;
        direction = 0;
        hasLast = false;
    }

    public void Reset()
    {
        Strokes = 0;
        armed = false;
        direction = 0;
        hasLast = false;
    }
}
