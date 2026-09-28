# Telemetry

Папка для локальных логов событий и будущих симуляционных прогонов нейрорежиссёра.

- `local/` - JSONL-сессии из Unity Editor.
- `simulations/` - headless-прогоны `DirectorSimulation` для датасетов нейрорежиссёра.
- `playtests/` - будущие выгрузки плейтестов.

Папка лежит вне `Assets`, поэтому Unity не импортирует эти файлы как игровые ассеты. Локальные JSONL-логи исключены из git через `.gitignore`.

## simulations

Симуляционный датасет создаётся через `DirectorSimulation.Run(...)` или меню Unity `DustMan/Run Director Simulation`.

Минимальная строка JSONL связывает вход, решение и исход дня:

```json
{"run_id":"devchecks_rule_based","profile_id":"balanced","policy_id":"rule_based","seed":404,"day":1,"observation_hash":"07b8b4d3","allowed_action_ids":"hold,market_generous,market_strict,buyer_friendly,buyer_strict","blocked_action_ids":"story_lead_humming_item:lead cooldown","chosen_action_id":"market_strict+buyer_strict","pressure":0,"boredom":0,"frustration":0,"money_before":100,"money_after":156,"bought":true,"repaired":true,"sold":true}
```

Этого достаточно для первого imitation-learning датасета: модель получает историю/снимок, список допустимых блоков и выбранное baseline-действие.

## simulations/batches

Batch-сравнение создаётся через `DirectorSimulationBatch.Run(...)`, меню Unity `DustMan/Run Director Batch Simulation` или batchmode-вход `DevChecks.RunDirectorBatchSimulationBatch`.

Структура папки:

```text
Telemetry/simulations/batches/<batchId>/
  <batchId>_<profile>_<policy>_<seed>.jsonl
  runs.jsonl
  summary.jsonl
  summary.csv
```

`summary.csv` нужен для быстрого сравнения политик по профилям: выживание экономики, финальные деньги, продажи/отказы, пустые дни, качество ремонта, разнообразие action-блоков и нарушения/срабатывания validator budget.
