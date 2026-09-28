using UnityEngine;

/// Генерация хаба (фон, земля, ориентиры, интерактивные точки).
/// Вынесена из PlayerController по mvp-scope §5.
public class LevelBuilder : MonoBehaviour
{
    /// Полуширина уровня; 0, пока билдер не отработал.
    public static float LevelHalfWidth { get; private set; }

    private void Awake()
    {
        var cam = Camera.main;
        float cameraHalfWidth = cam.orthographicSize * cam.aspect;
        float screenWidth = cameraHalfWidth * 2f;
        LevelHalfWidth = screenWidth * 2f;

        if (cam.GetComponent<CameraFollow>() == null)
            cam.gameObject.AddComponent<CameraFollow>();

        var player = GameObject.FindWithTag("Player");
        if (player != null)
            SpriteUtils.EnsureSprite(player, new Color(0.9f, 0.2f, 0.2f), 5);

        float levelWidth = LevelHalfWidth * 2f + 10f; // extra margin

        SpriteUtils.FindOrCreateSceneObject("Background", new Vector3(0f, 0f, 1f),
            new Vector3(levelWidth, 12f, 1f), new Color(0.53f, 0.81f, 0.92f), -10);
        SpriteUtils.FindOrCreateSceneObject("Ground", new Vector3(0f, -3.5f, 0f),
            new Vector3(levelWidth, 3f, 1f), new Color(0.35f, 0.55f, 0.25f), -5);

        SpriteUtils.FindOrCreateSceneObject("Tree1", new Vector3(3f, -0.5f, 0f),
            new Vector3(1.5f, 3f, 1f), new Color(0.18f, 0.42f, 0.15f), -1);
        SpriteUtils.FindOrCreateSceneObject("Tree2", new Vector3(-5f, -0.5f, 0f),
            new Vector3(1.2f, 2.5f, 1f), new Color(0.22f, 0.50f, 0.18f), -1);
        SpriteUtils.FindOrCreateSceneObject("Tree3", new Vector3(12f, -0.5f, 0f),
            new Vector3(1.8f, 3.5f, 1f), new Color(0.15f, 0.38f, 0.12f), -1);
        SpriteUtils.FindOrCreateSceneObject("Rock1", new Vector3(-15f, -1.5f, 0f),
            new Vector3(2f, 1f, 1f), new Color(0.5f, 0.5f, 0.5f), -1);
        SpriteUtils.FindOrCreateSceneObject("Tree4", new Vector3(22f, -0.5f, 0f),
            new Vector3(1.5f, 3f, 1f), new Color(0.2f, 0.45f, 0.16f), -1);
        SpriteUtils.FindOrCreateSceneObject("Rock2", new Vector3(-25f, -1.5f, 0f),
            new Vector3(1.5f, 1.2f, 1f), new Color(0.45f, 0.45f, 0.45f), -1);
        SpriteUtils.FindOrCreateSceneObject("Tree5", new Vector3(30f, -0.5f, 0f),
            new Vector3(1.3f, 2.8f, 1f), new Color(0.17f, 0.40f, 0.14f), -1);

        CreatePoints();
    }

    private void CreatePoints()
    {
        var market = InteractivePoint.Create("MarketPoint", new Vector3(-8f, -1f, 0f),
            new Color(1f, 0.9f, 0.2f), "Рынок — E");
        market.InitCallback(() => HubController.Instance.OpenMarket());

        var bench = InteractivePoint.Create("WorkbenchPoint", new Vector3(8f, -1f, 0f),
            new Color(0.9f, 0.55f, 0.2f), "Верстак — E");
        bench.InitCallback(() => HubController.Instance.TryEnterWorkbench());

        var counter = InteractivePoint.Create("CounterPoint", new Vector3(25f, -1f, 0f),
            new Color(0.4f, 0.85f, 0.9f), "Прилавок — E");
        counter.InitCallback(() => HubController.Instance.OpenCounter());
    }
}
