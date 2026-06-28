using L2TerrorWaveGui;

var cases = new (string Name, Action Test)[]
{
    ("Standard mode ignores Open World-only controls", StandardMode),
    ("Four Keys supplies flags and split-scaling input", FourKeysMode),
    ("Vanilla mode remains vanilla", VanillaMode),
    ("Custom world supplies the template path", CustomWorldMode)
};

foreach (var testCase in cases)
{
    testCase.Test();
    Console.WriteLine($"PASS  {testCase.Name}");
}

Console.WriteLine($"{cases.Length} smoke tests passed.");

static void StandardMode()
{
    var invocation = BaseOptions() with
    {
        Mode = GameMode.Standard,
        Flags = ['m', 'c'],
        Scaling = ScalingMode.SplitScaling,
        StartWithAirship = true,
        VeryRandomBosses = true,
        NoCapsuleMaster = true,
        MonsterMash = true
    };
    var result = invocation.BuildInvocation();
    Equal("cmmonstermash", result.Arguments[1]);
    SequenceEqual([string.Empty], result.StandardInputLines);
}

static void FourKeysMode()
{
    var invocation = BaseOptions() with
    {
        Mode = GameMode.FourKeys,
        Flags = ['p'],
        Scaling = ScalingMode.SplitScaling,
        BossScaling = 0.8m,
        NonBossScaling = 0.6m,
        StartWithAirship = true,
        VeryRandomBosses = true,
        NoCapsuleMaster = true
    };
    var result = invocation.BuildInvocation();
    Equal("pwfourkeysairshipbossynocapsplitscale", result.Arguments[1]);
    SequenceEqual(["0.80", "0.60", string.Empty], result.StandardInputLines);
}

static void VanillaMode()
{
    var invocation = BaseOptions() with
    {
        Mode = GameMode.Vanilla,
        Flags = ['c', 'i'],
        Scaling = ScalingMode.ForceScaling,
        EasyMode = true,
        MonsterMash = true
    };
    var result = invocation.BuildInvocation();
    Equal("v", result.Arguments[1]);
    SequenceEqual([string.Empty], result.StandardInputLines);
}

static void CustomWorldMode()
{
    var invocation = BaseOptions() with
    {
        Mode = GameMode.CustomOpenWorld,
        Flags = ['t'],
        CustomSeedPath = @"C:\seeds\my world.txt"
    };
    var result = invocation.BuildInvocation();
    Equal("twcustom", result.Arguments[1]);
    SequenceEqual([@"C:\seeds\my world.txt", string.Empty], result.StandardInputLines);
}

static RandomizerOptions BaseOptions() => new()
{
    ExecutablePath = @"C:\randomizer.exe",
    RomPath = @"C:\lufia.sfc",
    Mode = GameMode.Standard,
    Flags = ['c'],
    Seed = "123456",
    Randomness = 0.5m,
    Difficulty = 1m
};

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
}

static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual)
{
    if (!expected.SequenceEqual(actual))
        throw new InvalidOperationException("Sequences differ.");
}
