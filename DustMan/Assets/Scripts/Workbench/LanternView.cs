using System.Collections.Generic;
using UnityEngine;

public enum ScrewState
{
    Tightened, // закручен в корпус
    Seated,    // вставлен в отверстие, но не затянут
    Removed,   // лежит на столе
}

public class ScrewView
{
    public int Index;
    public GameObject Go;
    public ScrewState State = ScrewState.Tightened;
    public RotationTracker Tracker = new RotationTracker();
    public Vector2 TablePos;
    /// Штраф за перекрут уже выдан в текущем закручивании.
    public bool PenaltyGiven;
}

/// Визуал фонаря на верстаке: собранный/разобранный вид, оверлеи дефектов,
/// винты, луч света. Логика взаимодействий — в WorkbenchController.
public class LanternView : MonoBehaviour
{
    public ItemInstance Item { get; private set; }
    public GestureConfig Config { get; private set; }
    public bool Exploded { get; private set; }
    public readonly List<ScrewView> Screws = new List<ScrewView>();
    public WipeGrid DirtGrid { get; private set; }

    private Transform root;
    private readonly Dictionary<string, Transform> moduleContainers = new Dictionary<string, Transform>();
    private readonly List<TextMesh> explodedLabels = new List<TextMesh>();

    private GameObject beamGo;
    private SpriteRenderer beamSr;
    private readonly List<GameObject> crackLines = new List<GameObject>();
    private GameObject oxideGo;
    private readonly List<SpriteRenderer> batterySrs = new List<SpriteRenderer>();
    private Transform buttonContainer;
    private GameObject buttonTargetGo;

    private static readonly Vector2[] ScrewHoleOffsets =
    {
        new Vector2(-0.7f, -0.35f),
        new Vector2(0.5f, -0.35f),
    };

    private static readonly Dictionary<string, Vector2> AssembledOffsets = new Dictionary<string, Vector2>
    {
        { "body", new Vector2(0f, 0f) },
        { "lens", new Vector2(1.55f, 0f) },
        { "lamp", new Vector2(0.6f, 0f) },        // внутри корпуса, скрыта
        { "battery_bay", new Vector2(-0.4f, 0f) }, // внутри корпуса, скрыт
        { "button", new Vector2(0f, 0.7f) },
    };

    public void Build(ItemInstance item, GestureConfig config)
    {
        Item = item;
        Config = config;

        var rootGo = new GameObject("LanternRoot");
        root = rootGo.transform;
        root.SetParent(transform, false);
        root.position = new Vector3(0f, 0.5f, 0f);

        foreach (var module in item.Modules)
        {
            var def = module.Definition;
            var container = new GameObject($"Module_{def.id}").transform;
            container.SetParent(root, false);
            moduleContainers[def.id] = container;

            SpriteUtils.MakeSprite("Sprite", container, Vector3.zero, def.size, def.color, 5);

            var label = SpriteUtils.MakeText("Label", container,
                new Vector3(0f, -def.size.y * 0.5f - 0.35f, 0f),
                def.displayName, 22, 0.09f, new Color(1f, 1f, 1f, 0.7f), 10);
            label.gameObject.SetActive(false);
            explodedLabels.Add(label);
        }

        buttonContainer = moduleContainers["button"];

        BuildScrews();
        BuildDefectOverlays();
        BuildBeam();
        SetExploded(false);
        RefreshDefectVisuals();
    }

    private void BuildScrews()
    {
        var body = moduleContainers["body"];
        for (int i = 0; i < ScrewHoleOffsets.Length; i++)
        {
            var go = SpriteUtils.MakeSprite($"Screw{i}", body, ScrewHoleOffsets[i],
                new Vector2(0.26f, 0.26f), new Color(0.2f, 0.2f, 0.22f), 8);
            Screws.Add(new ScrewView
            {
                Index = i,
                Go = go,
                TablePos = new Vector2(-5.2f + i * 0.6f, -2.4f),
            });
        }
    }

    private void BuildDefectOverlays()
    {
        // Грязь: маска 8×8 в локальных координатах корпуса.
        if (Item.HasActiveDefect("dirty_body"))
        {
            var body = moduleContainers["body"];
            var bodySize = FindModuleDef("body").size;
            var localRect = new Rect(-bodySize.x * 0.5f, -bodySize.y * 0.5f, bodySize.x, bodySize.y);
            DirtGrid = new WipeGrid(Config.wipeGridSize, localRect);

            var cells = new GameObject[DirtGrid.GridSize, DirtGrid.GridSize];
            for (int x = 0; x < DirtGrid.GridSize; x++)
            {
                for (int y = 0; y < DirtGrid.GridSize; y++)
                {
                    var center = DirtGrid.CellCenter(x, y);
                    var cell = SpriteUtils.MakeSprite($"Dirt_{x}_{y}", body,
                        new Vector3(center.x, center.y, 0f),
                        DirtGrid.CellSize * 0.95f, new Color(0.22f, 0.16f, 0.09f, 0.8f), 6);
                    cells[x, y] = cell;
                }
            }
            DirtGrid.CellCleared = (x, y) =>
            {
                if (cells[x, y] != null) Destroy(cells[x, y]);
            };

            // Восстановить частичный прогресс протирки (возврат в сцену по Q).
            var dirtDefect = Item.FindDefect("dirty_body");
            if (dirtDefect != null && dirtDefect.Progress > 0f)
            {
                int total = DirtGrid.GridSize * DirtGrid.GridSize;
                DirtGrid.PreClear(Mathf.RoundToInt(
                    dirtDefect.Progress * Config.wipeCompleteFraction * total));
            }
        }

        // Трещина на линзе: тонкие тёмные линии, видны после лупы.
        if (Item.FindDefect("cracked_lens") != null)
        {
            var lens = moduleContainers["lens"];
            var l1 = SpriteUtils.MakeSprite("Crack1", lens, new Vector3(-0.05f, 0.08f, 0f),
                new Vector2(0.75f, 0.05f), new Color(0.15f, 0.2f, 0.25f, 0.9f), 7);
            l1.transform.localRotation = Quaternion.Euler(0, 0, 28f);
            var l2 = SpriteUtils.MakeSprite("Crack2", lens, new Vector3(0.12f, -0.1f, 0f),
                new Vector2(0.5f, 0.04f), new Color(0.15f, 0.2f, 0.25f, 0.9f), 7);
            l2.transform.localRotation = Quaternion.Euler(0, 0, -35f);
            crackLines.Add(l1);
            crackLines.Add(l2);
        }

        // Окисление контактов в отсеке.
        if (Item.FindDefect("oxidized_contacts") != null)
        {
            var bay = moduleContainers["battery_bay"];
            oxideGo = SpriteUtils.MakeSprite("Oxide", bay, new Vector3(0f, -0.28f, 0f),
                new Vector2(1.3f, 0.28f), new Color(0.35f, 0.65f, 0.3f, 0.9f), 7);
        }

        // Батарейки в отсеке (видны в разобранном виде).
        var bayContainer = moduleContainers["battery_bay"];
        for (int i = 0; i < 2; i++)
        {
            var b = SpriteUtils.MakeSprite($"Battery{i}", bayContainer,
                new Vector3(-0.35f + i * 0.7f, 0.12f, 0f),
                new Vector2(0.45f, 0.6f), Color.white, 6);
            batterySrs.Add(b.GetComponent<SpriteRenderer>());
        }

        // Цель для выпрямления кнопки (появляется при захвате плоскогубцами).
        buttonTargetGo = SpriteUtils.MakeSprite("ButtonTarget", transform,
            Vector3.zero, new Vector2(0.55f, 0.35f), new Color(1f, 1f, 1f, 0.3f), 4);
        buttonTargetGo.SetActive(false);
    }

    private void BuildBeam()
    {
        beamGo = SpriteUtils.MakeSprite("Beam", root, new Vector3(3.15f, 0f, 0f),
            new Vector2(2.4f, 0.9f), new Color(1f, 0.95f, 0.55f, 0.5f), 3);
        beamSr = beamGo.GetComponent<SpriteRenderer>();
        beamGo.SetActive(false);
    }

    private ModuleDefinition FindModuleDef(string id)
    {
        foreach (var m in Item.Modules)
            if (m.Definition.id == id)
                return m.Definition;
        return null;
    }

    public Transform ModuleContainer(string id) => moduleContainers[id];

    public Vector2 ModuleCenter(string id) => moduleContainers[id].position;

    /// AABB модуля; корректен в разобранном виде (без поворота).
    public Rect ModuleAabb(string id)
    {
        var def = FindModuleDef(id);
        Vector2 c = moduleContainers[id].position;
        return new Rect(c - def.size * 0.5f, def.size);
    }

    public Vector2 HoleWorldPos(int screwIndex)
    {
        return moduleContainers["body"].TransformPoint(ScrewHoleOffsets[screwIndex]);
    }

    public Vector2 ContactsCenter =>
        moduleContainers["battery_bay"].TransformPoint(new Vector3(0f, -0.28f, 0f));

    public void SetExploded(bool value)
    {
        Exploded = value;
        root.rotation = Quaternion.identity;

        foreach (var module in Item.Modules)
        {
            var def = module.Definition;
            var container = moduleContainers[def.id];
            bool hiddenInside = !value && (def.id == "lamp" || def.id == "battery_bay");
            container.gameObject.SetActive(!hiddenInside);
            container.localPosition = value
                ? (Vector3)def.explodedOffset
                : (Vector3)GetAssembledOffset(def.id);
            container.localRotation = Quaternion.identity;
        }

        foreach (var label in explodedLabels)
            label.gameObject.SetActive(value);

        // Винты, оставшиеся в корпусе, следуют за ним автоматически (дети body).
        ShowBeam(0);
        RefreshDefectVisuals();
    }

    private Vector2 GetAssembledOffset(string id)
    {
        return AssembledOffsets.TryGetValue(id, out var offset) ? offset : Vector2.zero;
    }

    /// Поворот собранного фонаря при осмотре.
    public void AddInspectAngle(float deltaDeg)
    {
        if (Exploded) return;
        root.Rotate(0f, 0f, deltaDeg);
    }

    public void ResetInspectAngle()
    {
        root.rotation = Quaternion.identity;
    }

    /// Протирка: конвертирует мировую точку в локальные координаты корпуса.
    public int WipeAtWorld(Vector2 worldPos, float radius)
    {
        if (DirtGrid == null) return 0;
        Vector2 local = moduleContainers["body"].InverseTransformPoint(worldPos);
        return DirtGrid.WipeAt(local, radius);
    }

    /// Протирка вдоль отрезка между кадрами — быстрые мазки не пропускают ячейки.
    public int WipeSegmentWorld(Vector2 fromWorld, Vector2 toWorld, float radius)
    {
        if (DirtGrid == null) return 0;
        var body = moduleContainers["body"];
        Vector2 a = body.InverseTransformPoint(fromWorld);
        Vector2 b = body.InverseTransformPoint(toWorld);
        return DirtGrid.WipeSegment(a, b, radius);
    }

    public void SetScrewParentBody(ScrewView screw)
    {
        screw.Go.transform.SetParent(moduleContainers["body"], false);
        screw.Go.transform.localPosition = ScrewHoleOffsets[screw.Index];
    }

    public void SetScrewOnTable(ScrewView screw)
    {
        screw.Go.transform.SetParent(transform, true);
        screw.Go.transform.position = new Vector3(screw.TablePos.x, screw.TablePos.y, 0f);
        screw.Go.transform.rotation = Quaternion.identity;
    }

    /// 0 — выключен, 1 — тусклый, 2 — полный.
    public void ShowBeam(int state)
    {
        beamGo.SetActive(state > 0 && !Exploded);
        if (state == 1)
            beamSr.color = new Color(1f, 0.95f, 0.55f, 0.13f);
        else if (state == 2)
            beamSr.color = new Color(1f, 0.95f, 0.55f, 0.5f);
    }

    public Vector2 ButtonTargetPos
    {
        get
        {
            var def = FindModuleDef("button");
            Vector2 basePos = Exploded
                ? (Vector2)root.TransformPoint(def.explodedOffset)
                : ModuleCenter("button");
            return basePos + new Vector2(0f, 1.1f);
        }
    }

    public void ShowButtonTarget(bool on)
    {
        buttonTargetGo.SetActive(on);
        if (on)
            buttonTargetGo.transform.position = ButtonTargetPos;
    }

    public void SetButtonDragPos(Vector2 worldPos)
    {
        buttonContainer.position = worldPos;
    }

    public void ResetButtonPos()
    {
        var def = FindModuleDef("button");
        buttonContainer.localPosition = Exploded
            ? (Vector3)def.explodedOffset
            : (Vector3)GetAssembledOffset("button");
    }

    /// Синхронизирует оверлеи дефектов с состоянием экземпляра.
    public void RefreshDefectVisuals()
    {
        var crack = Item.FindDefect("cracked_lens");
        bool crackVisible = crack != null && !crack.IsFixed && crack.Revealed;
        foreach (var line in crackLines)
            line.SetActive(crackVisible);

        var oxide = Item.FindDefect("oxidized_contacts");
        if (oxideGo != null)
            oxideGo.SetActive(oxide != null && !oxide.IsFixed);

        var dead = Item.FindDefect("dead_batteries");
        bool batteriesDead = dead != null && !dead.IsFixed;
        foreach (var sr in batterySrs)
            sr.color = batteriesDead
                ? new Color(0.4f, 0.4f, 0.35f)
                : new Color(0.65f, 0.85f, 0.45f);

        var bent = Item.FindDefect("bent_button");
        bool isBent = bent != null && !bent.IsFixed;
        buttonContainer.localRotation = Quaternion.Euler(0f, 0f, isBent ? 25f : 0f);
    }
}
