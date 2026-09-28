using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// Runtime-экземпляр предмета: модули, дефекты, прогресс ремонта.
public class ItemInstance
{
    public ItemDefinition Definition;
    public List<ModuleInstance> Modules = new List<ModuleInstance>();

    /// Ошибки жестов, штрафующие качество.
    public int OvertightenCount;
    public int ScratchCount;

    /// Выставляется при «Завершить ремонт» на верстаке.
    public bool RepairCompleted;
    public int Quality;

    /// «Гудящий» предмет — зацепка от режиссёра (плейсхолдер сюжетного слоя).
    public bool Hum;

    public IEnumerable<DefectInstance> AllDefects()
    {
        foreach (var m in Modules)
            foreach (var d in m.Defects)
                yield return d;
    }

    public int TotalDefects
    {
        get
        {
            int n = 0;
            foreach (var d in AllDefects()) n++;
            return n;
        }
    }

    public int FixedDefects
    {
        get
        {
            int n = 0;
            foreach (var d in AllDefects())
                if (d.IsFixed) n++;
            return n;
        }
    }

    public DefectInstance FindDefect(string defectId)
    {
        foreach (var d in AllDefects())
            if (d.Definition.id == defectId)
                return d;
        return null;
    }

    public bool HasActiveDefect(string defectId)
    {
        var d = FindDefect(defectId);
        return d != null && !d.IsFixed;
    }

    /// Дефекты, из-за которых предмет не работает на проверке.
    public List<DefectInstance> BlockingDefectsRemaining()
    {
        var list = new List<DefectInstance>();
        foreach (var d in AllDefects())
            if (!d.IsFixed && d.Definition.blocksFunction)
                list.Add(d);
        return list;
    }

    /// Симптом с наивысшим приоритетом среди недочиненных дефектов; null = работает.
    public DefectInstance FirstSymptom()
    {
        DefectInstance best = null;
        foreach (var d in BlockingDefectsRemaining())
            if (best == null || d.Definition.symptomPriority < best.Definition.symptomPriority)
                best = d;
        return best;
    }

    /// 0..1: насколько предмет чист. Без дефекта грязи — всегда 1.
    public float CleanlinessFraction()
    {
        var dirt = FindDefect("dirty_body");
        if (dirt == null) return 1f;
        return Mathf.Clamp01(dirt.Progress);
    }

    /// Качество 0-100: дефекты + чистота + аккуратность (mvp-scope §4).
    public int ComputeQuality(EconomyConfig cfg)
    {
        int total = TotalDefects;
        float defectScore = total == 0 ? 1f : (float)FixedDefects / total;
        float careScore = Mathf.Max(0,
            cfg.qualityPointsCare - (OvertightenCount + ScratchCount) * cfg.carePenaltyPerMistake);

        float q = defectScore * cfg.qualityPointsDefects
                + CleanlinessFraction() * cfg.qualityPointsClean
                + careScore;
        return Mathf.Clamp(Mathf.RoundToInt(q), 0, 100);
    }

    /// Честная цена продажи по качеству.
    public int FairPrice(EconomyConfig cfg)
    {
        float mod = Mathf.Lerp(cfg.qualityModMin, cfg.qualityModMax, Quality / 100f);
        return Mathf.RoundToInt(Definition.baseRepairedPrice * mod);
    }

    /// Видимые сразу дефекты — для описания на рынке.
    public string VisibleConditionNote()
    {
        var notes = new List<string>();
        foreach (var d in AllDefects())
            if (!d.IsFixed && d.Definition.visibility == DefectVisibility.Immediate
                && !string.IsNullOrEmpty(d.Definition.marketNote))
                notes.Add(d.Definition.marketNote);
        if (notes.Count == 0) return "на вид целый";
        return string.Join(", ", notes);
    }

    public string Describe()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{Definition.displayName} — дефектов: {TotalDefects}");
        foreach (var m in Modules)
        {
            if (m.Defects.Count == 0) continue;
            foreach (var d in m.Defects)
            {
                string vis = d.Definition.visibility switch
                {
                    DefectVisibility.Immediate => "виден сразу",
                    DefectVisibility.Magnifier => "виден лупой",
                    _ => "виден после разборки",
                };
                string tool = d.Definition.tool != null ? d.Definition.tool.displayName : "замена из запаса";
                sb.AppendLine($"  [{m.Definition.displayName}] {d.Definition.displayName} ({vis}; {tool})");
            }
        }
        return sb.ToString().TrimEnd();
    }
}

public class ModuleInstance
{
    public ModuleDefinition Definition;
    public List<DefectInstance> Defects = new List<DefectInstance>();
}

public class DefectInstance
{
    public DefectDefinition Definition;
    /// 0..1; 1 = устранён.
    public float Progress;
    /// Игрок узнал о дефекте (лупа/разборка) — для журналирования и UI.
    public bool Revealed;

    public bool IsFixed => Progress >= 1f;

    public void MarkFixed()
    {
        Progress = 1f;
    }
}
