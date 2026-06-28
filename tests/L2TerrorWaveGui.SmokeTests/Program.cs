using L2TerrorWaveGui;

var cases = new (string Name, Action Test)[]
{
    ("Terror Wave engine is embedded", EngineIsEmbedded),
    ("SNES copier headers are detected", CopierHeaders),
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

if (args is ["--integration-rom", var romPath])
{
    await RunRomIntegrationAsync(romPath);
}

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
static void EngineIsEmbedded()
{
    Equal(true, EmbeddedRandomizer.IsEmbedded);
}

static void CopierHeaders()
{
    Equal(0, RomInspector.GetRomDataOffset(0x280000));
    Equal(512, RomInspector.GetRomDataOffset(0x280200));
}

static async Task RunRomIntegrationAsync(string sourceRomPath)
{
    if (!File.Exists(sourceRomPath)) throw new FileNotFoundException("Integration ROM was not found.", sourceRomPath);
    var inspection = await RomInspector.InspectAsync(sourceRomPath);
    Equal(RomKind.Vanilla, inspection.Kind);
    Equal(true, inspection.HadCopierHeader);

    var artifactRoot = Path.Combine(Environment.CurrentDirectory, "artifacts", "integration-test");
    if (Directory.Exists(artifactRoot)) Directory.Delete(artifactRoot, recursive: true);
    Directory.CreateDirectory(artifactRoot);
    var inputPath = Path.Combine(artifactRoot, "vanilla-input.smc");
    var outputPath = Path.Combine(artifactRoot, "vanilla-input.v.246813579.smc");
    File.Copy(sourceRomPath, inputPath);

    try
    {
        var log = new List<string>();
        var runner = new RandomizerRunner(Path.Combine(artifactRoot, "engine"));
        var options = BaseOptions() with
        {
            RomPath = inputPath,
            Mode = GameMode.Vanilla,
            Seed = "246813579"
        };
        var exitCode = await runner.RunAsync(options, line =>
        {
            log.Add(line);
            Console.WriteLine(line);
        }, CancellationToken.None);

        Equal(0, exitCode);
        Equal(true, File.Exists(outputPath));
        Equal(0x400000L, new FileInfo(outputPath).Length);
        Equal(true, log.Any(line => line.Contains("Randomization completed successfully.", StringComparison.Ordinal)));
        Console.WriteLine("PASS  Embedded engine randomized the supplied vanilla ROM end to end");

        log.Clear();
        var randomizedOutputPath = Path.Combine(artifactRoot, "vanilla-input.cilmopst.246813580.smc");
        var randomizedOptions = BaseOptions() with
        {
            RomPath = inputPath,
            Mode = GameMode.Standard,
            Flags = RandomizerFlags.All.Select(option => option.Flag).ToArray(),
            Seed = "246813580"
        };
        exitCode = await runner.RunAsync(randomizedOptions, line =>
        {
            log.Add(line);
            Console.WriteLine(line);
        }, CancellationToken.None);

        Equal(0, exitCode);
        Equal(true, File.Exists(randomizedOutputPath));
        Equal(0x400000L, new FileInfo(randomizedOutputPath).Length);
        Equal(true, log.Any(line => line.Contains("Randomization completed successfully.", StringComparison.Ordinal)));
        Console.WriteLine("PASS  Embedded engine completed an all-category standard randomization");
    }
    finally
    {
        if (Directory.Exists(artifactRoot)) Directory.Delete(artifactRoot, recursive: true);
    }
}
