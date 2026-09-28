# Neural Director Roadmap

План перехода от текущего rule-based AI-режиссёра к нейросетевому режиссёру, который читает историю событий игрока и выбирает следующий блок контента.

Создано: 2026-07-10. Статус: рабочий план, не скоуп текущего вертикального среза.

## 1. Коротко

Нейрорежиссёр не должен генерировать правила игры, цены, поломки или сюжетный текст напрямую. Он выбирает один или несколько заранее описанных блоков из безопасной библиотеки:

```text
история событий игрока -> оценка скрытого состояния -> допустимые блоки -> выбор блока -> валидация -> применение
```

Главная цель - сделать режиссёра заменяемой политикой. Текущий `GameDirector` остаётся безопасным baseline-режиссёром, а GRU/Transformer сначала работает в shadow mode: предлагает решения рядом, но не влияет на игру.

## 2. Что уже есть

- Rule-based каркас: `Assets/Scripts/Director/GameDirector.cs`.
- Базовый runtime-лог событий: `Assets/Scripts/Director/DirectorTelemetry.cs`; пишет события в память, `Debug.Log` и JSONL-файлы в `Telemetry/local/`.
- Базовый builder наблюдений: `Assets/Scripts/Director/DirectorObservation.cs`; собирает `DirectorObservation`, окно последних событий и `observation_hash`.
- Базовый policy-контракт: `Assets/Scripts/Director/DirectorPolicy.cs`; `GameDirector` вызывает `IDirectorPolicy`, а текущая логика вынесена в `RuleBasedDirectorPolicy`.
- Базовый валидатор действий и baseline-policy: `Assets/Scripts/Director/DirectorActionValidator.cs`; добавлены `DirectorActionValidator`, `RandomDirectorPolicy` и `ReplayDirectorPolicy`.
- Базовая библиотека блоков: `Assets/Scripts/Director/DirectorActionLibrary.cs`; описывает первые `DirectorAction`-блоки и строит `allowed_action_ids`/`blocked_action_ids`.
- Базовый headless-симулятор: `Assets/Scripts/Director/DirectorSimulation.cs`; прогоняет дни с профилем игрока и любой `IDirectorPolicy`, пишет JSONL в `Telemetry/simulations/`.
- Базовый batch-runner: `Assets/Scripts/Director/DirectorSimulationBatch.cs`; сравнивает `profile × policy × seed`, пишет `runs.jsonl`, `summary.jsonl` и `summary.csv`.
- Индексы: давление, скука, фрустрация.
- Ручки MVP: щедрость рынка, настроение покупателя, момент зацепки.
- Ограничители: гистерезис, клампы, недельный бюджет отклонения.
- Отладочная панель: `DirectorDebugPanel.cs`.
- Конфиг чисел: `Assets/Data/Resources/DirectorConfig.asset`.
- Документ роли режиссёра: [AI-режиссёр](./ai-director.md).
- Исследовательская рамка: [DirectorBench](../research/directorbench.md).

## 3. Что нужно получить

Нужно отделить три слоя:

1. **Среда** - игра и её факты: деньги, слоты, предметы, ремонт, продажи, события мира.
2. **Политика режиссёра** - правило, GRU, Transformer, LLM или replay, которые выбирают действие.
3. **Валидатор** - обязательный слой, который проверяет кулдауны, бюджет, доступность блока и запреты.

Финальная архитектура:

```text
Game events
  -> DirectorTelemetry
  -> DirectorObservationBuilder
  -> IDirectorPolicy
  -> DirectorActionValidator
  -> DirectorActionApplier
  -> Game systems
```

## 4. Принципы

- Нейросеть не решает вне грамматики. Она выбирает из разрешённых действий.
- Правила, которые игрок выучил, не меняются: стоимость слотов, базовые свойства инструментов, поведение ремонта.
- Любое действие модели проходит валидацию.
- Нейросеть сначала советует, потом управляет.
- Награда и тесты должны быть детерминированными, без LLM-судьи в контуре оценки.
- Данные важнее модели: без событийного лога GRU/Transformer будет декоративным слоем.

## 5. Минимальная грамматика событий

Событийный лог - первый обязательный шаг. События пишутся в память, в `Debug.Log` для отладки и в JSONL для будущего датасета.

Базовые события:

| Событие | Когда пишется | Зачем режиссёру |
| --- | --- | --- |
| `DayStarted` | утром | начало контекста дня |
| `SlotSpent` | при списании слота | реальное распределение времени |
| `MarketOpened` | игрок открыл рынок | интерес к снабжению |
| `MarketRestocked` | рынок сгенерирован | что мир предложил игроку |
| `ItemBought` | покупка предмета | выбор игрока и давление денег |
| `RepairStarted` | вход на верстак или первое действие ремонта | начало трудозатраты |
| `DefectRevealed` | дефект стал известен | качество диагностики |
| `GestureError` | перекрут, лишняя царапина, перегрев в будущем | фрустрация и навык |
| `RepairCheckFailed` | проверка не прошла | диагностическое застревание |
| `RepairCompleted` | ремонт завершён | качество результата |
| `CounterOpened` | игрок пришёл продавать | готовность к торговле |
| `SaleAttempted` | предложена цена | поведение в продаже |
| `SaleCompleted` | покупатель купил | экономика и успешность цикла |
| `SaleRefused` | покупатель ушёл | фрустрация, цена, спрос |
| `DayEnded` | сводка дня | итог дня |
| `DirectorDecision` | режиссёр выбрал действие | объяснимость и обучение |
| `DirectorActionApplied` | действие реально применилось | связь решения с последствиями |
| `LeadSeen` | игрок увидел зацепку | нарративная доставка |
| `LeadAcquired` | игрок взял зацепку | заинтересованность |
| `LeadResolved` | зацепка починена/продана/оставлена | судьба сюжета |

Минимальная структура записи:

```text
session_id
event_id
event_type
day
slot
timestamp
money
slots_left
current_item_id
item_quality
active_defect_ids
payload_json
```

Позднее добавить:

```text
player_archetype_label
director_policy_id
director_action_id
observation_hash
allowed_action_ids
outcome_window_id
```

## 6. Библиотека действий режиссёра

Нейросеть выбирает только блоки из библиотеки. Каждый блок описывается данными.

Минимальные поля:

```text
id
title
description
level: slot/day/week/story
tags
target_index: pressure/boredom/frustration/narrative/economy
preconditions
cooldown_days
budget_cost
risk_level
payload
```

Примеры блоков для текущего среза:

| Блок | Эффект |
| --- | --- |
| `market_generous` | больше офферов, ниже цена, меньше дефектов |
| `market_strict` | меньше щедрости, если давления почти нет |
| `buyer_friendly` | покупатель принимает чуть более высокую цену |
| `buyer_strict` | покупатель строже, если игрок слишком стабилен |
| `story_lead_humming_item` | на рынке появляется гудящий предмет |
| `familiar_item` | знакомая категория предмета |
| `easy_defects` | меньше скрытых или блокирующих дефектов |
| `varied_defects` | меньше повторяемости последних дефектов |

2026-07-10: базовая версия библиотеки реализована в `Assets/Scripts/Director/DirectorActionLibrary.cs`. В текущем срезе применяются `hold`, `market_generous`, `market_strict`, `buyer_friendly`, `buyer_strict`, `story_lead_humming_item`. Будущие блоки `familiar_item`, `easy_defects`, `varied_defects` уже заведены как данные, но не попадают в allowed mask, пока нет runtime-эффекта.

Будущие блоки после расширения игры:

| Блок | Эффект |
| --- | --- |
| `debris_fall_event` | спрос на фонари, фильтры, ремни, инструменты |
| `water_contamination` | спрос на фильтры, рост цены чистящих материалов |
| `collector_visit` | шанс продать необычную вещь дороже |
| `repair_note_found` | подсказка в блокнот |
| `blackness_warning` | мягкое введение черноты |
| `black_market_request` | запрос на заражённый предмет с репутационным риском |

## 7. Observation Builder

`DirectorObservation` - снимок, который получает политика. Он не должен быть Unity-зависимым.

2026-07-10: базовая версия реализована в `Assets/Scripts/Director/DirectorObservation.cs`. Она ещё не является финальным контрактом ML-модели, но уже связывает события, текущее состояние и решения rule-based режиссёра через `observation_hash`.

Минимальные признаки:

```text
day
slots_left
money
spent_today
earned_today
days_since_last_lead
current_item_age_days
current_item_repair_completed
last_market_signatures
recent_gesture_errors
recent_check_fails
recent_sale_attempts
recent_walkaways
recent_unused_slots
pressure
boredom
frustration
```

Для GRU/Transformer дополнительно нужна последовательность событий:

```text
last_events[0..N]
```

Каждое событие превращается в токен:

```text
event_type_embedding
item_id_embedding
defect_id_embeddings
director_action_embedding
numeric_features
time_delta
```

Рекомендуемое окно для старта: 64 события. После появления недельного цикла - 128-256 событий.

## 8. Интерфейсы в коде

Нужный контракт:

```csharp
public interface IDirectorPolicy
{
    DirectorAction Decide(DirectorObservation observation, DirectorActionMask mask);
}
```

2026-07-10: базовый контракт реализован в `Assets/Scripts/Director/DirectorPolicy.cs`. `DirectorAction` пока описывает MVP-ручки: цель щедрости рынка, цель настроения покупателя и планирование сюжетной зацепки. `GameDirector` остаётся applier-слоем: применяет гистерезис, недельный бюджет, клампы и пишет `policy_id`/`action_id` в телеметрию.

2026-07-10: добавлен `DirectorActionValidator`. Он стоит между policy и применением действия, нормализует цели ручек, применяет `DirectorActionMask`, блокирует ранние зацепки и логирует `validation_changed`/`validation_notes`. Это первый safety-слой для будущей GRU/Transformer-policy.

Реализации:

```text
RuleBasedDirectorPolicy
RandomDirectorPolicy
ReplayDirectorPolicy
GruDirectorPolicy
TransformerDirectorPolicy
LlmDirectorPolicy
```

В MVP сначала нужны только:

```text
RuleBasedDirectorPolicy
RandomDirectorPolicy
ReplayDirectorPolicy
```

`ReplayDirectorPolicy` полезна для регрессионных тестов: она воспроизводит заранее записанные решения.

## 9. GRU-политика

GRU - первая нейросетевая модель, потому что она дешевле и проще Transformer.

Вход:

```text
последовательность событий за последние N событий
текущая агрегированная observation
маска допустимых действий
```

Выходы:

```text
pressure_hat
boredom_hat
frustration_hat
player_style_logits
action_logits
risk_hat
```

Где:

- `pressure_hat`, `boredom_hat`, `frustration_hat` - оценка скрытого состояния.
- `player_style_logits` - стиль поведения: экономный, рискованный, коллекционерский, быстрый, осторожный.
- `action_logits` - вероятности блоков.
- `risk_hat` - риск плохого исхода в ближайшем окне: застревание, отказ продажи, экономическая просадка.

В рантайме:

```text
action = argmax(masked_action_logits)
```

или с мягким sampling:

```text
action = sample(masked_action_logits, temperature)
```

Sampling разрешать только после тестов, иначе поведение будет труднее воспроизводить.

## 10. Transformer-политика

Transformer нужен не на первом этапе. Он оправдан, когда появятся:

- длинные нарративные цепочки;
- разные категории предметов;
- вылазки;
- чернота;
- покупатели с историей;
- недельные события;
- достаточно телеметрии.

Преимущество Transformer - он лучше видит дальние зависимости: игрок давно избегает продажи, часто оставляет необычные предметы, реагирует на определённые типы покупателей, забывает сюжетную зацепку.

Рекомендуемая роль:

```text
Transformer = долгосрочный планировщик
Rule-based validator = безопасность и правила
```

Не рекомендуется:

```text
Transformer напрямую меняет экономику, правила ремонта или цены
```

## 11. Обучение

### Этап 1. Imitation learning от rule-based директора

Текущий `GameDirector` - учитель. Модель учится повторять его решения.

Данные:

```text
observation
event_history
allowed_actions
rule_based_action
```

Польза:

- модель учится формату задачи;
- появляется baseline-качество;
- можно проверять, что она хотя бы копирует безопасную политику.

### Этап 2. Prediction heads

Учить модель предсказывать ближайшие исходы:

```text
будет ли продажа успешной
будет ли провал проверки
сколько ошибок жестов будет в следующем ремонте
насколько изменятся деньги
будет ли игрок бездействовать
```

Это полезнее раннего RL, потому что даёт модели представление о последствиях.

### Этап 3. Симуляторные прогоны

Создать простых симулированных игроков:

| Профиль | Поведение |
| --- | --- |
| Экономный | покупает дешёвое, продаёт быстро |
| Коллекционер | держит необычные вещи, медленно продаёт |
| Быстрый | чинит быстро, ошибается чаще |
| Осторожный | проверяет всё, ошибок меньше, цикл медленнее |
| Рискованный продавец | часто завышает цену |
| Уставший | чаще оставляет слоты пустыми |

Симулятор не должен быть “истиной”, он нужен для дешёвых регрессионных прогонов.

### Этап 4. Offline evaluation

Модель принимает решения на записанных историях, но не влияет на игру. Сравниваем:

```text
rule-based action
neural action
validator result
predicted outcome
actual outcome
```

### Этап 5. Shadow mode

В игре работает rule-based директор. Нейросеть рядом предлагает действие, и мы логируем расхождения:

```text
applied_action = rule_based_action
shadow_action = neural_action
```

Только после этого можно дать модели управлять небольшой частью ручек.

### Этап 6. Controlled rollout

Сначала нейросеть управляет только безопасными ручками:

- выбор знакомого/разнообразного лута;
- момент мягкой зацепки;
- тип покупателя;
- фоновое событие без экономического удара.

Запрещено на первом rollout:

- менять аренду;
- резко ухудшать рынок;
- ускорять черноту;
- менять правила ремонта;
- выдавать тяжёлые события без инструментов ответа.

## 12. Тестирование

### Unit-тесты

- Observation builder корректно собирает признаки.
- Маска действий запрещает недоступные блоки.
- Валидатор режет действия с кулдауном, превышением бюджета и нарушением предусловий.
- Событийный лог сохраняет причинную цепочку.

### Golden scenarios

Фиксированные истории:

| Сценарий | Ожидаемая реакция |
| --- | --- |
| Мало денег, много отказов продаж | щедрее рынок, мягче покупатель |
| Много ошибок жестов | знакомые предметы, меньше новых механик |
| Много пустых слотов | лёгкая зацепка или полезный заказ |
| Долго нет сюжета | зацепка в доступном канале |
| Игрок стабилен и богат | больше разнообразия, но без наказания |

### Monte Carlo

Тысячи прогонов на симулированных профилях. Проверять:

- не спамит ли один блок;
- не уводит ли экономику в минус;
- не превращает ли игру в постоянную помощь;
- не нарушает ли кулдауны;
- не теряет ли сюжетные зацепки.

### Метрики

```text
fit_to_target_curve
pressure_recovery_days
boredom_recovery_days
frustration_recovery_days
action_diversity
budget_violations
cooldown_violations
lead_delivery_rate
lead_resolution_rate
economy_survival_rate
rubber_banding_visibility
```

`rubber_banding_visibility` пока измеряется плейтест-вопросом: “казалось ли, что игра подыгрывает?”.

## 13. Данные и хранение

Для разработки достаточно JSONL:

```text
ProjectDocs/research/runs/
```

Но игровые логи лучше хранить вне `ProjectDocs`, чтобы не смешивать документацию и датасеты. Предлагаемый путь:

```text
Telemetry/
  local/
  simulations/
  playtests/
```

`Telemetry` не должна попадать в Unity `Assets`.

2026-07-10: симуляционные датасеты уже пишутся в `Telemetry/simulations/` через `DirectorSimulation`. Строка хранит `run_id`, `profile_id`, `policy_id`, `seed`, `day`, `observation_hash`, `allowed_action_ids`, `blocked_action_ids`, `chosen_action_id`, индексы режиссёра, результат валидации и итог дня игрока (`money_before/after`, `spent`, `earned`, `bought`, `repaired`, `sold`, `sale_refused`, `item_quality`).

2026-07-10: batch-сводки пишутся в `Telemetry/simulations/batches/<batchId>/`. Основные файлы: `runs.jsonl` для run-level метрик, `summary.jsonl` для машинного чтения агрегатов и `summary.csv` для быстрой ручной проверки.

Минимальная строка JSONL:

```json
{"session_id":"s1","event_type":"SaleCompleted","day":3,"slot":1,"money":128,"payload":{"price":72,"fair_price":68}}
```

## 14. Связь с DirectorBench

DirectorBench - исследовательский бенчмарк для проверки компетентности и верности модели. Для DustMan он полезен как методология:

- дискретная грамматика блоков;
- детерминированная награда;
- симулированные профили;
- hold-out профили;
- сравнение rule-based, random, neural и LLM-policy;
- проверка Goodhart-разрыва.

Но DustMan-продукт и DirectorBench-исследование не должны смешиваться слишком рано. В игре сначала нужен устойчивый и понятный режиссёр. В исследовании можно проверять более рискованные модели.

## 15. Поэтапный план

### Фаза A. Фундамент

Цель: сделать текущий директор заменяемым и измеримым.

Задачи:

- Ввести `DirectorEvent` и событийный лог. Базовый слой `DirectorTelemetry` уже добавлен; следующий шаг - стабилизация схемы payload.
- Ввести `DirectorObservation`. Базовый слой уже добавлен; следующий шаг - превратить его в контракт для policy.
- Ввести `DirectorAction`. Базовый слой добавлен.
- Ввести `DirectorActionMask`. Базовый слой добавлен.
- Ввести `IDirectorPolicy`. Базовый слой добавлен.
- Перенести текущую логику в `RuleBasedDirectorPolicy`. Готово базово: `GameDirector` вызывает policy и применяет результат.
- Добавить `RandomDirectorPolicy` для baseline. Готово базово.
- Добавить `ReplayDirectorPolicy` для тестов. Готово базово.
- Ввести `DirectorActionValidator`. Готово базово; следующий шаг - расширять маску и причины отказа под библиотеку блоков.
- Ввести `DirectorActionLibrary` и action mask по блокам. Готово базово.

Критерий готовности:

```text
Игра ведёт себя как сейчас, но все решения проходят через интерфейс policy.
```

### Фаза B. Библиотека блоков

Цель: перестать мыслить ручками напрямую и перейти к событиям/блокам.

Задачи:

- Описать первые 8-12 блоков. Базово готово: 6 применяемых + 3 future-блока.
- Добавить предусловия, кулдауны, бюджет. Базово готово на уровне кодовой библиотеки.
- Добавить валидатор. Базово готово.
- Залогировать action mask. Базово готово: `allowed_action_ids` и `blocked_action_ids` пишутся в `DirectorDecision`.

Критерий готовности:

```text
Режиссёр выбирает блок, валидатор применяет или отклоняет его с причиной.
```

### Фаза C. Симулятор

Цель: дешёвые прогоны до обучения.

Задачи:

- Сделать headless-симулятор дня. Базово готово: `DirectorSimulation` прогоняет день, покупку, ремонт, продажу/отказ и связывает observation/action/outcome.
- Описать 5-6 профилей игроков. Сейчас есть стартовые `balanced` и `struggling`.
- Запускать 1000+ дней симуляций. Базовый batch-runner готов; следующий шаг - поднять число seeds/дней и добавить hold-out профили.
- Сохранять JSONL. Базово готово: `Telemetry/simulations/<runId>.jsonl`.

Критерий готовности:

```text
Можно сравнить random и rule-based режиссёра без Unity Play Mode.
```

### Фаза D. GRU shadow model

Цель: первая нейросеть без влияния на игру.

Задачи:

- Подготовить датасет из симулятора и rule-based решений.
- Обучить GRU копировать rule-based policy.
- Добавить prediction heads.
- Подключить `GruDirectorPolicy` в shadow mode.
- Логировать расхождения.

Критерий готовности:

```text
GRU стабильно предлагает валидные действия и близко повторяет baseline.
```

### Фаза E. Controlled neural rollout

Цель: дать модели безопасную часть управления.

Задачи:

- Разрешить модели только низкорисковые блоки.
- Оставить rule-based fallback.
- Включить аварийный выключатель.
- Сравнить с rule-based на плейтесте.

Критерий готовности:

```text
Игроки не замечают подстройку как нечестную, экономика не ломается, зацепки доставляются.
```

### Фаза F. Transformer / LLM experiments

Цель: исследовать долгую историю и нарратив.

Задачи:

- Подключить Transformer как долгосрочную policy.
- Подключить LLM только через грамматику JSON-блоков.
- Сравнить с GRU и rule-based на hold-out профилях.

Критерий готовности:

```text
Transformer/LLM показывает пользу сверх GRU без роста нарушений ограничений.
```

## 16. Ближайший практический шаг

Не обучать модель сейчас. Сначала:

1. Починить списание слота ремонта: слот должен тратиться при начале ремонта или первом значимом действии, а не только при завершении.
2. Починить доставку зацепки: `LastLeadDay` должен обновляться не при генерации рынка, а при факте `LeadSeen` или `LeadAcquired`.
3. Добавить `DirectorEvent` и логирование базовых событий. Готово базово.
4. Вынести `DirectorObservation` и `IDirectorPolicy`. Готово базово: observation и rule-based policy уже соединены.
5. Добавить `DirectorActionValidator` и простые `RandomDirectorPolicy`/`ReplayDirectorPolicy` для baseline и регрессионных прогонов. Готово базово.
6. Добавить библиотеку `DirectorAction`-блоков и более строгий `DirectorActionMask` с cooldown/budget/precondition причинами. Готово базово.
7. Headless-симулятор/датасет готов базово: `DirectorSimulation` уже сравнимо прогоняет policy на action-блоках и пишет JSONL.
8. Batch-runner и первые метрики сравнения готовы базово: `balanced/struggling × rule_based/random × seeds`, агрегаты survival/action_diversity/budget_validation/lead_delivery.
9. Следующий слой - расширить профили игрока, поднять объём прогонов до 1000+ дней, добавить pass/fail-пороги качества policy и экспорт train/validation split для GRU.

После этих шагов текущий rule-based режиссёр станет нормальным baseline для обучения.

## 17. Открытые вопросы

- Где граница между событийным режиссёром и нарративным режиссёром?
- Считать ли `LeadSeen` просмотром рынка или только покупкой предмета?
- Когда списывать слот ремонта: вход на верстак, первое действие или завершение?
- Должна ли модель видеть реальные секунды сессии или только игровые слоты?
- Какие блоки можно дать нейросети в первом rollout без риска для доверия игрока?
- Нужен ли отдельный локальный формат датасета до подключения внешних ML-инструментов?

## 18. Связанные документы

- [AI-режиссёр](./ai-director.md) - текущая роль, индексы и ручки.
- [MVP-скоуп](./mvp-scope.md) - границы вертикального среза.
- [БГДД](./gdd.md) - кор, мета и циклы.
- [DirectorBench](../research/directorbench.md) - исследовательская рамка для оценки AI-режиссёров.
