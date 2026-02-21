using UnityEngine;

public enum TeamId
{
    Neutral = -1,
    Green = 0,
    Red = 1,
    Blue = 2,
    Yellow = 3
}

public static class GameColors
{
    public static readonly Color TeamGreen = new Color(144f / 255f, 238f / 255f, 144f / 255f);
    public static readonly Color TeamRed = new Color(255f / 255f, 155f / 255f, 155f / 255f);
    public static readonly Color TeamBlue = new Color(168f / 255f, 168f / 255f, 255f / 255f);
    public static readonly Color TeamYellow = new Color(255f / 255f, 246f / 255f, 141f / 255f);
}

public static class TeamUtils
{
    public static Color GetColor(TeamId teamId)
    {
        switch (teamId)
        {
            case TeamId.Green: return GameColors.TeamGreen;
            case TeamId.Red: return GameColors.TeamRed;
            case TeamId.Blue: return GameColors.TeamBlue;
            case TeamId.Yellow: return GameColors.TeamYellow;
            default: return Color.white;
        }
    }

    public static string GetName(TeamId teamId)
    {
        switch (teamId)
        {
            case TeamId.Neutral: return "neutro";
            case TeamId.Green: return "verde";
            case TeamId.Red: return "vermelho";
            case TeamId.Blue: return "azul";
            case TeamId.Yellow: return "amarelo";
            default: return $"time {(int)teamId}";
        }
    }

    public static Sprite GetTeamSprite(UnitData data, TeamId teamId, bool preferTransportSprite = false)
    {
        if (data == null)
            return null;

        if (preferTransportSprite && data.spriteTransport != null)
            return data.spriteTransport;

        switch (teamId)
        {
            case TeamId.Green: return data.spriteGreen != null ? data.spriteGreen : data.spriteDefault;
            case TeamId.Red: return data.spriteRed != null ? data.spriteRed : data.spriteDefault;
            case TeamId.Blue: return data.spriteBlue != null ? data.spriteBlue : data.spriteDefault;
            case TeamId.Yellow: return data.spriteYellow != null ? data.spriteYellow : data.spriteDefault;
            default: return data.spriteDefault;
        }
    }

    public static Sprite GetTeamSprite(UnitLayerMode mode, TeamId teamId, Sprite fallbackDefault = null)
    {
        switch (teamId)
        {
            case TeamId.Green: return mode.spriteGreen != null ? mode.spriteGreen : (mode.spriteDefault != null ? mode.spriteDefault : fallbackDefault);
            case TeamId.Red: return mode.spriteRed != null ? mode.spriteRed : (mode.spriteDefault != null ? mode.spriteDefault : fallbackDefault);
            case TeamId.Blue: return mode.spriteBlue != null ? mode.spriteBlue : (mode.spriteDefault != null ? mode.spriteDefault : fallbackDefault);
            case TeamId.Yellow: return mode.spriteYellow != null ? mode.spriteYellow : (mode.spriteDefault != null ? mode.spriteDefault : fallbackDefault);
            default: return mode.spriteDefault != null ? mode.spriteDefault : fallbackDefault;
        }
    }

    public static Sprite GetTeamSprite(ConstructionData data, TeamId teamId)
    {
        if (data == null)
            return null;

        switch (teamId)
        {
            case TeamId.Green: return data.spriteGreen != null ? data.spriteGreen : data.spriteDefault;
            case TeamId.Red: return data.spriteRed != null ? data.spriteRed : data.spriteDefault;
            case TeamId.Blue: return data.spriteBlue != null ? data.spriteBlue : data.spriteDefault;
            case TeamId.Yellow: return data.spriteYellow != null ? data.spriteYellow : data.spriteDefault;
            default: return data.spriteDefault;
        }
    }

    public static bool ShouldFlipX(TeamId teamId)
    {
        return teamId == TeamId.Red || teamId == TeamId.Yellow;
    }
}
