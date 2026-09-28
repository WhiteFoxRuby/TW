---
tags: [gdd, mechanic, system, director]
related: [[Director/Neural Director]], [[Director/DirectorBench]], [[Systems/Economy]], [[Reference/Architecture]]
---

# Director — AI-режиссёр: индексы, ручки, блоки

> [!summary]
> Система, которая следит за состоянием игрока и мира и подкручивает **входящий поток контента** — лут, поломки, покупателей, события, зацепки — в духе Director из Left 4 Dead, но для уютного темпа. Управляет потоком, **а не сложностью**. Держит три индекса — **давление, скука, фрустрация** — в коридоре, чтобы игра не скатилась ни в стресс-спираль, ни в монотонный гринд. В коде: `GameDirector` (тик — день), rule-based baseline-политика, валидатор, библиотека блоков, телеметрия и headless-симулятор. Все числа — `DirectorConfig.asset`.

## Раздел `Director/`

| Страница | Что в ней |
|---|---|
| **Director** (эта) | Как режиссёр устроен сейчас: роль, сигналы, индексы с формулами, ручки, ограничители, пайплайн policy → validator, библиотека блоков, телеметрия, симуляция, известные расхождения |
| [[Director/Neural Director\|Neural Director]] | Куда он растёт: GRU/Transformer-политика, обучение в шесть этапов, shadow mode, controlled rollout, тесты и метрики, фазы A–F |
| [[Director/DirectorBench\|DirectorBench]] | Исследовательская рамка той же роли: компетентность × верность, гипотезы H1–H4, Гудхартов разрыв, sim-to-live |

Числа — [[Reference/Cheat Sheet#AI-режиссёр]]; код — [[Reference/Architecture#Директор — контракты]]; вопросы — [[Reference/Open Questions#AI-режиссёр]]; правки из ревью — [[Reference/Roadmap#Существенно]].

## Роль и философия

Следствие философии мягкого дефицита ([[Design/Overview]]): игра не наказывает, а создаёт осмысленный выбор. Цель — держать эмоциональную кривую в коридоре: спокойная тактильная работа + мягкое давление.

> [!warning] Запреты
> - Не наказывать за стиль игры (хомячество учитывать, но не штрафовать).
> - Не подыгрывать заметно: очевидный rubber banding разрушает доверие к миру.
> - **Не трогать правила, которые игрок выучил:** стоимость слотов, цены аренды, поведение инструментов.

## Архитектура и тик

Игра дискретная — основной тик **тайм-слот**. Уровни решений:

| Уровень | Решения |
|---|---|
| **Слот** | микрокоррекции: какой покупатель зайдёт, фоновое событие |
| **День** (утро, до планирования) | состав рынка, заказы, погода/настроение поселения |
| **Неделя** | событие недели, сдвиг спроса, новые типы покупателей, темп черноты |

Лёгкий реалтайм-слой поверх слотов (звук, музыка, фоновая жизнь) читает те же индексы, но ничего не решает.

**В срезе тик = день:** `GameDirector.OnDayStart(day)` вызывается утром, до завоза рынка.

## Входные параметры (полный дизайн)

| Группа | Сигналы |
|---|---|
| **Темп игрока** | распределение слотов за N дней (вылазки / ремонты / продажи / «ничего»); реальное время на ремонт; ошибки жестов и провалы проверки подряд; частота обращения к блокноту |
| **Экономика** | деньги + тренд; **runway в днях до аренды** — главный сигнал давления; запас расходников; индекс перепроданности; деньги, «замороженные» в непроданном |
| **Инвентарь** | заполненность рюкзака / полок / витрины; дни на витрине (приедание); доля «любимых» предметов; разнообразие категорий |
| **Мастерство** | успешность диагностики; кривая времени по типам поломок; неиспользованные жесты и инструменты |
| **Чернота** | заражение склада и скорость распространения; есть ли средства очистки; продажи заражённого |
| **Покупатели** | конверсия визитов; реакция на цены; невыполненные запросы; баланс типов |
| **Нарратив** | время с последней зацепки; судьба «гудящих» предметов |

**MVP-сигналы (5):** runway, заполненность витрины, ошибки жестов, время с последней зацепки, индекс повторяемости лута. В срезе «витрина» сведена к «замороженному» непроданному фонарю.

## Индексы

Три агрегата 0..1, EMA-сглаженные (`indexSmoothing = 0.5`), окно истории `historyDays = 3`. Формулы — как в `GameDirector`:

### Давление

```
runwayFactor = clamp01(1 − runway / runwayComfortDays=4)     runway = Money / avgBurn (avgBurn = среднее Spent−Earned за историю)
lowMoney     = Money < lowMoneyThreshold=40 ? 1 : 0
frozen       = clamp01((heldDays − frozenDaysComfort=2) / frozenDaysComfort)   // непроданный текущий предмет
Pressure = clamp01(0.5·runwayFactor + 0.25·lowMoney + 0.25·frozen)
```

### Скука

```
repetition  = 1 − distinct / count   по последним repetitionWindowRestocks=4 сигнатурам завоза (нужно ≥ 4)
leadOverdue = clamp01((day − LastLeadDay − leadCooldownDays=4) / (leadOverdueDays=7 − 4))
idle        = средняя доля пустых слотов за историю
Boredom = clamp01(0.5·repetition + 0.3·leadOverdue + 0.2·idle)
```

### Фрустрация

```
errors    = clamp01(errorsPerRepair / errorsPerRepairComfort=2 − 1)
fails     = clamp01((checkFailStreak − checkFailStreakComfort=2) / 2)
walkaways = ушедшие покупатели / попытки продажи за историю
Frustration = clamp01(0.4·errors + 0.4·fails + 0.2·walkaways)
```

### Коридоры и реакция

| Индекс | Порог | Реакция при выходе из коридора |
|---|---|---|
| Давление | high **0.65** / low **0.25** | Высокое → щедрый рынок, сговорчивый покупатель. Низкое → рынок скупее, покупатель прижимистее |
| Скука | high **0.6** | Высокая (и прошёл cooldown) → зацепка: гудящий фонарь на рынке |
| Фрустрация | high **0.55** | Высокая → покупатель сговорчивее (mood ≥ 0.6); в полной игре — знакомые категории, подсказка через записку, пауза в новых механиках |

Полный дизайн: давление низкое → событие недели, редкий заказ, рост расходов; скука → редкая находка, необычный предмет, коллекционер.

## Ручки MVP

Полный список выходов дизайна: состав лута рынка и вылазок; тип и число поломок; поток покупателей; событие недели; темп черноты; момент зацепки; фон, погода, музыка. **В срезе три ручки, −1..1:**

| Ручка | Эффект при значении `g` | Числа |
|---|---|---|
| **Щедрость рынка** `MarketGenerosity` | сток `base + round(g·1)`, кламп `[marketMinStock=1, MaxMarketStock=4]`; склонение генерации к min дефектов с вероятностью `g·0.75`; скидка `round(g·4)` монет с полом цены **8** | `stockBonusAtFullGenerosity`, `defectBiasAtFullGenerosity`, `priceDiscountAtFullGenerosity`, `marketPriceFloor` |
| **Настроение покупателя** `BuyerMood` | `happy = 0.9 + mood·0.08`, `accept = 1.15 + mood·0.08` | `buyerMoodRatioDelta` |
| **Момент зацепки** `LeadPlanned` | на ближайшем завозе один фонарь помечается `Hum`; гудящий фонарь с качеством ≥ **80** продаётся на **+15%** | `humQualityThreshold`, `humPriceBonus` |

«Поток покупателей» в срезе сведён к сговорчивости одного покупателя; событие недели и чернота не реализованы.

## Правила-ограничители

| Ограничитель | Дизайн | В коде |
|---|---|---|
| **Гистерезис** | решения уровня дня/недели, не каждый слот; повёрнутая ручка не трогается до завтра | решение раз в день; ручка движется к цели не быстрее `knobMaxStepPerDay = 0.34` |
| **Клампы** | рынок никогда не пустой; хотя бы один покупатель; зацепка не позже дня X; заражение не быстрее, чем игрок может чистить | сток ≥ 1 и ≤ вместимости UI; пол цены 8; зацепка **не позже 8 дней** без зацепки (`leadMaxDays`) — принудительно |
| **Бюджет отклонения** | суммарная «помощь» и «помеха» за неделю ограничены — ни спасательного круга, ни удушения | `weeklyKnobBudget = 3`: в день платится `|MarketGenerosity| + |BuyerMood|`; исчерпан → ручки распадаются к нейтрали шагом 0.34; сброс каждые 7 дней |

> [!warning] Расхождение: две валюты бюджета
> `GameDirector.ApplyPolicyAction` списывает **интеграл отклонения** ручек за день (решение ревью 2026-07-09 — «застрявшая помощь» невозможна). Но `DirectorActionLibrary.IsBlockAllowed` и `DirectorActionValidator.ValidateBudget` сравнивают с тем же `weeklyKnobBudget` величину **`BudgetCost`** блока (1.0 у `market_generous`, 0.8 у `buyer_friendly`…). Шкалы разные, счётчик один; `DevChecks` допускает перерасход +2. Нужно выбрать одну валюту — [[Reference/Roadmap#Существенно]].

## Пайплайн решения

Цепочка дневного решения (введена 2026-07-10, чтобы режиссёр стал **заменяемой политикой**):

```text
Game events → DirectorTelemetry → DirectorObservationBuilder → IDirectorPolicy → DirectorActionValidator → GameDirector.ApplyPolicyAction → Game systems
```

```csharp
public interface IDirectorPolicy
{
    string PolicyId { get; }
    DirectorAction Decide(DirectorObservation observation, DirectorActionMask mask);
}

public class DirectorAction
{
    public string PolicyId, ActionId = "hold";      // ActionId = теги блоков через "+", напр. "market_generous+buyer_friendly"
    public float BudgetCost; public int RiskLevel;
    public float MarketGenerosityTarget, BuyerMoodTarget; public bool PlanLead;
    public readonly List<string> Reasons;
}

public struct DirectorActionMask
{
    public bool CanAdjustMarket, CanAdjustBuyer, CanPlanLead, CanUseWeeklyBudget;   // всегда true из Default
    public List<string> AllowedActionIds, BlockedActionIds;                          // строит DirectorActionLibrary.BuildMask
}
```

`OnDayStart`: пересчёт индексов → `BuildForDay(day)` → `BuildMask` → `policy.Decide` → `Validate` → `ApplyPolicyAction` → `LogDecision`. Снимок последнего решения (`LastDecisionObservation/Mask/Action/Validation`) хранится для симулятора.

### Политики

| Policy | Id | Роль | Статус |
|---|---|---|---|
| `RuleBasedDirectorPolicy` | `rule_based` | продакшн-baseline: правила по порогам индексов | ✅ по умолчанию |
| `RandomDirectorPolicy` | `random` | baseline для сравнения; выбирает только из `AllowedActionIds` | ✅ |
| `ReplayDirectorPolicy` | `replay` | воспроизводит записанные решения — регрессии, shadow | ✅ |
| `GruDirectorPolicy`, `TransformerDirectorPolicy`, `LlmDirectorPolicy` | — | нейросетевые / LLM | ⬜ [[Director/Neural Director]] |

> [!warning] Расхождение: маска не влияет на rule-based
> `BuildMask` аккуратно считает по каждому блоку кулдауны, коридоры индексов и бюджет и складывает результат в `AllowedActionIds / BlockedActionIds`. Но `RuleBasedDirectorPolicy.Decide` проверяет только четыре булевых флага (всегда `true`) и **ни разу не смотрит в списки id**; `TryApplyBlock` маску тоже игнорирует. Предусловия блоков вычисляются, пишутся в телеметрию — и отбрасываются; реально ограничивают только `ValidateBudget` и `ValidateLead`. При этом `RandomDirectorPolicy` маску уважает — baseline и продакшн играют по разным правилам, сравнение в batch нечестное. См. [[Reference/Roadmap#Существенно]].

### Валидатор

`DirectorActionValidator.Validate(action, observation, mask, config)` — обязательный safety-слой между policy и применением: нормализует цели ручек в −1..1, режет запрещённые маской действия, блокирует ранние зацепки по cooldown, проверяет бюджет по `BudgetCost`, пишет `validation_changed` / `validation_notes`.

## Библиотека блоков

Режиссёр (и будущая нейросеть) выбирает только блоки из библиотеки — **дискретная грамматика** вместо генеративного мусора. Каждый блок: `id, title, description, level, tags, target_index, preconditions, cooldown_days, budget_cost, risk_level, payload`. Код: `DirectorActionLibrary` (кодовая библиотека, не SO).

| Блок | Уровень | Цель | Cooldown | Cost | Условие | Эффект | Статус |
|---|---|---|---|---|---|---|---|
| `hold` | day | — | — | 0 | — | без вмешательства | ✅ |
| `market_generous` | day | pressure | 1 | 1.0 | давление высокое; есть бюджет | щедрость → 1 | ✅ |
| `market_strict` | day | economy | 1 | 0.5 | давление ≤ 0.5; есть бюджет | щедрость → −0.5 | ✅ |
| `buyer_friendly` | day | frustration | 1 | 0.8 | давление или фрустрация | mood → 0.6…1 | ✅ |
| `buyer_strict` | day | economy | 1 | 0.4 | давление ≤ 0.5, фрустрация ≤ 0.75 | mood → −0.4 | ✅ |
| `story_lead_humming_item` | story | narrative | по конфигу (4 дня) | 0.75 | зацепка не запланирована; cooldown прошёл | гудящий фонарь в следующем завозе | ✅ |
| `familiar_item` | day | frustration | — | 0.5 | не реализован | знакомая категория предмета | ⬜ данные |
| `easy_defects` | day | frustration | — | 0.7 | не реализован | меньше скрытых / блокирующих дефектов | ⬜ данные |
| `varied_defects` | day | boredom | — | 0.5 | не реализован | уход от повторяющихся дефектов | ⬜ данные |

Будущие блоки после расширения игры: `debris_fall_event` (спрос на фонари, фильтры, ремни), `water_contamination` (спрос на фильтры), `collector_visit`, `repair_note_found` (подсказка в блокнот), `blackness_warning`, `black_market_request`.

## Телеметрия

`DirectorTelemetry` — кольцевой runtime-лог событий: в память, `Debug.Log` и JSONL (`Telemetry/local/` в редакторе; `persistentDataPath/Telemetry/local/` в билде). Снапшот при каждом событии: день, слот, деньги, предмет, активные дефекты.

События: `SessionStarted, DayStarted, SlotSpent, MarketOpened, MarketRestocked, ItemBought, RepairStarted, DefectRevealed, GestureError, RepairCheckFailed, RepairCompleted, CounterOpened, SaleAttempted, SaleCompleted, SaleRefused, DayEnded, DirectorDecision, DirectorActionApplied, LeadSeen, LeadAcquired, LeadResolved`.

`DirectorDecision` несёт: индексы, ручки, `policy_id`, `action_id`, `observation_hash`, `allowed_action_ids`, `blocked_action_ids`, `validation_changed`, `validation_notes`, причину.

### Observation

`DirectorObservationBuilder.Build(windowSize)` собирает `DirectorObservation`: день/слот/деньги, текущий предмет (возраст, качество, дефекты, чистота, hum), рынок (сток, hum, средняя цена), зацепки, индексы, ручки, бюджет, агрегаты окна событий (ошибки, провалы, попытки продаж, отказы, пустые слоты) и список `DirectorEventToken` для будущей GRU/Transformer. `observation_hash` связывает решение с входом.

## Отладка

- **F1-панель** (`DirectorDebugPanel`, хаб и верстак): индексы, ручки, бюджет, история решений, ручной override (`NudgeKnobs` — бюджет не тратит), принудительная зацепка (`ForceLead`).
- Плейтест-вопрос: замечают ли игроки подстройку. Если да — ослаблять.
- Известная проблема: панель вызывает `Build(24)` четыре раза в секунду и перезаписывает `LastObservation` — инструмент наблюдения меняет наблюдаемое ([[Reference/Roadmap#Существенно]]).

## Симуляция

### `DirectorSimulation`

Headless-прогон дней без Play Mode: профиль игрока + seed + число дней + любая `IDirectorPolicy`. Дешёвый бот: покупает, чинит, ошибается, продаёт или получает отказ. Пишет `Telemetry/simulations/<runId>.jsonl` — первый датасет для imitation learning.

| Профиль | Деньги | BuyChance | RepairSkill | MistakeChance | SalePriceRatio | PreferHum |
|---|---|---|---|---|---|---|
| `balanced` | 100 | 0.9 | 0.85 | 0.10 | 0.95 | да |
| `struggling` | 55 | 0.75 | 0.55 | 0.35 | 1.20 | нет |

### `DirectorSimulationBatch`

Матрица `профиль × policy × seed` → `Telemetry/simulations/batches/<batchId>/` (`runs.jsonl`, `summary.jsonl`, `summary.csv`). Метрики: `survival_rate, avg_final_money, avg_spent, avg_earned, avg_sold_days, avg_sale_refusal_days, avg_idle_days, avg_unused_slots, avg_item_quality, avg_action_diversity, avg_validation_changes, avg_budget_validation_changes, avg_lead_delivery_days`. Тихий режим логов для batch.

Меню Unity: `DustMan/Run Director Simulation`, `DustMan/Run Director Batch Simulation`; batchmode — `DevChecks.RunDirectorSimulationBatch`, `DevChecks.RunDirectorBatchSimulationBatch`.

## Что дальше

Roadmap нейросетевой политики, shadow mode и controlled rollout — [[Director/Neural Director]]. Исследовательская рамка (компетентность × верность, Гудхартов разрыв, sim-to-live) — [[Director/DirectorBench]]. Открытые вопросы — [[Reference/Open Questions#AI-режиссёр]].

Числа — [[Reference/Cheat Sheet#AI-режиссёр]]. Файл: `Assets/Data/Resources/DirectorConfig.asset`.
