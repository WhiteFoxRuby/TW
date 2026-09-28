---
tags: [gdd, mechanic, system, mvp]
related: [[Systems/Repair]], [[Systems/Economy]], [[Reference/Architecture]], [[Reference/Cheat Sheet]]
---

# Items and Defects — предметы и дефекты: модель данных и фонарь среза

> [!summary]
> Предмет описан данными: `ItemDefinition` → список `ModuleDefinition` → допустимые `DefectDefinition`, у дефекта — где виден, чем и каким жестом чинится, какой симптом даёт на проверке. Runtime-экземпляр `ItemInstance` хранит модули с дефектами и их прогресс, ошибки жестов и итоговое качество. В срезе один предмет — **карманный фонарь**: 5 модулей, 5 дефектов в пуле, 2–3 случайных на экземпляр.

## Модель данных (ScriptableObject, `Assets/Data/Resources/`)

```csharp
public class ItemDefinition : ScriptableObject
{
    public string id, displayName;
    public List<ModuleDefinition> modules;
    public int baseRepairedPrice = 60;          // цена починенного при качестве 50
    public int marketPriceMin = 15, marketPriceMax = 25;
    public int minDefects = 2, maxDefects = 3;
}

public class ModuleDefinition : ScriptableObject
{
    public string id, displayName;
    public List<DefectDefinition> possibleDefects;
    public Color color; public Vector2 size, explodedOffset;   // плейсхолдер-визуал
}

public enum DefectVisibility { Immediate, Magnifier, AfterDisassembly }

public class DefectDefinition : ScriptableObject
{
    public string id, displayName;
    public DefectVisibility visibility;
    public ToolDefinition tool;        // null = замена детали из запаса (drag без инструмента)
    public GestureKind gesture;
    public string symptom;             // пустая = не влияет на работоспособность, только на цену
    public bool blocksFunction = true;
    public int symptomPriority = 10;   // меньше = показывается раньше при нескольких дефектах
    public string marketNote;          // как выглядит в списке на рынке (для видимых сразу)
}
```

Runtime:

```csharp
public class ItemInstance
{
    public ItemDefinition Definition;
    public List<ModuleInstance> Modules;
    public int OvertightenCount, ScratchCount;   // ошибки жестов → штраф качества
    public bool RepairCompleted; public int Quality;
    public bool Hum;                             // гудящий предмет — зацепка режиссёра
    public DefectInstance FirstSymptom();        // по symptomPriority среди blocksFunction
    public int ComputeQuality(EconomyConfig cfg);
    public int FairPrice(EconomyConfig cfg);
}

public class DefectInstance
{
    public DefectDefinition Definition;
    public float Progress;      // 0..1; 1 = устранён
    public bool Revealed;       // игрок узнал о дефекте (лупа / разборка)
}
```

## Фонарь среза

`Lantern.asset`: id `lantern`, «Карманный фонарь», `baseRepairedPrice 60`, рынок 15–25, дефектов 2–3.

### Модули

| Модуль | id | Возможные дефекты |
|---|---|---|
| Корпус | `body` | грязный корпус |
| Линза | `lens` | треснутая линза |
| Лампа и контакты | `lamp` | — (без дефектов в срезе) |
| Батарейный отсек | `battery_bay` | окисленные контакты, севшие батарейки |
| Кнопка | `button` | погнутая кнопка |

### Дефекты фонаря

| Дефект | id | Модуль | Виден | Инструмент → жест | Симптом на проверке | Приоритет | Блокирует |
|---|---|---|---|---|---|---|---|
| Грязный корпус | `dirty_body` | корпус | сразу («корпус в грязи») | тряпка → протирка | — (только цена) | 99 | нет |
| Погнутая кнопка | `bent_button` | кнопка | сразу («кнопка перекошена») | плоскогубцы → клик + drag | «кнопка не нажимается» | **1** | да |
| Окисленные контакты | `oxidized_contacts` | отсек | после разборки | наждачка → качание | «не включается» | 2 | да |
| Севшие батарейки | `dead_batteries` | отсек | после разборки | замена из запаса → drag | «не включается» | 2 | да |
| Треснутая линза | `cracked_lens` | линза | только лупой | замена из запаса → drag | «светит тускло» | 3 | да |

Порядок симптомов при нескольких дефектах: **кнопка → питание → линза**. Для линзы луч рисуется тусклым (`ShowBeam(1)`), для остальных — нет луча.

Запасные линзы и батарейки лежат в ящике на столе, бесплатны и бесконечны (упрощение среза).

## Генерация экземпляра

`ItemInstanceGenerator.Generate(definition, rng, defectBias)`:

1. Кандидаты — все пары (модуль, дефект); каждый тип дефекта не больше одного раза.
2. Сколько дефектов: `defectBias > 0` с вероятностью `bias` даёт `minDefects` (щедрый рынок), `< 0` — `maxDefects`, иначе равномерно `[min, max]`.
3. Fisher–Yates по кандидатам, берём первые `want`.
4. Дефекты с `visibility == Immediate` сразу `Revealed`.

Приёмка этапа 1: 20 генераций дают разные комбинации (проверяется `DevChecks`).

### Рыночная цена

Хуже состояние — дешевле, с разбросом:

```
t = (TotalDefects − minDefects) / (maxDefects − minDefects)
price = marketPriceMax − span·t + rand(−2..2), clamp [min, max]
затем режиссёр: price = max(marketPriceFloor=8, price − generosity·priceDiscountAtFullGenerosity)
```

Для фонаря: 2 дефекта → ~25, 3 дефекта → ~15.

## Качество и цена

### Качество 0–100

```
defectScore = FixedDefects / TotalDefects                       → × 40 (qualityPointsDefects)
cleanliness = Progress дефекта "dirty_body" (нет дефекта → 1)   → × 30 (qualityPointsClean)
careScore   = max(0, 30 − (Overtighten + Scratch) × 10)          (qualityPointsCare, carePenaltyPerMistake)
Quality = round(defectScore·40 + cleanliness·30 + careScore), clamp 0..100
```

Считается при **«Завершить ремонт»** (E) после пройденной проверки. Пройденная проверка обязательна для завершения; недочиненный фонарь завершить нельзя.

### Честная цена

```
mod = lerp(qualityModMin=0.7, qualityModMax=1.3, Quality/100)
FairPrice = round(baseRepairedPrice=60 × mod)                     → 42 … 78
× SaleFairMultiplier: гудящий (Hum) с Quality ≥ 80 → ×1.15
```

Реакция покупателя на предложенную цену — по отношению к честной ([[Systems/Economy#Прилавок в срезе]]).

> [!note] Баланс — мягче задуманного
> 30 очков аккуратности выдаются авансом (снимаются только за перекруты и царапины), а фонарь без дефекта грязи получает 30 очков чистоты даром. Починенный, но грязный фонарь даёт качество ~50 → цена 60 при покупке за 15–25. Не баг, но маржа на плейтесте окажется мягче ([[Reference/Roadmap#Мелочи]]).

## Ошибки жестов

| Ошибка | Когда | Счётчик |
|---|---|---|
| **Перекрут** | затянуть винт больше `screwTurnsToRemove + overtightenExtraTurns` оборотов CW; один штраф на закручивание | `OvertightenCount` |
| **Царапина** | наждачка по уже зачищенным контактам / по чистому месту | `ScratchCount` |

Каждая пишется в телеметрию `GestureError` и в фрустрацию режиссёра ([[Director/Director#Индексы]]).

## Известный технический долг

- **Зашитые строковые id.** В верстаке 62 строковых литерала (`"dirty_body"`, `"battery_bay"`, `"cracked_lens"`, `"bent_button"`…): `LanternView` — 30, `WorkbenchController` — 18, `GameState.FallbackData` — 10, `ItemInstance` — 1. Плюс статические словари `AssembledOffsets` и `ScrewHoleOffsets` под конкретный фонарь.
- `ItemInstance.CleanlinessFraction()` ищет дефект ровно с id `dirty_body`, иначе возвращает 1 — любой будущий предмет без такого дефекта получает все 30 очков чистоты даром.
- Для среза с одним фонарём — осознанно. Но **второй предмет** (ожерелье, чайник, радио) потребует переписывания обоих файлов верстака, а не добавления ассета. Зафиксировано в [[Reference/Roadmap#Существенно]].

## Расширение за срезом

Модульная структура задумана на все категории ([[Systems/Repair#Категории предметов и типы поломок]]): состояния модуля исправен / повреждён / отсутствует / заражён; типизированные слоты инвентаря по категориям (электроника, украшения, инструменты); каждый найденный предмет уникален и попадает в базу блокнота.
