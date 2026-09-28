using UnityEngine;

/// Черновой баланс среза (mvp-scope §4). Все числа тут, не в коде.
[CreateAssetMenu(menuName = "DustMan/Economy Config", fileName = "EconomyConfig")]
public class EconomyConfig : ScriptableObject
{
    [Header("Деньги")]
    public int startingMoney = 100;

    [Header("Качество -> цена")]
    /// Модификатор цены при качестве 0.
    public float qualityModMin = 0.7f;
    /// Модификатор цены при качестве 100.
    public float qualityModMax = 1.3f;

    [Header("Расчёт качества (сумма 100)")]
    public int qualityPointsDefects = 40;
    public int qualityPointsClean = 30;
    public int qualityPointsCare = 30;
    /// Штраф за каждый перекрут или царапину.
    public int carePenaltyPerMistake = 10;

    [Header("Слоты дня")]
    public int daySlots = 4;
    public int marketSlotCost = 1;
    public int repairSlotCost = 1;
    public int saleSlotCost = 1;

    [Header("Рынок")]
    /// Сколько фонарей лежит на рынке (2-3 по скоупу).
    public int marketStockMin = 2;
    public int marketStockMax = 3;

    [Header("Продажа")]
    /// Цена <= честная * happy — покупатель доволен.
    public float happyPriceRatio = 0.9f;
    /// Цена <= честная * accept — покупатель думает, но берёт. Выше — уходит.
    public float acceptPriceRatio = 1.15f;
    /// Максимум шкалы цены на прилавке.
    public int priceScaleMax = 100;

    [Header("Аренда (по умолчанию выключена)")]
    public bool rentEnabled = false;
    public int rentAmount = 150;
    /// Аренда списывается в конце этого дня.
    public int rentDay = 7;
}
