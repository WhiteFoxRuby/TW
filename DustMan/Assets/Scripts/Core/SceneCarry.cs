/// Передача данных между сценами без PlayerPrefs (см. mvp-scope §5).
public static class SceneCarry
{
    /// Позиция игрока по X в хабе; восстанавливается при возврате и сбрасывается.
    public static float? PlayerX;
}
