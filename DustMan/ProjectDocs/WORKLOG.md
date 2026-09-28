# Журнал работ

## 2026-09-04: полное ревью проекта

Проведено ревью всей кодовой базы среза (38 скриптов, ~7600 строк), конфигов, настроек проекта и документации. Результат — [ревью проекта](./_service/code-review-2026-09-04.md).

- **Критично.** Версионирования нет: папка `.git/` пуста, ни одного коммита за всё время работы. `.gitignore` покрывает только телеметрию — `Library/`, `obj/`, `Logs/`, `UserSettings/`, `.csproj`/`.sln` не игнорируются, их нужно закрыть до `git init`. Тестов нет: `DevChecks` — Editor-only `[MenuItem]`, в проекте ноль `.asmdef`, поэтому чистая логика (`RotationTracker`, `WipeGrid`, `RockTracker`, `ComputeQuality`, формулы индексов) не тестируется вне редактора.
- **Директор.** Найдены два расхождения между задуманной и фактической архитектурой: `RuleBasedDirectorPolicy` не смотрит в `AllowedActionIds`/`BlockedActionIds` (маска считается, пишется в телеметрию и отбрасывается; `RandomDirectorPolicy` её при этом уважает — baseline сравнивается нечестно); недельный бюджет считается в двух несовместимых валютах — интеграл отклонения в `GameDirector` против `BudgetCost` в библиотеке и валидаторе.
- **Прочее существенное.** 62 зашитых строковых id модулей и дефектов в верстаке в обход SO-слоя; сброс статики держится на domain reload при уже включённом `m_EnterPlayModeOptionsEnabled`; JSON-хелперы продублированы в трёх файлах; `DirectorDebugPanel` перезаписывает `LastObservation` 4 раза в секунду.
- **Мелочи.** Вёрстка рынка жёстко под 4 ряда против вычисляемого `MaxMarketStock`; 30 очков аккуратности выдаются авансом; `Camera.main` без проверки в `LevelBuilder`/`PlayerController`; отладочные сцены включены в Build Settings.
- Отмечено как сделанное ранее: правки прошлых ревью на месте — утечка текстур закрыта, дедзона вращения, восстановление прогресса при выходе по Q, `raycastTarget=false` на подписях.
- Порядок работ: git и `.gitignore` → `.asmdef` и EditModeTests → валюта бюджета директора → маска в rule-based политике.

## 2026-07-10: DirectorSimulationBatch и policy-метрики

- Добавлен `Assets/Scripts/Director/DirectorSimulationBatch.cs`: batch-runner для матрицы `профиль игрока × policy × seed`.
- Базовая матрица сравнения: профили `balanced` и `struggling`, политики `rule_based` и `random`, несколько seed на одинаковом числе дней.
- Batch пишет папку `Telemetry/simulations/batches/<batchId>/`: дневные JSONL каждого run, `runs.jsonl`, `summary.jsonl` и `summary.csv`.
- Первые метрики: `survival_rate`, `avg_final_money`, `avg_spent`, `avg_earned`, `avg_sold_days`, `avg_sale_refusal_days`, `avg_idle_days`, `avg_unused_slots`, `avg_item_quality`, `avg_action_diversity`, `avg_validation_changes`, `avg_budget_validation_changes`, `avg_lead_delivery_days`.
- В меню Unity добавлен пункт `DustMan/Run Director Batch Simulation`, для batchmode добавлен `DevChecks.RunDirectorBatchSimulationBatch`.
- `DevChecks` теперь проверяет, что batch создаёт 4 агрегата для дефолтной матрицы `2 профиля × 2 policy`, сохраняет summary-файлы и не ломает одиночный симулятор.
- Для симуляций добавлен тихий режим директорских debug-строк: batch больше не забивает лог сотнями `[Режиссёр]`/`[Telemetry]` сообщений, но обычная игра сохраняет отладочный вывод.

## 2026-07-10: DirectorSimulation и симуляционный датасет

- Добавлен `Assets/Scripts/Director/DirectorSimulation.cs`: headless-прогон игровых дней для нейрорежиссёра без Play Mode.
- Симулятор принимает `DirectorSimulationProfile`, seed, число дней и любую `IDirectorPolicy`, поэтому на одном контракте можно сравнивать `RuleBasedDirectorPolicy`, `RandomDirectorPolicy`, `ReplayDirectorPolicy` и будущую GRU/Transformer-policy.
- Базовые профили: `balanced` и `struggling`. Пока это не полная модель игрока, а дешёвый бот: покупает, чинит, ошибается, продаёт или получает отказ.
- Датасет пишется в `Telemetry/simulations/<runId>.jsonl`. В строке есть `observation_hash`, `allowed_action_ids`, `blocked_action_ids`, `chosen_action_id`, индексы режиссёра, результат валидации, деньги/слоты и исход дня игрока.
- `GameDirector` хранит последний снимок решения (`LastDecisionObservation`, `LastDecisionMask`, `LastDecisionAction`, `LastDecisionValidation`), чтобы симулятор мог собрать строку датасета без парсинга JSONL-телеметрии.
- В меню Unity добавлен пункт `DustMan/Run Director Simulation`, для batchmode добавлен `DevChecks.RunDirectorSimulationBatch`; `DevChecks` проверяет, что симулятор создаёт JSONL, связывает observation/mask/action и моделирует хотя бы одно действие игрока.

## 2026-07-10: DirectorActionLibrary и action mask

- Добавлен `Assets/Scripts/Director/DirectorActionLibrary.cs`: кодовая библиотека первых блоков режиссёра.
- Описаны MVP-блоки `hold`, `market_generous`, `market_strict`, `buyer_friendly`, `buyer_strict`, `story_lead_humming_item`; будущие блоки `familiar_item`, `easy_defects`, `varied_defects` уже есть в каталоге, но помечены как недоступные в текущем срезе.
- Каждый блок хранит `id`, `title`, `description`, `level`, `tags`, `target_index`, `preconditions`, `cooldown_days`, `budget_cost`, `risk_level` и `payload`.
- `DirectorActionLibrary.BuildMask` строит `allowed_action_ids` и `blocked_action_ids` с причинами. `GameDirector` теперь пишет эти поля в `DirectorDecision`.
- `RuleBasedDirectorPolicy` использует библиотечные блоки для action id/metadata, а `RandomDirectorPolicy` выбирает только из доступных блоков.
- `DevChecks` проверяет наличие каталога, future-блоки вне mask, превращение блока в `DirectorAction` и metadata `budget/risk/target_index`.

## 2026-07-10: DirectorActionValidator и baseline policies

- Добавлен `Assets/Scripts/Director/DirectorActionValidator.cs`: `DirectorActionValidator`, `DirectorValidationResult`, `RandomDirectorPolicy` и `ReplayDirectorPolicy`.
- Цепочка дневного решения стала явной: `DirectorObservation -> IDirectorPolicy -> DirectorActionValidator -> GameDirector.ApplyPolicyAction`.
- Валидатор нормализует цели ручек в диапазон -1..1, режет действия, запрещённые `DirectorActionMask`, блокирует ранние зацепки по cooldown и пишет `validation_changed`/`validation_notes` в `DirectorDecision`.
- `RandomDirectorPolicy` нужен как baseline для сравнения, а `ReplayDirectorPolicy` - для будущих регрессионных и shadow-прогонов.
- `DevChecks` проверяет валидатор, random/replay policies и подмену policy в `GameDirector`.

## 2026-07-10: IDirectorPolicy и rule-based policy

- Добавлен `Assets/Scripts/Director/DirectorPolicy.cs`: `DirectorAction`, `DirectorActionMask`, `IDirectorPolicy` и `RuleBasedDirectorPolicy`.
- `GameDirector.OnDayStart` теперь строит `DirectorObservation`, передаёт его в policy, применяет возвращённый `DirectorAction`, а сам оставляет у себя клампы, гистерезис, недельный бюджет и запись телеметрии.
- `DirectorDecision` теперь пишет `policy_id`, `action_id` и `director_action_id`, чтобы будущие replay/shadow-прогоны могли сравнивать baseline, GRU и Transformer на одном контракте.
- `DevChecks` проверяет, что default policy - `RuleBasedDirectorPolicy`, и что решение возвращает `policy_id`/`action_id`.

## 2026-07-10: DirectorObservationBuilder

- Добавлен `Assets/Scripts/Director/DirectorObservation.cs`: `DirectorObservation`, `DirectorEventToken` и `DirectorObservationBuilder`.
- Builder собирает снимок текущего состояния для будущей policy: деньги, день/слот, текущий предмет, рынок, зацепки, индексы режиссёра, ручки, недельный бюджет и окно последних событий.
- `GameDirector` теперь добавляет `observation_hash` и краткие признаки окна событий в `DirectorDecision`, чтобы будущие replay/shadow-прогоны могли связывать решение с входным состоянием.
- F1-панель режиссёра показывает краткую строку observation, а `DevChecks` проверяет hash и размер окна событий.

## 2026-07-10: базовая событийная телеметрия

- Добавлен `Assets/Scripts/Director/DirectorTelemetry.cs`: кольцевой runtime-лог событий режиссёра, снапшот дня/слота/денег/предмета/активных дефектов, compact `Debug.Log` и helper-формат payload.
- Подключены базовые события для будущего GRU/Transformer-пайплайна: старт сессии и дня, трата слотов, завоз и открытие рынка, покупка, старт ремонта, раскрытие дефектов, ошибки жестов, провал проверки, завершение ремонта, открытие прилавка, попытка/успех/отказ продажи, итог дня, решения и применённые действия режиссёра, сюжетная зацепка `humming_item`.
- `GameDirector` теперь пишет решения в единый событийный лог вместе с индексами давления/скуки/фрустрации, ручками рынка/покупателя и причиной решения. Текущий rule-based режиссёр остаётся baseline, но уже создаёт датасет для следующего слоя.
- В редакторе Unity JSONL-сессии сохраняются в `Telemetry/local/` в корне проекта, то есть вне `Assets` и без импорта Unity. В билде fallback-путь - `Application.persistentDataPath/Telemetry/local/`.

## 2026-07-10: roadmap нейрорежиссёра

- Создан [design/neural-director-roadmap.md](./design/neural-director-roadmap.md): план перехода от текущего rule-based `GameDirector` к GRU/Transformer-режиссёру по истории событий.
- В roadmap зафиксированы: событийная телеметрия, библиотека блоков, `DirectorObservation`, `IDirectorPolicy`, GRU/Transformer-подходы, обучение, shadow mode, тестирование и поэтапный план.
- Обновлены ссылки в [design/README.md](./design/README.md) и [design/ai-director.md](./design/ai-director.md).

## 2026-07-09: каркас AI-режиссёра (по явному запросу владельца — вне скоупа среза)

Реализован MVP из [design/ai-director.md](./design/ai-director.md) поверх день-цикла среза:

- `GameDirector` (`Assets/Scripts/Director/`): тик — день (утро, до завоза рынка); индексы **давление / скука / фрустрация** (EMA-сглаживание) из доступных в срезе сигналов: runway по среднему дневному минусу + низкий кошелёк + «замороженный» непроданный фонарь; повторяемость комбинаций дефектов на рынке + дни без зацепки + пустые слоты; ошибки жестов на ремонт + провалы проверки подряд + ушедшие покупатели.
- Три ручки MVP: **щедрость рынка** (число фонарей, склонение к меньшему числу дефектов, скидка с полом цены), **настроение покупателя** (сдвиг порогов реакции ±8% максимум), **момент зацепки** (гудящий фонарь на рынке — плейсхолдер сюжетного слоя; тщательно починенный гудящий продаётся на 15% дороже).
- Ограничители по доку: гистерезис (шаг ручки ≤0.34/день, решения раз в день), клампы (рынок не пустой и не больше вместимости окна, пол цены, зацепка не позже дня 8 — сюжетный минимум), недельный бюджет отклонения (интеграл |отклонений ручек| по дням ≤ 3; исчерпан → ручки распадаются к нейтрали, «застрявшая помощь» невозможна). Выученные правила (слоты, аренда, инструменты) не трогаются.
- Телеметрия: каждое решение с индексами и причиной — в лог и кольцевой буфер; отладочная панель **F1** (хаб и верстак): индексы, ручки, бюджет, история, ручной override и принудительная зацепка.
- Все числа — в `Assets/Data/Resources/DirectorConfig.asset` (`DirectorConfig` SO).
- DevChecks дополнен симуляцией недели безденежья: давление растёт, щедрость включается, бюджет и клампы соблюдаются, зацепка расходуется один раз. Батч-прогон Unity: компиляция и все проверки чистые.
- Упрощения среза: «поток покупателей» сведён к сговорчивости одного покупателя; событие недели и чернота не реализованы (их нет в срезе); модель навыка — только счётчики ошибок.
- Адверсариальное ревью каркаса (2 ревьюера × скептики-верификаторы): 14 находок, 4 подтверждены и исправлены — добавлен кламп «зацепка не позже дня X» (иначе с дефолтными весами скука её не дотягивала), бюджет переведён со «стоимости движения» на интеграл отклонения с распадом ручек, вёрстка рынка пересчитана под 4 оффера и надписи перестали перехватывать клики (raycastTarget=false), DevChecks отказывается запускаться в Play mode.

## 2026-07-08 (вечер): собран вертикальный срез по mvp-scope

Реализованы этапы 0-5 из [design/mvp-scope.md](./design/mvp-scope.md); этап 6 (звук/твины) не делался — он опционален.

- **Этап 0 — санация.** `SpriteUtils` (кэшированный белый спрайт, утечка закрыта, дубли убраны), генерация хаба вынесена из `PlayerController` в `LevelBuilder`, позиция между сценами — через `SceneCarry` вместо PlayerPrefs, движение через Input Actions (project-wide asset: WASD и стрелки), `AutoSprite` удалён.
- **Этап 1 — каркас данных.** SO-типы в `Assets/Scripts/Core/Data/` (`ItemDefinition`, `ModuleDefinition`, `DefectDefinition`, `ToolDefinition`, `GestureConfig`, `EconomyConfig`), runtime `ItemInstance`, генератор 2-3 случайных дефектов. Ассеты — в `Assets/Data/Resources/` (13 штук: фонарь, 5 модулей, 5 дефектов по таблице скоупа, 5 инструментов, 2 конфига). Сцена `DataTest`: кнопка «Сгенерировать» печатает состав в консоль и на экран.
- **Этап 2 — жесты.** `Assets/Scripts/Gestures/`: `RotationTracker` (угловая дельта, CCW/CW), `WipeGrid` (маска 8×8), `RockTracker` (качания с амплитудой), `GestureRecognizer` (все 6 жестов). Пороги — в `GestureConfig.asset`, не в коде. Сцена `GestureDebug`: 6 площадок с индикаторами прогресса, R — сброс.
- **Этапы 3-4 — верстак.** Сцена `Workbench`: фонарь из модулей, инструменты на лотке (взять/вернуть), выкручивание винтов отвёрткой (вращение), разборка на модули, протирка грязи, зачистка контактов наждачкой, выпрямление кнопки плоскогубцами, замена линзы/батареек drag из запаса, сборка (посадить винт + затянуть), перекрут и лишние царапины штрафуют качество, лупа проявляет трещину линзы, проверка кнопкой с симптомами по приоритету (кнопка → питание → линза, тусклый луч для линзы), качество 0-100 по формуле скоупа, «Завершить ремонт» по E.
- **Этап 5 — экономика.** `GameState` (деньги, день, 4 слота, текущий предмет), точки Рынок/Верстак/Прилавок в хабе, рынок и прилавок — uGUI-оверлеи (программная сборка, `UiBuilder`), шкала цены с реакцией покупателя (доволен/думает/уходит), сводка дня и новый день, аренда за флагом (выключена). Баланс — в `EconomyConfig.asset`.
- Служебное: `Assets/Editor/DevChecks.cs` — проверки данных и приёмки этапа 1 (20 генераций) для батч-режима и меню «DustMan/Run Dev Checks»; сцены добавлены в Build Settings; GUID-ы новых ассетов/сцен прописаны вручную по паттерну проекта.
- Упрощения в рамках скоупа: возврат инструмента — ПКМ или клик по лотку; батарейки/линза меняются простым drag (без поддевания плоскогубцами); шкала цены показывает число (для проверки маржи на плейтесте).

Проведено адверсариальное ревью собранного кода (мульти-агентное + ручная верификация); исправлено 18 находок, ключевые: дедзона вращения глотала медленные обороты (референсный угол теперь не сдвигается под порогом), масштаб ячеек грязи в дебаг-сцене, сброс пройденной проверки при выкручивании винта, непрерывный перекрут при затяжке (захват не отпускается на пороге), ПКМ-отмена активного жеста, плоскогубцы работают и на собранном фонаре, интерполяция протирки между кадрами, восстановление частичного прогресса протирки/зачистки после выхода по Q, клики по uGUI не проваливаются в мир, чувствительность осмотра и радиус центра вращения вынесены в GestureConfig.

Приёмку этапов по разделу 6 скоупа должен пройти человек в редакторе (жесты — руками); автоматически проверены компиляция (Unity batchmode), загрузка данных и генератор (DevChecks).

## 2026-07-08

- Проведено ревью кода прототипа (5 скриптов) и документации; замечания переданы в чат (основные: утечка текстур в `CreateWhiteSprite`, дублирование кода, генерация уровня внутри `PlayerController`, неиспользуемый Input Actions).
- Разобраны все `.docx` из `incoming-files`, содержимое перенесено в markdown:
  - создан раздел `design/`: `gdd.md` (БГДД), `workbench-blueprint.md` (блюпринт верстака), `world.md` (мир падающего неба), `glossary.md` (глоссарий терминов);
  - `features/repair-mechanics.md` - жесты, инструменты, категории предметов, поломки, процедуры;
  - `game-assets/visual-style.md` и `game-assets/visual-prompts.md`.
- Обновлены индексы: главное содержание, список фич, разделы ассетов, статусы входящих файлов.
- Исходные `.docx` оставлены в `incoming-files` как оригиналы.
- Спроектирован AI-режиссёр (черновик): входные параметры, сводные индексы (давление/скука/фрустрация), ручки, ограничители, MVP - `design/ai-director.md`.
- Создан раздел `research/`; добавлена спецификация DirectorBench (бенчмарк ИИ-режиссёра, пре-регистрация v0.1) - `research/directorbench.md`.
- Написан `design/mvp-scope.md`: вертикальный срез (день-цикл с ремонтом фонаря), границы скоупа, техкаркас (SO-данные, GestureRecognizer), 7 этапов с критериями приёмки, черновой баланс, правила для агента-разработчика.

## 2026-06-28

- Проверена проектная папка `D:/WORK/DustMan`: отдельной документации проекта не было найдено.
- Создана корневая папка `ProjectDocs`, чтобы документация не мешала Unity и не импортировалась как игровые ассеты.
- Добавлен базовый каркас документации:
  - главное содержание;
  - служебный файл для Codex;
  - папка для подгружаемых файлов;
  - папка для описания фич проекта;
  - папка для описания ассетов игры.
- Добавлена папка `ProjectDocs/decomposition`.
- Созданы декомпозиции референсов:
  - Uncle Chop's Rocket Shop;
  - Reshine;
  - Trash Goblin.
- Прочитаны `.docx` файлы из `ProjectDocs/incoming-files`; их статус отмечен в индексе входящих файлов.
