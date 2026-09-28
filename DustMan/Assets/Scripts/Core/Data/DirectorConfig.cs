using UnityEngine;

/// Все числа AI-режиссёра (design/ai-director.md): веса сигналов, коридоры
/// индексов, шаги ручек, ограничители. Баланс крутится здесь, не в коде.
[CreateAssetMenu(menuName = "DustMan/Director Config", fileName = "DirectorConfig")]
public class DirectorConfig : ScriptableObject
{
    [Header("История и сглаживание")]
    /// Окно усреднения дневных сигналов.
    public int historyDays = 3;
    /// EMA: доля нового значения индекса за тик (0..1).
    [Range(0.05f, 1f)] public float indexSmoothing = 0.5f;

    [Header("Коридоры индексов")]
    public float pressureHigh = 0.65f;
    public float pressureLow = 0.25f;
    public float boredomHigh = 0.6f;
    public float frustrationHigh = 0.55f;

    [Header("Давление: сигналы и веса")]
    /// Runway (дней при текущем среднем расходе) выше порога = давления нет.
    public float runwayComfortDays = 4f;
    /// Денег меньше порога — давит независимо от тренда.
    public int lowMoneyThreshold = 40;
    /// Непроданный предмет висит дольше (дней) — деньги «заморожены».
    public int frozenDaysComfort = 2;
    public float pressureWeightRunway = 0.5f;
    public float pressureWeightLowMoney = 0.25f;
    public float pressureWeightFrozen = 0.25f;

    [Header("Скука: сигналы и веса")]
    /// Раньше этого срока новую зацепку не планируем.
    public int leadCooldownDays = 4;
    /// Сюжетный минимум: не позже этого срока зацепка выдаётся безусловно (кламп).
    public int leadMaxDays = 8;
    /// Столько дней без зацепки = максимум вклада в скуку.
    public int leadOverdueDays = 7;
    /// Сколько последних завозов рынка учитывать в повторяемости лута.
    public int repetitionWindowRestocks = 4;
    public float boredomWeightRepetition = 0.5f;
    public float boredomWeightLeadOverdue = 0.3f;
    public float boredomWeightIdleSlots = 0.2f;

    [Header("Фрустрация: сигналы и веса")]
    /// Ошибок жестов на один ремонт без роста фрустрации.
    public float errorsPerRepairComfort = 2f;
    /// Провалов проверки подряд без роста фрустрации.
    public int checkFailStreakComfort = 2;
    public float frustrationWeightErrors = 0.4f;
    public float frustrationWeightCheckFails = 0.4f;
    public float frustrationWeightWalkaways = 0.2f;

    [Header("Ручки: пределы и гистерезис")]
    /// Максимальный сдвиг ручки за день (решения — раз в день).
    public float knobMaxStepPerDay = 0.34f;
    /// Сдвиг реакции покупателя на полной ручке (доля к happy/accept ratio).
    public float buyerMoodRatioDelta = 0.08f;
    /// +фонарей на рынке при полной щедрости.
    public int stockBonusAtFullGenerosity = 1;
    /// Скидка в монетах при полной щедрости.
    public int priceDiscountAtFullGenerosity = 4;
    /// Вероятность склонить генерацию к минимуму дефектов при полной щедрости.
    [Range(0f, 1f)] public float defectBiasAtFullGenerosity = 0.75f;

    [Header("Ограничители")]
    /// Рынок никогда не пустой.
    public int marketMinStock = 1;
    /// Цена на рынке не падает ниже.
    public int marketPriceFloor = 8;
    /// Суммарный |сдвиг ручек| за неделю (бюджет отклонения).
    public float weeklyKnobBudget = 3f;

    [Header("Зацепка (гудящий предмет — плейсхолдер сюжета)")]
    /// Качество ремонта, с которого гудение усиливает цену.
    public int humQualityThreshold = 80;
    /// Бонус к честной цене гудящего предмета при тщательном ремонте.
    public float humPriceBonus = 0.15f;

    [Header("Телеметрия")]
    public int decisionLogSize = 32;
}
