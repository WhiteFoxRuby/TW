using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class InteractionSceneController : MonoBehaviour
{
    [SerializeField] private Color backgroundColor = new Color(0.2f, 0.2f, 0.5f);
    [SerializeField] private string labelText = "Interaction Scene";

    private void Awake()
    {
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
        cam.backgroundColor = backgroundColor;

        SpriteUtils.MakeSprite("Background", null, new Vector3(0, 0, 1),
            new Vector2(20, 12), backgroundColor, -10);

        SpriteUtils.MakeText("Label", null, new Vector3(0, 1, 0),
            labelText, 48, 0.2f, Color.white, 5);

        SpriteUtils.MakeText("Hint", null, new Vector3(0, -1.5f, 0),
            "press Q to return", 32, 0.15f, new Color(1f, 1f, 1f, 0.7f), 5);
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.qKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene("SampleScene");
        }
    }
}
