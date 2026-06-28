using L2TerrorWaveGui;
using System.Security.Cryptography;

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
        var result = await runner.RunAsync(options, line =>
        {
            log.Add(line);
            Console.WriteLine(line);
        }, CancellationToken.None);

        var outputPath = Path.Combine(result.SeedDirectory, "vanilla-input.v.246813579.smc");
        Equal(0, result.ExitCode);
        Equal(true, result.Succeeded);
        Equal(true, File.Exists(outputPath));
        Equal(0x400000L, new FileInfo(outputPath).Length);
        AssertSeedArtifacts(result, "246813579");
        Equal(true, (await File.ReadAllTextAsync(result.SpoilerPath)).Contains(
            "only emits item/progression spoilers for Open World modes", StringComparison.Ordinal));
        Equal(false, File.Exists(Path.Combine(result.SeedDirectory, "source.smc")));
        Equal(true, log.Any(line => line.Contains("Randomization completed successfully.", StringComparison.Ordinal)));
        Console.WriteLine("PASS  Embedded engine randomized the supplied vanilla ROM end to end");

        log.Clear();
        var randomizedOptions = BaseOptions() with
        {
            RomPath = inputPath,
            Mode = GameMode.Standard,
            Flags = RandomizerFlags.All.Select(option => option.Flag).ToArray(),
            Seed = "246813580"
        };
        result = await runner.RunAsync(randomizedOptions, line =>
        {
            log.Add(line);
            Console.WriteLine(line);
        }, CancellationToken.None);

        var randomizedOutputPath = Path.Combine(result.SeedDirectory, "vanilla-input.cilmopst.246813580.smc");
        Equal(0, result.ExitCode);
        Equal(true, result.Succeeded);
        Equal(true, File.Exists(randomizedOutputPath));
        Equal(0x400000L, new FileInfo(randomizedOutputPath).Length);
        AssertSeedArtifacts(result, "246813580");
        Equal(true, (await File.ReadAllTextAsync(result.SpoilerPath)).Contains(
            "only emits item/progression spoilers for Open World modes", StringComparison.Ordinal));
        Equal(false, File.Exists(Path.Combine(result.SeedDirectory, "source.smc")));
        Equal(true, log.Any(line => line.Contains("Randomization completed successfully.", StringComparison.Ordinal)));
        Console.WriteLine("PASS  Embedded engine completed an all-category standard randomization");

        var firstStandardHash = await FileSha256Async(randomizedOutputPath);
        var replicaSourceDirectory = Path.Combine(artifactRoot, "replica");
        Directory.CreateDirectory(replicaSourceDirectory);
        var replicaInputPath = Path.Combine(replicaSourceDirectory, "vanilla-input.smc");
        File.Copy(inputPath, replicaInputPath);
        var replicaResult = await runner.RunAsync(
            randomizedOptions with { RomPath = replicaInputPath },
            _ => { },
            CancellationToken.None);
        var replicaOutputPath = Path.Combine(replicaResult.SeedDirectory, "vanilla-input.cilmopst.246813580.smc");
        Equal(firstStandardHash, await FileSha256Async(replicaOutputPath));
        Console.WriteLine("PASS  Same seed is byte-identical from a different source folder");

        log.Clear();
        var openWorldOptions = BaseOptions() with
        {
            RomPath = inputPath,
            Mode = GameMode.OpenWorld,
            Flags = RandomizerFlags.All.Select(option => option.Flag).ToArray(),
            Seed = "246813581"
        };
        result = await runner.RunAsync(openWorldOptions, line =>
        {
            log.Add(line);
            Console.WriteLine(line);
        }, CancellationToken.None);

        var openWorldOutputPath = Path.Combine(result.SeedDirectory, "vanilla-input.cilmopstw.246813581.smc");
        Equal(0, result.ExitCode);
        Equal(true, result.Succeeded);
        Equal(true, File.Exists(openWorldOutputPath));
        AssertSeedArtifacts(result, "246813581");
        Equal(true, (await File.ReadAllTextAsync(result.SpoilerPath)).StartsWith("LUFIA 2 CRESTING WAVE", StringComparison.Ordinal));
        Equal(true, (await File.ReadAllTextAsync(Path.Combine(result.SeedDirectory, "run.json"))).Contains(
            "Terror Wave Open World spoiler", StringComparison.Ordinal));
        Console.WriteLine("PASS  Open World seed preserved Terror Wave's progression spoiler");
    }
    finally
    {
        if (Directory.Exists(artifactRoot)) Directory.Delete(artifactRoot, recursive: true);
    }
}

static void AssertSeedArtifacts(RandomizerRunResult result, string expectedSeed)
{
    Equal(expectedSeed, Path.GetFileName(result.SeedDirectory));
    Equal(true, File.Exists(result.LogPath));
    Equal(true, File.Exists(result.SpoilerPath));
    Equal(true, File.Exists(Path.Combine(result.SeedDirectory, "events.txt")));
    Equal(true, File.Exists(Path.Combine(result.SeedDirectory, "run.json")));
    Equal(true, new FileInfo(result.LogPath).Length > 0);
    Equal(true, new FileInfo(result.SpoilerPath).Length > 0);
    Equal(true, new FileInfo(Path.Combine(result.SeedDirectory, "events.txt")).Length > 0);
}

static async Task<string> FileSha256Async(string path)
{
    await using var stream = File.OpenRead(path);
    return Convert.ToHexString(await SHA256.HashDataAsync(stream));
}
