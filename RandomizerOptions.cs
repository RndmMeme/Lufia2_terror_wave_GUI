using System.Globalization;

namespace L2TerrorWaveGui;

internal enum GameMode
{
    Standard,
    OpenWorld,
    FourKeys,
    CustomOpenWorld,
    Vanilla
}

internal enum ScalingMode
{
    Automatic,
    ForceScaling,
    NoScaling,
    SplitScaling
}

internal sealed record RandomizerOptions
{
    public required string ExecutablePath { get; init; }
    public required string RomPath { get; init; }
    public required GameMode Mode { get; init; }
    public required IReadOnlyCollection<char> Flags { get; init; }
    public string Seed { get; init; } = string.Empty;
    public decimal Randomness { get; init; } = 0.5m;
    public decimal Difficulty { get; init; } = 1.0m;
    public ScalingMode Scaling { get; init; }
    public decimal BossScaling { get; init; } = 0.75m;
    public decimal NonBossScaling { get; init; } = 0.75m;
    public string CustomSeedPath { get; init; } = string.Empty;
    public bool StartWithAirship { get; init; }
    public bool VeryRandomBosses { get; init; }
    public bool MonsterMash { get; init; }
    public bool AggressiveEnemies { get; init; }
    public bool EquipmentAnywhere { get; init; }
    public bool EasyMode { get; init; }
    public bool EnemiesRunAway { get; init; }
    public bool NoCapsuleMaster { get; init; }

    public RandomizerInvocation BuildInvocation()
    {
        var flags = Mode == GameMode.Vanilla
            ? "v"
            : string.Concat(Flags.OrderBy(flag => flag));

        if (Mode is GameMode.OpenWorld or GameMode.FourKeys or GameMode.CustomOpenWorld)
        {
            flags += "w";
        }

        var isOpenWorld = Mode is GameMode.OpenWorld or GameMode.FourKeys or GameMode.CustomOpenWorld;
        if (Mode == GameMode.FourKeys) flags += "fourkeys";
        if (Mode == GameMode.CustomOpenWorld) flags += "custom";
        if (Mode != GameMode.Vanilla)
        {
            if (isOpenWorld && StartWithAirship) flags += "airship";
            if (isOpenWorld && VeryRandomBosses) flags += "bossy";
            if (MonsterMash) flags += "monstermash";
            if (AggressiveEnemies) flags += "nothingpersonnelkid";
            if (EquipmentAnywhere) flags += "anywhere";
            if (EasyMode) flags += "easymodo";
            if (EnemiesRunAway) flags += "holiday";
            if (isOpenWorld && NoCapsuleMaster) flags += "nocap";
        }

        flags += !isOpenWorld ? string.Empty : Scaling switch
        {
            ScalingMode.ForceScaling => "scale",
            ScalingMode.NoScaling => "noscale",
            ScalingMode.SplitScaling => "splitscale",
            _ => string.Empty
        };

        var standardInput = new List<string>();
        if (isOpenWorld && Scaling == ScalingMode.SplitScaling)
        {
            standardInput.Add(BossScaling.ToString("0.00", CultureInfo.InvariantCulture));
            standardInput.Add(NonBossScaling.ToString("0.00", CultureInfo.InvariantCulture));
        }

        if (Mode == GameMode.CustomOpenWorld)
        {
            standardInput.Add(CustomSeedPath);
        }

        // One spare line lets Terror Wave dismiss its error prompt cleanly.
        standardInput.Add(string.Empty);

        return new RandomizerInvocation(
            [
                RomPath,
                flags,
                Seed,
                Randomness.ToString("0.00", CultureInfo.InvariantCulture),
                Difficulty.ToString("0.00", CultureInfo.InvariantCulture)
            ],
            standardInput);
    }
}

internal sealed record RandomizerInvocation(
    IReadOnlyList<string> Arguments,
    IReadOnlyList<string> StandardInputLines);

internal static class RandomizerFlags
{
    public static readonly IReadOnlyList<(char Flag, string Label)> All =
    [
        ('c', "Characters"),
        ('i', "Items & equipment"),
        ('l', "Learnable spells"),
        ('m', "Monsters"),
        ('o', "Monster movement"),
        ('p', "Capsule monsters"),
        ('s', "Shops"),
        ('t', "Treasure chests")
    ];
}
