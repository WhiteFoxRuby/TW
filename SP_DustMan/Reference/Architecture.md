---
tags: [gdd, reference]
related: [[Home]], [[Reference/MVP Scope]], [[Director/Director]], [[Reference/Roadmap]]
---

# Architecture — архитектура кода (Unity)

> [!summary]
> Unity 6000.3.8f1 (URP 17.3, Input System 1.18), C#. 38 скриптов, ~7 600 строк. Осмысленная слоистость `Core / Gestures / Workbench / Economy / Director / UI`; все числа — в ScriptableObject-конфигах; UI — uGUI, собираемый кодом; состояние день-цикла и режиссёр — статические классы, живущие между сценами. Директор — не прототип, а рабочая исследовательская инфраструктура под [[Director/DirectorBench]]. Описание соответствует коммиту `9f12204` от 2026-09-04.

## Стек

- **Unity 6000.3.8f1**, **URP 17.3.0** — рендер 2D (плейсхолдер-спрайты).
- **Input System 1.18** — project-wide Input Actions (`InputSystem_Actions.inputactions`): WASD и стрелки в хабе; на верстаке — `Mouse.current` для жестов.
- **uGUI 2.0** — HUD и оверлеи, собираются программно через `UiBuilder` (без префабов).
- **ScriptableObject** — все дефиниции и конфиги в `Assets/Data/Resources/`, загружаются `Resources.Load`.
- Пакеты в манифесте, но не используются в коде: Timeline, Visual Scripting, AI Navigation, Multiplayer Center, Test Framework 1.6 (тестовой сборки нет).

## Структура проекта

```
DustMan/
├── Assets/
│   ├── Data/Resources/            # 21 ассет: Lantern, Module_*, Defect_*, Tool_*, EconomyConfig, GestureConfig, DirectorConfig
│   ├── Editor/
│   │   └── DevChecks.cs           # приёмка данных, batchmode-входы, меню DustMan/*
│   ├── Scenes/                    # SampleScene (хаб), Workbench, GestureDebug, DataTest, InteractionScene1/2
│   ├── Scripts/
│   │   ├── Core/
│   │   │   ├── Data/              # ItemDefinition, ModuleDefinition, DefectDefinition, ToolDefinition,
│   │   │   │                      #   ItemInstance, ItemInstanceGenerator, EconomyConfig, GestureConfig, DirectorConfig
│   │   │   ├── GameState.cs       # деньги, день, слоты, текущий предмет, рынок; FallbackData
│   │   │   ├── LevelBuilder.cs    # генерация хаба (вынесена из PlayerController)
│   │   │   ├── SpriteUtils.cs     # кэшированный белый спрайт, прямоугольники-плейсхолдеры
│   │   │   ├── SceneCarry.cs      # позиция игрока между сценами (вместо PlayerPrefs)
│   │   │   └── DataTestController.cs
│   │   ├── Gestures/
│   │   │   ├── GestureCore.cs     # GestureKind, RotationTracker, WipeGrid, RockTracker (чистая логика)
│   │   │   ├── GestureRecognizer.cs   # MonoBehaviour-обёртка для дебаг-сцены
│   │   │   └── GestureDebugController.cs
│   │   ├── Workbench/
│   │   │   ├── WorkbenchController.cs # 814 строк: осмотр, инструменты, винты, дефекты, проверка, качество
│   │   │   └── LanternView.cs         # визуал фонаря: собранный/разобранный, винты, оверлеи дефектов, луч
│   │   ├── Economy/
│   │   │   ├── MarketOverlay.cs, CounterOverlay.cs, DaySummaryOverlay.cs
│   │   ├── Director/
│   │   │   ├── GameDirector.cs        # индексы, ручки, ограничители, применение действия
│   │   │   ├── DirectorPolicy.cs      # DirectorAction, DirectorActionMask, IDirectorPolicy, RuleBasedDirectorPolicy
│   │   │   ├── DirectorActionLibrary.cs   # блоки, BuildMask, TryApplyBlock
│   │   │   ├── DirectorActionValidator.cs # валидатор, RandomDirectorPolicy, ReplayDirectorPolicy
│   │   │   ├── DirectorObservation.cs     # DirectorObservation, DirectorEventToken, ObservationBuilder
│   │   │   ├── DirectorTelemetry.cs       # событийный лог, JSONL
│   │   │   ├── DirectorSimulation.cs      # headless-прогон дней, профили
│   │   │   ├── DirectorSimulationBatch.cs # матрица profile × policy × seed, summary.csv
│   │   │   └── DirectorDebugPanel.cs      # F1-панель
│   │   ├── UI/
│   │   │   ├── HubController.cs       # HUD, оверлеи, завершение дня
│   │   │   └── UiBuilder.cs           # программная сборка uGUI
│   │   ├── PlayerController.cs, CameraFollow.cs, InteractivePoint.cs, InteractionSceneController.cs
│   └── Settings/                  # URP
├── Packages/manifest.json
├── ProjectDocs/                   # документация (Unity не импортирует)
├── Telemetry/                     # local/, simulations/, playtests/ — вне Assets, в .gitignore
└── ProjectSettings/
```

## Слои и поток данных

```text
ScriptableObject-данные (Item/Module/Defect/Tool, конфиги)
        ↓ Resources.Load
GameState (static) ── RestockMarket ──► ItemInstanceGenerator ──► ItemInstance (runtime)
   │                                                                     │
   ├─ хаб: HubController + InteractivePoint ─► MarketOverlay / CounterOverlay / DaySummaryOverlay
   │                                                                     │
   └─ сцена Workbench: WorkbenchController ⇄ LanternView ⇄ GestureCore-трекеры
                                                                         │
                 все действия ─► DirectorTelemetry ─► DirectorObservationBuilder ─► IDirectorPolicy ─► Validator ─► GameDirector ─► MarketPlan / ratio покупателя
```

### Сцены

| Сцена | Роль | В билде |
|---|---|---|
| `SampleScene` | хаб: side-scroll, три точки (Рынок, Верстак, Прилавок), HUD, конец дня | да |
| `Workbench` | верстак | да |
| `GestureDebug` | 6 площадок жестов, R — сброс (этап 2) | да — выключить перед плейтест-билдом |
| `DataTest` | генератор фонаря, кнопка «Сгенерировать» (этап 1) | да — выключить |
| `InteractionScene1/2` | паттерн «зайти в сцену, вернуться по Q» из прототипа | да — выключить |

Переход хаб → верстак через `InteractivePoint` (E); возврат по Q с восстановлением позиции через `SceneCarry`. Частичный прогресс протирки/зачистки при выходе по Q восстанавливается (`WipeGrid.PreClear`, `DefectInstance.Progress`).

### Верстак

`WorkbenchController` держит всё взаимодействие: `OnPress / OnHold / OnRelease` по `Mouse.current`, состояние удерживаемого инструмента и запасной детали, активный винт (`ScrewView`: `Tightened / Seated / Removed`, свой `RotationTracker`), протирка (`WipeGrid` на корпусе), качание наждачкой (`RockTracker`), лупа (`magnifierHover`), проверка (`DoCheck` по `FirstSymptom`), `FinishRepair` (качество, слот, телеметрия, возврат в хаб). `LanternView` — только визуал: собранный/exploded вид по `explodedOffset`, оверлеи дефектов (трещины, окисел, батарейки), луч `ShowBeam(0|1|2)`.

Уровни осмотра из дизайна (обзор → рабочий → лупа) в срезе сведены к зуму камеры + лупе.

### Экономика и UI

`MarketOverlay` — вёрстка под 4 оффера (жёстко), покупка списывает слот и деньги, `ItemBought`. `CounterOverlay` — шкала 0–100, реакция по `AdjustedHappyRatio / AdjustedAcceptRatio`, `SaleAttempted / Completed / Refused`. `DaySummaryOverlay` — сводка. `HubController` — HUD, счётчик открытых оверлеев (`OverlayOpen` блокирует точки взаимодействия и повторное открытие), кнопка «Закончить день». Подписи — `raycastTarget = false`, чтобы не перехватывать клики.

## Статика и Enter Play Mode

`GameState` и `GameDirector` — статические классы с флагом `initialized`, сбрасываемым **domain reload**. `EditorSettings`: `m_EnterPlayModeOptionsEnabled: 1`, `m_EnterPlayModeOptions: 0` — сейчас обе перезагрузки выполняются, но один клик «Disable Domain Reload» ради скорости итераций сломает игру: второй Play унаследует деньги, день и историю. Остальная статика готова (`SpriteUtils.whiteSprite` через `== null`, `HubController.overlayCount` в `Awake`). Дешёвая страховка — `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` со сбросом флагов; **пока не добавлена** ([[Reference/Roadmap#Существенно]]).

## Директор — контракты

Подробно — [[Director/Director#Пайплайн решения]]. Ключевые типы:

| Тип | Файл | Роль |
|---|---|---|
| `GameDirector` (static) | GameDirector.cs | индексы, ручки, бюджет, `OnDayStart`, `BuildMarketPlan`, `Adjusted*Ratio`, `Report*` из игры, `CaptureRuntimeState / RestoreRuntimeState` для симулятора |
| `IDirectorPolicy`, `DirectorAction`, `DirectorActionMask` | DirectorPolicy.cs | контракт политики |
| `DirectorActionBlock`, `DirectorActionLibrary` | DirectorActionLibrary.cs | библиотека блоков, `BuildMask`, `TryApplyBlock`, `BuildCandidates` |
| `DirectorActionValidator`, `DirectorValidationResult` | DirectorActionValidator.cs | safety-слой |
| `DirectorObservation`, `DirectorObservationBuilder` | DirectorObservation.cs | снимок + окно событий, `observation_hash` |
| `DirectorTelemetry`, `DirectorEventType`, `DirectorEventRecord` | DirectorTelemetry.cs | лог; публичные JSON-хелперы `Str / Num / Bool / Escape` |
| `DirectorSimulation*`, `DirectorSimulationProfile` | DirectorSimulation*.cs | headless-прогоны и batch |

## Телеметрия и датасеты

```text
Telemetry/
  local/          # JSONL-сессии из Unity Editor (в билде — persistentDataPath/Telemetry/local/)
  simulations/    # <runId>.jsonl — DirectorSimulation
    batches/<batchId>/
      <batchId>_<profile>_<policy>_<seed>.jsonl
      runs.jsonl · summary.jsonl · summary.csv
  playtests/      # будущие выгрузки
```

Строка датасета симуляции связывает вход, решение и исход дня:

```json
{"run_id":"devchecks_rule_based","profile_id":"balanced","policy_id":"rule_based","seed":404,"day":1,
 "observation_hash":"07b8b4d3","allowed_action_ids":"hold,market_generous,market_strict,buyer_friendly,buyer_strict",
 "blocked_action_ids":"story_lead_humming_item:lead cooldown","chosen_action_id":"market_strict+buyer_strict",
 "pressure":0,"boredom":0,"frustration":0,"money_before":100,"money_after":156,"bought":true,"repaired":true,"sold":true}
```

`README.md` раздела версионируется, сами прогоны — нет.

## Проверки и точки входа

`Assets/Editor/DevChecks.cs` — единственная автоматическая проверка: загрузка данных, 20 генераций фонаря, директор (симуляция недели безденежья: давление растёт, щедрость включается, бюджет и клампы соблюдаются, зацепка расходуется один раз), валидатор, policies, библиотека, симулятор и batch. Отказывается запускаться в Play mode.

| Вход | Где |
|---|---|
| Меню `DustMan/Run Dev Checks` | редактор |
| Меню `DustMan/Run Director Simulation` / `Run Director Batch Simulation` | редактор |
| `DevChecks.Run` / `RunDirectorSimulationBatch` / `RunDirectorBatchSimulationBatch` | `-batchmode -executeMethod` |

## Соглашения

- Числа баланса и пороги — только в SO-конфигах (`EconomyConfig`, `GestureConfig`, `DirectorConfig`); правило соблюдается.
- Комментарии ссылаются на разделы дизайн-документов (`mvp-scope §4`, `design/ai-director.md`).
- `ProjectDocs` и `Telemetry` — вне `Assets`, Unity их не импортирует.
- GUID-ы новых ассетов и сцен прописаны вручную по паттерну проекта.
- Git: ветка `main`, стандартный Unity-`.gitignore` (Library, obj, Logs, UserSettings, csproj/sln, телеметрия, `.claude/settings.local.json`).

## Технический долг (ревью 2026-09-04)

| # | Что | Где |
|---|---|---|
| 1 | **Тестов нет**: ноль `.asmdef`, весь код в `Assembly-CSharp`, `DevChecks` — монолитный `[MenuItem]`, `CheckDirector` восстанавливает только `Money` | весь проект |
| 2 | Библиотека блоков не влияет на rule-based решения (маска игнорируется) | `DirectorPolicy.cs`, `DirectorActionLibrary.TryApplyBlock` |
| 3 | Две валюты недельного бюджета: интеграл отклонения vs `BudgetCost` | `GameDirector.cs:364`, `DirectorActionLibrary.cs:362`, `DirectorActionValidator.cs:114` |
| 4 | 62 зашитых строковых id модулей и дефектов | `LanternView` 30, `WorkbenchController` 18, `GameState.FallbackData` 10, `ItemInstance` 1 |
| 5 | Сброс статики держится на domain reload | `GameState.initialized`, `GameDirector.initialized` |
| 6 | JSON-хелперы продублированы ×3 (~40 строк на копию); `Escape` не экранирует управляющие символы кроме `\n \r \t` | `DirectorTelemetry`, `DirectorSimulation`, `DirectorSimulationBatch` |
| 7 | `DirectorDebugPanel.Update` вызывает `Build(24)` 4 раза в секунду и перезаписывает `LastObservation` | `DirectorDebugPanel.cs:87` |
| 8 | Вёрстка рынка жёстко под 4 ряда против вычисляемого `MaxMarketStock` | `MarketOverlay.cs:60-84` |
| 9 | `Camera.main` без проверки | `LevelBuilder.cs:13`, `PlayerController.cs:51` |
| 10 | `ReportRepairCompleted(int quality)` — параметр не используется; `ResetState` не сбрасывает `EchoDecisionsToDebug`; бюджет не тратится при ручном override; `AppendJsonl` открывает файл на каждое событие; отладочные сцены в Build Settings | разное |

Приоритеты и порядок — [[Reference/Roadmap#Порядок работ]].
