public enum WeaponCategory
{
    AntiInfantaria = 0,
    AntiTanque = 1,
    AntiAerea = 2,
    AntiNavio = 3
}

public static class WeaponCategoryLabels
{
    public static string GetAlias(WeaponCategory category)
    {
        switch (category)
        {
            case WeaponCategory.AntiInfantaria:
                return "anti-inf";
            case WeaponCategory.AntiTanque:
                return "anti-tanque";
            case WeaponCategory.AntiAerea:
                return "antiaérea";
            case WeaponCategory.AntiNavio:
                return "antinavio";
            default:
                return category.ToString();
        }
    }
}
