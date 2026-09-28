using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    private Transform player;

    private float levelHalfWidth;
    private float cameraHalfWidth;

    private void Start()
    {
        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;

        var cam = GetComponent<Camera>();
        cameraHalfWidth = cam.orthographicSize * cam.aspect;

        levelHalfWidth = LevelBuilder.LevelHalfWidth;
        if (levelHalfWidth <= 0f)
        {
            // Level = 2 screen widths in each direction
            float screenWidth = cameraHalfWidth * 2f;
            levelHalfWidth = screenWidth * 2f;
        }
    }

    private void LateUpdate()
    {
        if (player == null) return;

        float minX = -levelHalfWidth + cameraHalfWidth;
        float maxX = levelHalfWidth - cameraHalfWidth;

        float targetX = Mathf.Clamp(player.position.x, minX, maxX);

        Vector3 pos = transform.position;
        pos.x = targetX;
        transform.position = pos;
    }
}
