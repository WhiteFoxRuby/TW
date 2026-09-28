using System.Collections.Generic;

/// Генератор экземпляра предмета с 2-3 случайными дефектами (этап 1).
public static class ItemInstanceGenerator
{
    /// defectBias -1..1 — ручка режиссёра: >0 склоняет к минимуму дефектов
    /// (щедрый рынок), <0 — к максимуму. 0 = равномерно.
    public static ItemInstance Generate(ItemDefinition definition, System.Random rng, float defectBias = 0f)
    {
        var instance = new ItemInstance { Definition = definition };

        // Кандидаты: пары (модуль, дефект); каждый тип дефекта — не больше одного раза.
        var candidates = new List<(ModuleDefinition module, DefectDefinition defect)>();
        foreach (var module in definition.modules)
        {
            var mi = new ModuleInstance { Definition = module };
            instance.Modules.Add(mi);
            foreach (var defect in module.possibleDefects)
                candidates.Add((module, defect));
        }

        int want;
        if (defectBias > 0f && rng.NextDouble() < defectBias)
            want = definition.minDefects;
        else if (defectBias < 0f && rng.NextDouble() < -defectBias)
            want = definition.maxDefects;
        else
            want = rng.Next(definition.minDefects, definition.maxDefects + 1);
        if (want > candidates.Count) want = candidates.Count;

        // Fisher-Yates и берём первые want.
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        for (int i = 0; i < want; i++)
        {
            var (module, defect) = candidates[i];
            var mi = instance.Modules.Find(m => m.Definition == module);
            mi.Defects.Add(new DefectInstance
            {
                Definition = defect,
                Revealed = defect.visibility == DefectVisibility.Immediate,
            });
        }

        return instance;
    }

    /// Рыночная цена: хуже состояние — дешевле, с небольшим разбросом.
    public static int MarketPrice(ItemInstance item, System.Random rng)
    {
        var def = item.Definition;
        int span = def.marketPriceMax - def.marketPriceMin;
        float t = def.maxDefects <= def.minDefects
            ? 0.5f
            : (float)(item.TotalDefects - def.minDefects) / (def.maxDefects - def.minDefects);
        int price = def.marketPriceMax - (int)(span * t);
        price += rng.Next(-2, 3);
        if (price < def.marketPriceMin) price = def.marketPriceMin;
        if (price > def.marketPriceMax) price = def.marketPriceMax;
        return price;
    }
}
