using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// Точка взаимодействия в хабе: по E либо загружает сцену, либо вызывает колбэк.
public class InteractivePoint : MonoBehaviour
{
    private string targetSceneName;
    private Action callback;
    private string label = "нажми E";
    private float interactionRadius = 0.75f;

    private Transform player;
    private GameObject bubble;
    private bool playerInRange;

    public void Init(string sceneName, float radius = 0.75f)
    {
        targetSceneName = sceneName;
        interactionRadius = radius;
    }

    public void Init(string sceneName, string labelText, float radius = 0.75f)
    {
        targetSceneName = sceneName;
        label = labelText;
        interactionRadius = radius;
    }

    public void InitCallback(Action onInteract, float radius = 0.75f)
    {
        callback = onInteract;
        interactionRadius = radius;
    }

    /// Диамант-визуал + компонент; label показывается в пузыре.
    public static InteractivePoint Create(string name, Vector3 position, Color color, string label)
    {
        var obj = new GameObject(name);
        obj.transform.position = position;

        var visual = new GameObject("Visual");
        visual.transform.SetParent(obj.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
        visual.transform.localRotation = Quaternion.Euler(0, 0, 45);
        var sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteUtils.White;
        sr.color = color;
        sr.sortingOrder = 3;

        var ip = obj.AddComponent<InteractivePoint>();
        ip.label = label;
        return ip;
    }

    private void Start()
    {
        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;

        CreateBubble();
        bubble.SetActive(false);
    }

    private void Update()
    {
        if (player == null) return;

        if (HubController.OverlayOpen)
        {
            if (playerInRange)
            {
                playerInRange = false;
                bubble.SetActive(false);
            }
            return;
        }

        float dist = Vector2.Distance(
            new Vector2(player.position.x, player.position.y),
            new Vector2(transform.position.x, transform.position.y));

        bool inRange = dist <= interactionRadius;

        if (inRange != playerInRange)
        {
            playerInRange = inRange;
            bubble.SetActive(playerInRange);
        }

        if (playerInRange)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                if (callback != null)
                {
                    callback();
                }
                else if (!string.IsNullOrEmpty(targetSceneName))
                {
                    SceneCarry.PlayerX = player.position.x;
                    SceneManager.LoadScene(targetSceneName);
                }
            }
        }
    }

    private void CreateBubble()
    {
        bubble = new GameObject("Bubble");
        bubble.transform.SetParent(transform);
        bubble.transform.localPosition = new Vector3(0f, 2f, 0f);
        bubble.transform.localRotation = Quaternion.identity;

        float width = Mathf.Max(2.5f, label.Length * 0.32f);
        var bg = SpriteUtils.MakeSprite("BubbleBG", bubble.transform, Vector3.zero,
            new Vector2(width, 0.8f), new Color(0f, 0f, 0f, 0.75f), 9);
        bg.name = "BubbleBG";

        SpriteUtils.MakeText("BubbleText", bubble.transform, new Vector3(0f, 0f, -0.1f),
            label, 32, 0.15f, Color.white, 10);
    }
}
