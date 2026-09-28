using UnityEngine;

/// Общие помощники для плейсхолдер-графики. Единственное место,
/// где создаётся белый спрайт (раньше дублировался в четырёх скриптах и тёк).
public static class SpriteUtils
{
    private static Sprite whiteSprite;

    public static Sprite White
    {
        get
        {
            if (whiteSprite == null)
            {
                Texture2D tex = new Texture2D(4, 4);
                Color[] pixels = new Color[16];
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = Color.white;
                tex.SetPixels(pixels);
                tex.Apply();
                tex.filterMode = FilterMode.Point;
                whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
            }
            return whiteSprite;
        }
    }

    public static SpriteRenderer EnsureSprite(GameObject obj, Color color, int sortingOrder)
    {
        var sr = obj.GetComponent<SpriteRenderer>();
        if (sr == null)
            sr = obj.AddComponent<SpriteRenderer>();

        if (sr.sprite == null)
            sr.sprite = White;

        sr.color = color;
        sr.sortingOrder = sortingOrder;
        return sr;
    }

    /// Прямоугольник из белого спрайта. size задаётся масштабом.
    public static GameObject MakeSprite(string name, Transform parent, Vector3 localPosition,
        Vector2 size, Color color, int sortingOrder)
    {
        var obj = new GameObject(name);
        if (parent != null)
            obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPosition;
        obj.transform.localScale = new Vector3(size.x, size.y, 1f);
        EnsureSprite(obj, color, sortingOrder);
        return obj;
    }

    /// Ищет объект по имени и создаёт, если его нет (паттерн генерации хаба).
    public static void FindOrCreateSceneObject(string name, Vector3 position, Vector3 scale,
        Color color, int sortingOrder)
    {
        if (GameObject.Find(name) != null) return;

        var obj = new GameObject(name);
        obj.transform.position = position;
        obj.transform.localScale = scale;
        EnsureSprite(obj, color, sortingOrder);
    }

    public static TextMesh MakeText(string name, Transform parent, Vector3 localPosition,
        string text, int fontSize, float characterSize, Color color, int sortingOrder)
    {
        var obj = new GameObject(name);
        if (parent != null)
            obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPosition;
        var tm = obj.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = fontSize;
        tm.characterSize = characterSize;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        var mr = obj.GetComponent<MeshRenderer>();
        mr.sortingOrder = sortingOrder;
        return tm;
    }
}
