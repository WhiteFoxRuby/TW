using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Тестовая сцена этапа 1: кнопка «сгенерировать» печатает в консоль
/// состав фонаря с дефектами. Q — в хаб.
public class DataTestController : MonoBehaviour
{
    private Text output;
    private int counter;

    private void Awake()
    {
        GameState.EnsureInit();

        var cam = Camera.main;
        if (cam == null)
        {
            var camObj = new GameObject("Camera");
            cam = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
            camObj.tag = "MainCamera";
        }
        cam.orthographic = true;
        cam.orthographicSize = 5;
        cam.transform.position = new Vector3(0, 0, -10);
        cam.backgroundColor = new Color(0.1f, 0.14f, 0.12f);

        var canvas = UiBuilder.CreateCanvas("DataTestCanvas", 10);

        UiBuilder.CreateText("Title", canvas.transform,
            "DataTest — генератор экземпляров фонаря (этап 1). Q — в хаб.", 24,
            Color.white, TextAnchor.MiddleCenter,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -60), new Vector2(0, -12));

        UiBuilder.CreateButton("Generate", canvas.transform, "Сгенерировать фонарь",
            Generate, new Color(0.3f, 0.45f, 0.25f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-140, -120), new Vector2(140, -70));

        output = UiBuilder.CreateText("Output", canvas.transform,
            "Нажми кнопку — состав экземпляра появится здесь и в консоли.", 20,
            new Color(0.85f, 0.95f, 0.85f), TextAnchor.UpperLeft,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-400, 20), new Vector2(400, -140));
    }

    private void Generate()
    {
        counter++;
        var item = ItemInstanceGenerator.Generate(GameState.LanternDefinition, GameState.Rng);
        string text = $"Генерация #{counter}\n{item.Describe()}";
        Debug.Log(text);
        output.text = text;
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.qKey.wasPressedThisFrame)
            SceneManager.LoadScene("SampleScene");
    }
}
