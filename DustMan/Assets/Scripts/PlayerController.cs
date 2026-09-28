using UnityEngine;
using UnityEngine.InputSystem;

/// Только движение игрока. Генерация уровня — в LevelBuilder.
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private InputAction moveAction;
    private bool ownsMoveAction;
    private float levelHalfWidth;

    private void Awake()
    {
        var actions = InputSystem.actions;
        if (actions != null)
            moveAction = actions.FindAction("Player/Move", false);

        if (moveAction == null)
        {
            // Запасной вариант, если project-wide Input Actions недоступны.
            moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            ownsMoveAction = true;
        }

        moveAction.Enable();
    }

    private void OnDestroy()
    {
        if (ownsMoveAction)
            moveAction.Dispose();
    }

    private void Start()
    {
        levelHalfWidth = LevelBuilder.LevelHalfWidth;
        if (levelHalfWidth <= 0f)
        {
            var cam = Camera.main;
            levelHalfWidth = cam.orthographicSize * cam.aspect * 4f;
        }

        if (SceneCarry.PlayerX.HasValue)
        {
            Vector3 pos = transform.position;
            pos.x = SceneCarry.PlayerX.Value;
            transform.position = pos;
            SceneCarry.PlayerX = null;
        }
    }

    private void Update()
    {
        float input = moveAction.ReadValue<Vector2>().x;

        if (Mathf.Abs(input) > 0.01f)
        {
            transform.Translate(Vector3.right * input * moveSpeed * Time.deltaTime);

            Vector3 scale = transform.localScale;
            scale.x = Mathf.Sign(input) * Mathf.Abs(scale.x);
            transform.localScale = scale;
        }

        // Invisible walls — clamp player position
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, -levelHalfWidth, levelHalfWidth);
        transform.position = pos;
    }
}
