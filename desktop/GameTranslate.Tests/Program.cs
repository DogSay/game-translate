using GameTranslate.Core;
using System.IO.Compression;
using System.Security.Cryptography;

var tests = new (string Name, Func<Task> Run)[]
{
    ("detects nested Unreal IoStore and pak layout", Sync(DetectsNestedUnreal)),
    ("detects Unity layout", Sync(DetectsUnity)),
    ("chooses the most complete Unreal Paks directory", Sync(ChoosesMainPaksDirectory)),
    ("parses Simplified Chinese locres candidates", Sync(ParsesLocresCandidates)),
    ("protects placeholders and markup", Sync(ProtectsTokens)),
    ("round trips quoted localization CSV", Sync(RoundTripsCsv)),
    ("only tool-owned patches are restorable", Sync(ClassifiesPatches)),
    ("restore disables owned patch without deleting it", Sync(RestoresPatch)),
    ("patch disabling rolls back when a later rename fails", Sync(RollsBackPartialPatchDisable)),
    ("parses base pak metadata", Sync(ParsesPakInfo)),
    ("converts high-bit pak seed as unsigned decimal", Sync(ConvertsUnsignedPakSeed)),
    ("fills empty translations and preserves manual rows", ConvertsLocalizationCsv),
    ("rejects blank or replacement-character provider output", Sync(RejectsInvalidProviderOutput)),
    ("zhconvert posts Taiwan converter and parses result", CallsZhConvertApi),
    ("maps only the source culture segment", Sync(MapsCulturePath)),
    ("rejects unsafe Unreal virtual paths", Sync(RejectsUnsafeVirtualPath)),
    ("verified install rejects a tampered patch", Sync(RejectsTamperedPatch)),
    ("verified install backs up an existing patch", Sync(InstallsWithBackup)),
    ("verified install requires and copies both IoStore companions", Sync(InstallsIoStoreTriplet)),
    ("install rollback never deletes an untouched matching destination", Sync(PreservesUntouchedDestinationOnRollback)),
    ("repairs UEExtractor multiline locres keys", Sync(RepairsMultilineKeys)),
    ("requires exact packed virtual paths", Sync(VerifiesPackedPaths)),
    ("keeps target CSV basename while isolating its directory", Sync(UsesLocresCompatibleCsvName)),
    ("derives the Unreal project root from Content Paks", Sync(DerivesUnrealProjectRoot)),
    ("accepts generic targeted UEExtractor output names", Sync(FindsGenericExtraction)),
    ("prefers the requested base archive extraction", Sync(PrefersArchiveExtraction)),
    ("rejects generic extraction when another archive output competes", Sync(RejectsAmbiguousGenericExtraction)),
    ("does not pair an exact CSV with a generic hash sidecar", Sync(DoesNotMixExtractionBasenames)),
    ("places locres hash sidecar beside translated CSV", Sync(PlacesTranslatedHashSidecar)),
    ("allows a localization extraction without hash sidecar", Sync(AllowsMissingHashSidecar)),
    ("requires matching ownership evidence for a legacy patch name", Sync(RequiresLegacyOwnershipEvidence)),
    ("finds an existing Unreal user culture config", Sync(FindsUserCultureConfig)),
    ("requires an existing culture config for native mode", Sync(RequiresConfigForNativeMode)),
    ("updates only the Internationalization culture keys", Sync(UpdatesCultureIni)),
    ("updates every duplicate culture key", Sync(UpdatesDuplicateCultureKeys)),
    ("stages a culture change and rolls it back", Sync(RollsBackCultureChange)),
    ("restore resets culture while disabling the owned patch", Sync(ResetsCultureWhenDisablingPatch)),
    ("disable migrates legacy state outside the game folder", Sync(DisableMigratesLegacyStateOutsideGame)),
    ("legacy migration refuses an occupied destination without deleting either copy", Sync(MigrationKeepsBothOnDestinationConflict)),
    ("legacy migration preserves a writer that remains open during the move", Sync(MigrationPreservesOpenWriter)),
    ("patch disabling remains available when legacy migration conflicts", Sync(DisableWorksDespiteMigrationConflict)),
    ("delete writes state outside a fresh game folder", Sync(DeleteKeepsFreshGameFolderClean)),
    ("portable storage refuses an AppData root inside the game", Sync(RejectsInGameStorageRoot)),
    ("portable storage refuses a linked AppData root inside the game", Sync(RejectsLinkedInGameStorageRoot)),
    ("portable storage refuses a linked per-game destination", Sync(RejectsLinkedPerGameDestination)),
    ("session logging does not occupy the migration destination", Sync(SessionLogsDoNotBlockMigration)),
    ("session logging refuses a linked path inside the game", Sync(SessionLogsRejectLinkedGamePath)),
    ("portable tools are shared across games and migrate an existing cache", Sync(SharesAndMigratesToolCache)),
    ("portable tools refuse a linked shared destination", Sync(RejectsLinkedSharedToolDestination)),
    ("portable tools refuse a linked old per-game cache", Sync(RejectsLinkedOldToolCache)),
    ("a locked old tool cache does not prevent creating shared tools", Sync(LockedOldToolsDoNotBlockSharedCache)),
    ("shared tool preparation refuses an active cleanup for the same game", Sync(SharedToolsRespectGameOperationLease)),
    ("cache clearing removes only the selected game's disposable data", Sync(ClearsOnlySelectedGameCache)),
    ("cache clearing creates no storage for a game without cache", Sync(ClearingAbsentCacheIsNoOp)),
    ("cache clearing refuses an unexpected file at the game-store path", Sync(ClearingRefusesUnexpectedStoreFile)),
    ("cache clearing refuses to run during another game operation", Sync(ClearingRefusesActiveGameOperation)),
    ("cache clearing preserves another live session log", Sync(ClearingPreservesOtherLiveLog)),
    ("session log paths are unique for concurrent instances", Sync(SessionLogPathsAreUnique)),
    ("portable storage refuses a destination beneath a drive-root game", Sync(RejectsDriveRootStorage)),
    ("persists successful translation batches", PersistsTranslationMemory),
    ("reads wrapped and flat translation-memory snapshots", Sync(ReadsTranslationMemorySnapshots)),
    ("rejects invalid translation-memory snapshots", Sync(RejectsInvalidTranslationMemorySnapshots)),
    ("does not cache failed translation batches", DoesNotCacheFailedBatch),
    ("preserves v3 locres tables while replacing localized strings", Sync(PreservesV3LocresTables)),
    ("auto-detects the v1 locres string-array layout", Sync(PreservesV1LocresLayout)),
    ("preserves v2 locres reference counts", Sync(PreservesV2Refcounts)),
    ("rejects trailing bytes after a locres string array", Sync(RejectsTrailingLocresBytes)),
    ("artifact records describe IoStore companions", Sync(ArtifactRecordsDescribeCompanions)),
    ("required IoStore records reject a missing companion pair", Sync(RequiredIoStoreRecordRejectsNoCompanions)),
    ("maps an Unreal executable version to retoc", Sync(MapsRetocVersion)),
    ("reads the Unreal version selected by the archive probe", Sync(ReadsProbeEngineVersion)),
    ("builds locres translations from CSV and memory", Sync(BuildsLocresTranslations)),
    ("exact overrides beat converted translations", Sync(AppliesExactOverrides)),
    ("locres surgery renames the language label", Sync(RenamesLanguageLabel)),
    ("probe version resolve returns null for the LATEST placeholder", Sync(ProbeVersionResolveReturnsNullForLatest)),
    ("finds exactly one shipping executable for version detection", Sync(FindsShippingExecutable)),
    ("portable UI state reflects an existing translation patch", Sync(BuildsPortableUiState)),
    ("delete removes owned patch files but never foreign or original files", Sync(DeletesOwnedPatchFiles)),
    ("portable UI enables delete when only residual artifacts remain", Sync(EnablesDeleteForResiduals)),
    ("portable UI toggle offers enable when only a disabled patch exists", Sync(OffersEnableToggleForDisabledPatch)),
    ("enable renames the disabled owned triplet back without overwriting", Sync(EnablesDisabledOwnedPatch)),
    ("residual cleanup keeps the active triplet", Sync(DeletesOnlyResiduals)),
    ("enable picks the newest complete generation per stem", Sync(EnablePicksNewestCompleteGeneration)),
    ("enable never mixes files from different generations", Sync(EnableNeverMixesGenerations)),
    ("enable rolls back the whole group when one rename fails", Sync(EnableRollsBackOnFailure)),
    ("UEExtractor working directory is the bundled tools directory", Sync(UsesToolsDirectoryForUeExtractor)),
    ("parses zh-CN locres candidates with their culture", Sync(ParsesZhCnCandidates)),
    ("picks the dominant simplified culture across candidates", Sync(PicksDominantCulture)),
    ("derives culture-specific patch names", Sync(DerivesCulturePatchNames)),
    ("reads the installed target culture from install state", Sync(ReadsInstalledTargetCulture)),
    ("reads the installed culture from per-game AppData state", Sync(ReadsCultureFromAppData)),
    ("conflicting legacy and AppData state refuses a culture guess", Sync(ConflictingCultureStateRefusesGuess)),
    ("builds the UEExtractor version argument from executable parts", Sync(BuildsUeExtractorVersionArgument)),
    ("updates an existing Culture key without inventing one", Sync(UpdatesExistingCultureKey)),
    ("retries a failed tool run exactly once", RetriesToolRunOnce),
    ("retries a zero-exit focused extraction when no CSV was produced", RetriesMissingFocusedCsv),
    ("retries a zero-exit focused extraction when the CSV is empty", RetriesEmptyFocusedCsv),
    ("focused extraction never reuses a CSV from a failed attempt", DoesNotReuseFailedFocusedCsv),
    ("focused extraction refuses a nonempty output directory before running", RejectsPreexistingFocusedCsv),
    ("focused extraction fails after two zero-exit runs without a CSV", MissingFocusedCsvFailsClosed),
    ("native mode targets zh-Hant while compat keeps the source culture", Sync(MapsModeToTargetCulture)),
    ("restore culture prefers the recorded source culture", Sync(RestoreCulturePrefersSource)),
    ("pinned native runtime accepts a verified existing DLL without network", AcceptsVerifiedNativeRuntime),
    ("pinned native runtime rejects a modified existing DLL", RejectsModifiedNativeRuntime),
    ("pinned native runtime replaces only a known legacy DLL after verifying the download", ReplacesKnownLegacyNativeRuntime),
    ("pinned native runtime keeps a known legacy DLL when download verification fails", KeepsKnownLegacyOnBadDownload),
    ("pinned native runtime verifies archive and extracted DLL before installation", VerifiesNativeRuntimeDownload),
    ("repak runtime rejects an unverified existing Oodle DLL", Sync(RejectsUnverifiedRepakRuntime)),
    ("Unreal workflow rejects an unverified runtime before starting tools", WorkflowRejectsUnverifiedRuntime),
    ("Unreal workflow refuses to start while cache clearing holds the game lease", WorkflowRespectsGameOperationLease),
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception error)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {test.Name}: {error.Message}");
    }
}

Console.WriteLine($"{tests.Length - failures}/{tests.Length} passed");
return failures == 0 ? 0 : 1;

static Func<Task> Sync(Action action) => () => { action(); return Task.CompletedTask; };

static void DetectsNestedUnreal()
{
    using var fixture = new TemporaryDirectory();
    var paks = Path.Combine(fixture.Path, "Deep", "Example", "Content", "Paks");
    Directory.CreateDirectory(paks);
    File.WriteAllText(Path.Combine(paks, "pakchunk0-Windows.pak"), "");
    File.WriteAllText(Path.Combine(paks, "pakchunk0-Windows.utoc"), "");
    File.WriteAllText(Path.Combine(paks, "pakchunk0-Windows.ucas"), "");

    var result = EngineDetector.Detect(fixture.Path);
    Equal(EngineKind.Unreal, result.Engine);
    Equal(PackagingKind.IoStore, result.Packaging);
    Equal("Example", result.ProjectName);
    Equal(Path.GetFullPath(paks), result.PaksDirectory);
    Equal(3, result.Archives.Count);
}

static void DetectsUnity()
{
    using var fixture = new TemporaryDirectory();
    File.WriteAllText(Path.Combine(fixture.Path, "UnityPlayer.dll"), "");
    Directory.CreateDirectory(Path.Combine(fixture.Path, "Sample_Data"));
    File.WriteAllText(Path.Combine(fixture.Path, "Sample_Data", "globalgamemanagers"), "");

    var result = EngineDetector.Detect(fixture.Path);
    Equal(EngineKind.Unity, result.Engine);
    Equal(PackagingKind.UnityAssets, result.Packaging);
    Equal("Sample", result.ProjectName);
}

static void ChoosesMainPaksDirectory()
{
    using var fixture = new TemporaryDirectory();
    var auxiliary = Path.Combine(fixture.Path, "Auxiliary", "Content", "Paks");
    Directory.CreateDirectory(auxiliary);
    File.WriteAllText(Path.Combine(auxiliary, "optional.pak"), "");
    var main = Path.Combine(fixture.Path, "ActualGame", "Content", "Paks");
    Directory.CreateDirectory(main);
    File.WriteAllText(Path.Combine(main, "pakchunk0.pak"), "");
    File.WriteAllText(Path.Combine(main, "pakchunk0.utoc"), "");
    File.WriteAllText(Path.Combine(main, "pakchunk0.ucas"), "");

    var result = EngineDetector.Detect(fixture.Path);
    Equal(Path.GetFullPath(main), result.PaksDirectory);
    Equal("ActualGame", result.ProjectName);
    Equal(3, result.Archives.Count);
}

static void ParsesLocresCandidates()
{
    const string log = """
      Reading: Example/Content/Localization/Game/zh-Hans/Game.locres (from OtherMod_P.pak)
      Reading: Example/Content/Localization/Game/zh-Hans/Game.locres (from pakchunk0-Windows.pak)
      Reading: Engine/Content/Localization/Engine/en/Engine.locres (from pakchunk0-Windows.pak)
      Reading: Example/Content/Localization/Game/zh-Hant/Game.locres (from pakchunk99-GameTranslate_zhHant_P.pak)
      Reading: Example/Plugins/Foo/Content/Localization/Foo/zh-Hans/Foo.locres (from pakchunk1-Windows.pak)
      """;

    var candidates = UnrealProbeParser.ParseSimplifiedChineseCandidates(log);
    Equal(2, candidates.Count);
    Equal("Example/Content/Localization/Game/zh-Hans/Game.locres", candidates[0].VirtualPath);
    Equal("pakchunk0-Windows.pak", candidates[0].SourceArchive);
    Equal("Game", candidates[0].Name);
}

static void ProtectsTokens()
{
    const string input = "按下 <b>{Button}</b> 造成 %d 點傷害\\n[InputAction Jump]";
    var layout = ProtectedText.CreateLayout(input);
    var result = layout.Reassemble(layout.VisibleSegments.Select(value => $"繁:{value}").ToArray());

    SequenceEqual(new[] { "<b>", "{Button}", "</b>", "%d", "\\n", "[InputAction Jump]" }, ProtectedText.Tokens(result));
    True(result.Contains("繁:按下 "));
}

static void RoundTripsCsv()
{
    const string csv = "key,source,Translation\r\nA,\"第一行,文字\",\"繁體\"\r\nB,\"第二行\n續行\",\r\n";
    var document = LocalizationCsv.Parse(csv);
    Equal(3, document.Rows.Count);
    Equal("第一行,文字", document.Rows[1][1]);
    Equal("第二行\n續行", document.Rows[2][1]);
    var reparsed = LocalizationCsv.Parse(document.Serialize());
    Equal(document.Rows.Count, reparsed.Rows.Count);
    for (var index = 0; index < document.Rows.Count; index++)
        SequenceEqual(document.Rows[index], reparsed.Rows[index]);
}

static void ClassifiesPatches()
{
    using var fixture = new TemporaryDirectory();
    File.WriteAllText(Path.Combine(fixture.Path, "pakchunk99-GameTranslate_zhHant_P.pak"), "owned");
    File.WriteAllText(Path.Combine(fixture.Path, "pakchunk99-ZhHant_P.pak"), "legacy");
    File.WriteAllText(Path.Combine(fixture.Path, "SomeOtherMod_P.pak"), "foreign");

    var state = PatchManager.Inspect(fixture.Path);
    Equal(1, state.OwnedActive.Count);
    Equal(2, state.ForeignActive.Count);
    True(state.CanRestore);
}

static void RestoresPatch()
{
    using var fixture = new TemporaryDirectory();
    var patch = Path.Combine(fixture.Path, "pakchunk99-GameTranslate_zhHans_P.pak");
    File.WriteAllText(patch, "owned");
    File.WriteAllText(Path.ChangeExtension(patch, ".utoc"), "utoc");
    File.WriteAllText(Path.ChangeExtension(patch, ".ucas"), "ucas");

    var disabled = PatchManager.DisableOwned(fixture.Path);
    Equal(3, disabled.Count);
    False(File.Exists(patch));
    True(File.Exists(patch + ".disabled"));
    Equal("owned", File.ReadAllText(patch + ".disabled"));
    True(File.Exists(Path.ChangeExtension(patch, ".utoc") + ".disabled"));
    True(File.Exists(Path.ChangeExtension(patch, ".ucas") + ".disabled"));
}

static void RollsBackPartialPatchDisable()
{
    using var fixture = new TemporaryDirectory();
    var first = Path.Combine(fixture.Path, "pakchunk98-GameTranslate_A_P.pak");
    var second = Path.Combine(fixture.Path, "pakchunk99-GameTranslate_B_P.pak");
    File.WriteAllText(first, "first");
    File.WriteAllText(second, "second");
    using var locked = new FileStream(second, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

    Throws<IOException>(() => PatchManager.DisableOwned(fixture.Path));
    True(File.Exists(first));
    True(File.Exists(second));
    Equal(0, Directory.EnumerateFiles(fixture.Path, "*.disabled*", SearchOption.TopDirectoryOnly).Count());
}

static void ParsesPakInfo()
{
    const string output = "mount point: ../../../\r\nversion: V11\r\ncompression: Oodle\r\npath hash seed: Some(D8195615)\r\n4668 file entries\r\n";
    var info = RepakParser.ParseInfo(output);
    Equal("../../../", info.MountPoint);
    Equal("V11", info.Version);
    Equal("D8195615", info.PathHashSeed);
    Equal(4668, info.FileCount);
}

static void ConvertsUnsignedPakSeed()
{
    Equal("3625539093", RepakParser.SeedDecimal("D8195615"));
    Equal("18446744073709551615", RepakParser.SeedDecimal("FFFFFFFFFFFFFFFF"));
}

static async Task ConvertsLocalizationCsv()
{
    const string csv = "key,source,Translation\nA,伤害 {Count},\nB,名称,手動名稱\nC,ASCII only,\n";
    var api = new RecordingTranslationApi(values => values.Select(value => value.Replace("伤害", "傷害")).ToArray());
    var result = await LocalizationTranslator.ConvertAsync(csv, api, batchSize: 100, CancellationToken.None);
    var document = LocalizationCsv.Parse(result.Text);

    Equal("傷害 {Count}", document.Rows[1][2]);
    Equal("手動名稱", document.Rows[2][2]);
    Equal("ASCII only", document.Rows[3][2]);
    Equal(1, api.Calls.Count);
    Equal(1, result.Converted);
    Equal(1, result.Preserved);
}

static void RejectsInvalidProviderOutput()
{
    const string csv = "key,source,Translation\nA,伤害,\n";
    foreach (var invalid in new[] { string.Empty, "傷�" })
    {
        var api = new RecordingTranslationApi(values => values.Select(_ => invalid).ToArray());
        Throws<InvalidDataException>(() => LocalizationTranslator.ConvertAsync(csv, api, 100, CancellationToken.None).GetAwaiter().GetResult());
    }
}

static async Task CallsZhConvertApi()
{
    var handler = new StubHttpHandler(request =>
    {
        var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
        True(body.Contains("converter=Taiwan"));
        True(body.Contains("text="));
        True(Uri.UnescapeDataString(body.Replace('+', ' ')).Contains("伤害"));
        False(body.Contains("%5Cu", StringComparison.OrdinalIgnoreCase));
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"code\":0,\"data\":{\"text\":\"[\\\"傷害\\\"]\"}}"),
        };
    });
    var client = new ZhConvertClient(new HttpClient(handler));
    var converted = await client.ConvertBatchAsync(["伤害"], CancellationToken.None);
    SequenceEqual(new[] { "傷害" }, converted);
}

static void MapsCulturePath()
{
    Equal(
        "Example/Content/Localization/Game/zh-Hant/Game.locres",
        UnrealPaths.ChangeCulture("Example/Content/Localization/Game/zh-Hans/Game.locres", "zh-Hans", "zh-Hant"));
}

static void RejectsUnsafeVirtualPath()
{
    using var fixture = new TemporaryDirectory();
    Throws<InvalidDataException>(() => UnrealPaths.StagePath(fixture.Path, "../outside.locres"));
    Throws<InvalidDataException>(() => UnrealPaths.ChangeCulture("Example/zh-Hans/Other/zh-Hans/File.locres", "zh-Hans", "zh-Hant"));
}

static void RejectsTamperedPatch()
{
    using var fixture = new TemporaryDirectory();
    var source = Path.Combine(fixture.Path, "artifact.pak");
    var paks = Path.Combine(fixture.Path, "Paks");
    Directory.CreateDirectory(paks);
    File.WriteAllText(source, "tampered");
    var record = new ArtifactRecord("pakchunk99-GameTranslate_zhHans_P.pak", "BADHASH", "compat", "zh-Hans", [], new("../../../", "V11", "Zlib", "0", 0));

    Throws<InvalidDataException>(() => PatchManager.InstallVerified(source, record, paks));
}

static void InstallsWithBackup()
{
    using var fixture = new TemporaryDirectory();
    var source = Path.Combine(fixture.Path, "artifact.pak");
    var paks = Path.Combine(fixture.Path, "Paks");
    Directory.CreateDirectory(paks);
    File.WriteAllText(source, "new patch");
    var name = "pakchunk99-GameTranslate_zhHans_P.pak";
    File.WriteAllText(Path.Combine(paks, name), "old patch");
    var hash = FileHashes.Sha256(source);
    var record = new ArtifactRecord(name, hash, "compat", "zh-Hans", ["Example/zh-Hans/Game.locres"], new("../../../", "V11", "Zlib", "0", 1));

    var result = PatchManager.InstallVerified(source, record, paks);
    Equal("new patch", File.ReadAllText(result.Destination));
    True(result.BackupPath is not null && File.Exists(result.BackupPath));
    Equal("old patch", File.ReadAllText(result.BackupPath!));
    Equal(hash, FileHashes.Sha256(result.Destination));
}

static void InstallsIoStoreTriplet()
{
    using var fixture = new TemporaryDirectory();
    var name = "pakchunk99-GameTranslate_zhHans_P.pak";
    var source = Path.Combine(fixture.Path, name);
    var utoc = Path.ChangeExtension(source, ".utoc");
    var ucas = Path.ChangeExtension(source, ".ucas");
    var paks = Path.Combine(fixture.Path, "Paks");
    Directory.CreateDirectory(paks);
    File.WriteAllText(source, "patch");
    File.WriteAllText(utoc, "utoc");
    File.WriteAllText(ucas, "ucas");
    var companions = new[] {
        new CompanionArtifact(Path.GetFileName(utoc), new FileInfo(utoc).Length, FileHashes.Sha256(utoc)),
        new CompanionArtifact(Path.GetFileName(ucas), new FileInfo(ucas).Length, FileHashes.Sha256(ucas)),
    };
    var record = new ArtifactRecord(name, FileHashes.Sha256(source), "compat", "zh-Hans", [], companions,
        new("../../../", "V11", "Zlib", "0", 0));

    PatchManager.InstallVerified(source, record, paks);
    Equal("utoc", File.ReadAllText(Path.Combine(paks, Path.GetFileName(utoc))));
    Equal("ucas", File.ReadAllText(Path.Combine(paks, Path.GetFileName(ucas))));
    File.Delete(utoc);
    Throws<FileNotFoundException>(() => PatchManager.InstallVerified(source, record, paks));
}

static void PreservesUntouchedDestinationOnRollback()
{
    using var fixture = new TemporaryDirectory();
    var name = "pakchunk99-GameTranslate_zhHans_P.pak";
    var source = Path.Combine(fixture.Path, name);
    var utoc = Path.ChangeExtension(source, ".utoc");
    var ucas = Path.ChangeExtension(source, ".ucas");
    var paks = Path.Combine(fixture.Path, "Paks");
    Directory.CreateDirectory(paks);
    File.WriteAllText(source, "same patch");
    File.WriteAllText(utoc, "utoc");
    File.WriteAllText(ucas, "ucas");
    File.WriteAllText(Path.Combine(paks, name), "same patch");
    Directory.CreateDirectory(Path.Combine(paks, Path.GetFileName(ucas)));
    var companions = new[] {
        new CompanionArtifact(Path.GetFileName(utoc), new FileInfo(utoc).Length, FileHashes.Sha256(utoc)),
        new CompanionArtifact(Path.GetFileName(ucas), new FileInfo(ucas).Length, FileHashes.Sha256(ucas)),
    };
    var record = new ArtifactRecord(name, FileHashes.Sha256(source), "compat", "zh-Hans", [], companions,
        new("../../../", "V11", "Zlib", "0", 0));
    Throws<IOException>(() => PatchManager.InstallVerified(source, record, paks));
    True(File.Exists(Path.Combine(paks, name)));
    Equal("same patch", File.ReadAllText(Path.Combine(paks, name)));
}

static void RepairsMultilineKeys()
{
    const string csv = "key,source,Translation\nFIRST,,,\nSECOND,文字,\n";
    var result = UeExtractorCsv.Normalize(csv, new HashSet<string> { "FIRST\nSECOND" });
    Equal(1, result.Repairs);
    var document = LocalizationCsv.Parse(result.Text);
    Equal(2, document.Rows.Count);
    Equal("FIRST\nSECOND", document.Rows[1][0]);
}

static void VerifiesPackedPaths()
{
    var expected = new[] { "Example/Content/A.locres", "Example/Content/B.locres" };
    UnrealVerification.ExactPaths("Example/Content/B.locres\nExample/Content/A.locres\n", expected);
    Throws<InvalidDataException>(() => UnrealVerification.ExactPaths("Example/Content/A.locres\n", expected));
}

static void UsesLocresCompatibleCsvName()
{
    var path = UnrealWorkPaths.TranslatedCsv("C:\\work\\translated", 3, "Game");
    Equal("Game.csv", Path.GetFileName(path));
    Equal("003-Game", Path.GetFileName(Path.GetDirectoryName(path)));
}

static void DerivesUnrealProjectRoot()
{
    var gameRoot = Path.GetFullPath("C:\\Games\\Example");
    var paks = Path.Combine(gameRoot, "ActualProject", "Content", "Paks");
    Equal(Path.Combine(gameRoot, "ActualProject"), UnrealPaths.ProjectRootFromPaks(gameRoot, paks));
    Throws<InvalidDataException>(() => UnrealPaths.ProjectRootFromPaks(gameRoot, Path.Combine(gameRoot, "Other", "Paks")));
    Throws<InvalidDataException>(() => UnrealPaths.ProjectRootFromPaks(gameRoot, "C:\\Outside\\Project\\Content\\Paks"));
}

static void FindsGenericExtraction()
{
    using var fixture = new TemporaryDirectory();
    var generic = Path.Combine(fixture.Path, "Game.csv");
    File.WriteAllText(generic, "data");
    File.WriteAllText(Path.Combine(fixture.Path, "Game._skipped_lines.csv"), "skip");
    var extraction = UeExtractionFiles.FindLocalization(fixture.Path, "Game", "pakchunk0-Windows");
    Equal(generic, extraction.Csv);
    Equal<string?>(null, extraction.Hashes);
}

static void PrefersArchiveExtraction()
{
    using var fixture = new TemporaryDirectory();
    File.WriteAllText(Path.Combine(fixture.Path, "Game.csv"), "generic");
    var exact = Path.Combine(fixture.Path, "Game_pakchunk0-Windows.csv");
    File.WriteAllText(exact, "base");
    Equal(exact, UeExtractionFiles.FindLocalization(fixture.Path, "Game", "pakchunk0-Windows").Csv);
}

static void RejectsAmbiguousGenericExtraction()
{
    using var fixture = new TemporaryDirectory();
    File.WriteAllText(Path.Combine(fixture.Path, "Game.csv"), "generic");
    File.WriteAllText(Path.Combine(fixture.Path, "Game_other-patch.csv"), "other");
    Throws<InvalidDataException>(() => UeExtractionFiles.FindLocalization(fixture.Path, "Game", "pakchunk0-Windows"));
}

static void DoesNotMixExtractionBasenames()
{
    using var fixture = new TemporaryDirectory();
    var exact = Path.Combine(fixture.Path, "Game_pakchunk0-Windows.csv");
    File.WriteAllText(exact, "base");
    File.WriteAllText(Path.Combine(fixture.Path, "Game.locreshashes"), "{}");
    var extraction = UeExtractionFiles.FindLocalization(fixture.Path, "Game", "pakchunk0-Windows");
    Equal(exact, extraction.Csv);
    Equal<string?>(null, extraction.Hashes);
}

static void PlacesTranslatedHashSidecar()
{
    var csv = UnrealWorkPaths.TranslatedCsv("C:\\work\\translated", 3, "Game");
    var hashes = UnrealWorkPaths.TranslatedHashes("C:\\work\\translated", 3, "Game");
    Equal(Path.GetDirectoryName(csv), Path.GetDirectoryName(hashes));
    Equal("Game.locreshashes", Path.GetFileName(hashes));
}

static void AllowsMissingHashSidecar()
{
    using var fixture = new TemporaryDirectory();
    File.WriteAllText(Path.Combine(fixture.Path, "OnlineSubsystem.csv"), "data");
    Equal<string?>(null, UeExtractionFiles.FindLocalization(fixture.Path, "OnlineSubsystem", "pakchunk0-Windows").Hashes);
}

static void RequiresLegacyOwnershipEvidence()
{
    using var fixture = new TemporaryDirectory();
    var legacy = Path.Combine(fixture.Path, "pakchunk99-ZhHant_P.pak");
    File.WriteAllText(legacy, "verified legacy patch");
    var before = PatchManager.Inspect(fixture.Path);
    Equal(0, before.OwnedActive.Count);
    Equal(1, before.ForeignActive.Count);

    var hash = FileHashes.Sha256(legacy);
    PatchManager.RecordLegacyOwnership(legacy, hash);
    Equal(1, PatchManager.Inspect(fixture.Path).OwnedActive.Count);

    File.AppendAllText(legacy, "tampered");
    var after = PatchManager.Inspect(fixture.Path);
    Equal(0, after.OwnedActive.Count);
    Equal(1, after.ForeignActive.Count);
}

static void FindsUserCultureConfig()
{
    using var fixture = new TemporaryDirectory();
    var config = Path.Combine(fixture.Path, "TheMound", "Saved", "Config", "Windows", "GameUserSettings.ini");
    Directory.CreateDirectory(Path.GetDirectoryName(config)!);
    File.WriteAllText(config, "[Internationalization]\nLanguage=zh-Hans\nLocale=zh-Hans\n");
    var game = new GameDetection("C:\\Games\\The Mound", EngineKind.Unreal, PackagingKind.IoStore,
        "TheMound", "C:\\Games\\The Mound\\TheMound\\Content\\Paks", []);

    Equal(config, CultureConfig.FindExisting(game, fixture.Path));
}

static void RequiresConfigForNativeMode()
{
    using var fixture = new TemporaryDirectory();
    var game = new GameDetection("C:\\Games\\Example", EngineKind.Unreal, PackagingKind.Pak,
        "Example", "C:\\Games\\Example\\Content\\Paks", []);
    Equal<string?>(null, CultureConfig.ForMode(game, fixture.Path, TranslationMode.Compatibility));
    Throws<NotSupportedException>(() => CultureConfig.ForMode(game, fixture.Path, TranslationMode.NativeTraditional));
}

static void UpdatesCultureIni()
{
    const string original = ";METADATA=(Diff=true)\r\n[Internationalization]\r\nLanguage=zh-Hans\r\nLocale=zh-Hans\r\n\r\n[Other]\r\nValue=Keep\r\n";
    var updated = CultureConfig.UpdateText(original, "zh-Hant");
    True(updated.Contains("Language=zh-Hant\r\nLocale=zh-Hant"));
    True(updated.Contains("[Other]\r\nValue=Keep"));
    False(updated.Contains("Language=zh-Hans"));
}

static void UpdatesDuplicateCultureKeys()
{
    const string original = "[Internationalization]\nLanguage=zh-Hans\nLanguage=en\nLocale=zh-Hans\nLocale=en\n[Other]\nValue=Keep\n";
    var updated = CultureConfig.UpdateText(original, "zh-Hant");
    Equal(2, updated.Split("Language=zh-Hant", StringSplitOptions.None).Length - 1);
    Equal(2, updated.Split("Locale=zh-Hant", StringSplitOptions.None).Length - 1);
    False(updated.Contains("Language=en"));
    False(updated.Contains("Locale=en"));
}

static void RollsBackCultureChange()
{
    using var fixture = new TemporaryDirectory();
    var config = Path.Combine(fixture.Path, "GameUserSettings.ini");
    const string original = "[Internationalization]\nLanguage=zh-Hans\nLocale=zh-Hans\n";
    File.WriteAllText(config, original);

    using var change = CultureConfig.Prepare(config, "zh-Hant", "test");
    Equal(original, File.ReadAllText(config));
    change.Commit();
    True(File.ReadAllText(config).Contains("Language=zh-Hant"));
    True(change.BackupPath is not null && File.Exists(change.BackupPath));
    change.Rollback();
    Equal(original, File.ReadAllText(config));
}

static void ResetsCultureWhenDisablingPatch()
{
    using var fixture = new TemporaryDirectory();
    var gameRoot = Path.Combine(fixture.Path, "InstalledGame");
    var paks = Path.Combine(gameRoot, "Game", "Content", "Paks");
    Directory.CreateDirectory(paks);
    var patch = Path.Combine(paks, "pakchunk99-GameTranslate_zhHant_P.pak");
    File.WriteAllText(patch, "owned");
    var config = Path.Combine(fixture.Path, "Local", "Game", "Saved", "Config", "Windows", "GameUserSettings.ini");
    Directory.CreateDirectory(Path.GetDirectoryName(config)!);
    File.WriteAllText(config, "[Internationalization]\nLanguage=zh-Hant\nLocale=zh-Hant\n");
    var game = new GameDetection(gameRoot, EngineKind.Unreal, PackagingKind.Pak, "Game", paks, []);
    var stateDirectory = Path.Combine(gameRoot, ".game-translate");
    Directory.CreateDirectory(stateDirectory);
    File.WriteAllText(Path.Combine(stateDirectory, "install-state.json"), "{\"version\":3,\"active\":true,\"artifact\":{\"Companions\":[{\"Name\":\"patch.utoc\"}]}}");

    var result = PortableModeManager.DisableAndReset(game, Path.Combine(fixture.Path, "Local"));
    Equal(1, result.DisabledPatches.Count);
    False(File.Exists(patch));
    True(File.Exists(patch + ".disabled"));
    True(File.ReadAllText(config).Contains("Language=zh-Hans"));
    False(Directory.Exists(stateDirectory));
    var migratedState = Path.Combine(PortableStorage.GameDirectory(gameRoot, Path.Combine(fixture.Path, "Local")), "install-state.json");
    using var state = System.Text.Json.JsonDocument.Parse(File.ReadAllText(migratedState));
    False(state.RootElement.GetProperty("active").GetBoolean());
    Equal(1, state.RootElement.GetProperty("artifact").GetProperty("Companions").GetArrayLength());
}

static void DisableMigratesLegacyStateOutsideGame()
{
    using var fixture = new TemporaryDirectory();
    var gameRoot = Path.Combine(fixture.Path, "Game");
    var paks = Path.Combine(gameRoot, "Example", "Content", "Paks");
    Directory.CreateDirectory(paks);
    var patch = Path.Combine(paks, "pakchunk99-GameTranslate_zhHans_P.pak");
    File.WriteAllText(patch, "owned");
    var legacy = Path.Combine(gameRoot, ".game-translate");
    var memory = Path.Combine(legacy, "translation-memory", "zhconvert-taiwan.json");
    Directory.CreateDirectory(Path.GetDirectoryName(memory)!);
    File.WriteAllText(memory, "{\"source\":\"translated\"}");
    File.WriteAllText(Path.Combine(legacy, "install-state.json"),
        "{\"version\":3,\"active\":true,\"artifact\":{\"SourceCulture\":\"zh-Hans\"}}");
    var local = Path.Combine(fixture.Path, "Local");
    var game = new GameDetection(gameRoot, EngineKind.Unreal, PackagingKind.Pak, "Example", paks, []);

    PortableModeManager.DisableAndReset(game, local);

    False(Directory.Exists(legacy));
    var states = Directory.GetFiles(local, "install-state.json", SearchOption.AllDirectories);
    Equal(1, states.Length);
    True(File.ReadAllText(states[0]).Contains("\"active\": false", StringComparison.Ordinal));
    True(File.Exists(Path.Combine(Path.GetDirectoryName(states[0])!, "translation-memory", "zhconvert-taiwan.json")));
    True(File.Exists(patch + ".disabled"));
}

static void MigrationKeepsBothOnDestinationConflict()
{
    using var fixture = new TemporaryDirectory();
    var gameRoot = Path.Combine(fixture.Path, "Game");
    var legacy = Path.Combine(gameRoot, ".game-translate");
    Directory.CreateDirectory(legacy);
    File.WriteAllText(Path.Combine(legacy, "new-log.txt"), "late write");
    var destination = PortableStorage.GameDirectory(gameRoot, Path.Combine(fixture.Path, "Local"));
    Directory.CreateDirectory(destination);
    File.WriteAllText(Path.Combine(destination, "new-log.txt"), "late write");

    Throws<InvalidDataException>(() => PortableStorage.PrepareGameDirectory(gameRoot, Path.Combine(fixture.Path, "Local")));
    Equal("late write", File.ReadAllText(Path.Combine(legacy, "new-log.txt")));
    Equal("late write", File.ReadAllText(Path.Combine(destination, "new-log.txt")));
}

static void MigrationPreservesOpenWriter()
{
    using var fixture = new TemporaryDirectory();
    var gameRoot = Path.Combine(fixture.Path, "Game");
    var legacy = Path.Combine(gameRoot, ".game-translate");
    Directory.CreateDirectory(legacy);
    var source = Path.Combine(legacy, "active.log");
    File.WriteAllText(source, "before");
    using var writer = new FileStream(source, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
    writer.Seek(0, SeekOrigin.End);
    var local = Path.Combine(fixture.Path, "Local");
    try
    {
        var destination = PortableStorage.PrepareGameDirectory(gameRoot, local);
        writer.Write("after"u8);
        writer.Flush(flushToDisk: true);
        writer.Dispose();
        Equal("beforeafter", File.ReadAllText(Path.Combine(destination, "active.log")));
        False(Directory.Exists(legacy));
    }
    catch (IOException)
    {
        // Some Windows file systems refuse a directory rename with an open writer.
        // Failure is safe only if the live original remains untouched.
        True(Directory.Exists(legacy));
        writer.Write("after"u8);
        writer.Flush(flushToDisk: true);
        writer.Dispose();
        Equal("beforeafter", File.ReadAllText(source));
    }
}

static void DisableWorksDespiteMigrationConflict()
{
    using var fixture = new TemporaryDirectory();
    var gameRoot = Path.Combine(fixture.Path, "Game");
    var paks = Path.Combine(gameRoot, "Example", "Content", "Paks");
    Directory.CreateDirectory(paks);
    var patch = Path.Combine(paks, "pakchunk99-GameTranslate_zhHans_P.pak");
    File.WriteAllText(patch, "owned");
    var legacy = Path.Combine(gameRoot, ".game-translate");
    Directory.CreateDirectory(legacy);
    File.WriteAllText(Path.Combine(legacy, "install-state.json"), "old-state");
    var local = Path.Combine(fixture.Path, "Local");
    var destination = PortableStorage.GameDirectory(gameRoot, local);
    Directory.CreateDirectory(destination);
    File.WriteAllText(Path.Combine(destination, "install-state.json"), "other-state");
    var game = new GameDetection(gameRoot, EngineKind.Unreal, PackagingKind.Pak, "Example", paks, []);

    var result = PortableModeManager.DisableAndReset(game, local);

    True(result.StorageWarning is not null);
    True(File.Exists(patch + ".disabled"));
    Equal("old-state", File.ReadAllText(Path.Combine(legacy, "install-state.json")));
    Equal("other-state", File.ReadAllText(Path.Combine(destination, "install-state.json")));
}

static void DeleteKeepsFreshGameFolderClean()
{
    using var fixture = new TemporaryDirectory();
    var gameRoot = Path.Combine(fixture.Path, "Game");
    var paks = Path.Combine(gameRoot, "Example", "Content", "Paks");
    Directory.CreateDirectory(paks);
    var patch = Path.Combine(paks, "pakchunk99-GameTranslate_zhHans_P.pak");
    File.WriteAllText(patch, "owned");
    var local = Path.Combine(fixture.Path, "Local");
    var game = new GameDetection(gameRoot, EngineKind.Unreal, PackagingKind.Pak, "Example", paks, []);

    PortableModeManager.DeleteAndReset(game, local);

    False(Directory.Exists(Path.Combine(gameRoot, ".game-translate")));
    Equal(1, Directory.GetFiles(local, "install-state.json", SearchOption.AllDirectories).Length);
    False(File.Exists(patch));
}

static void RejectsInGameStorageRoot()
{
    using var fixture = new TemporaryDirectory();
    var gameRoot = Path.Combine(fixture.Path, "Game");
    Directory.CreateDirectory(gameRoot);
    var unsafeLocalRoot = Path.Combine(gameRoot, "AppData", "Local");
    Throws<InvalidDataException>(() => PortableStorage.GameDirectory(gameRoot, unsafeLocalRoot));
    False(Directory.Exists(unsafeLocalRoot));
}

static void RejectsLinkedInGameStorageRoot()
{
    using var fixture = new TemporaryDirectory();
    var gameRoot = Path.Combine(fixture.Path, "Game");
    Directory.CreateDirectory(gameRoot);
    var link = Path.Combine(fixture.Path, "AppDataLink");
    try { Directory.CreateSymbolicLink(link, gameRoot); }
    catch (UnauthorizedAccessException) { return; } // Developer Mode or symlink privilege is unavailable.
    catch (IOException error) when ((error.HResult & 0xFFFF) == 1314) { return; }
    Throws<InvalidDataException>(() => PortableStorage.GameDirectory(gameRoot, link));
    False(Directory.Exists(Path.Combine(gameRoot, "GameTranslate")));
}

static void RejectsLinkedPerGameDestination()
{
    using var fixture = new TemporaryDirectory();
    var game = Path.Combine(fixture.Path, "Game");
    var other = Path.Combine(fixture.Path, "Other");
    var local = Path.Combine(fixture.Path, "Local");
    Directory.CreateDirectory(game);
    Directory.CreateDirectory(other);
    var destination = PortableStorage.GameDirectory(game, local);
    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
    try { Directory.CreateSymbolicLink(destination, other); }
    catch (UnauthorizedAccessException) { return; }
    catch (IOException error) when ((error.HResult & 0xFFFF) == 1314) { return; }
    Throws<InvalidDataException>(() => PortableStorage.PrepareGameDirectory(game, local));
    False(Directory.Exists(Path.Combine(other, "work")));
}

static void SessionLogsDoNotBlockMigration()
{
    using var fixture = new TemporaryDirectory();
    var gameRoot = Path.Combine(fixture.Path, "Game");
    Directory.CreateDirectory(gameRoot);
    var local = Path.Combine(fixture.Path, "Local");
    var storage = PortableStorage.GameDirectory(gameRoot, local);
    var logs = PortableStorage.SessionLogDirectory(gameRoot, local);
    False(logs.StartsWith(storage + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    Directory.CreateDirectory(logs);
    False(Directory.Exists(storage));
    False(Directory.Exists(Path.Combine(gameRoot, ".game-translate")));
}

static void SessionLogsRejectLinkedGamePath()
{
    using var fixture = new TemporaryDirectory();
    var gameRoot = Path.Combine(fixture.Path, "Game");
    Directory.CreateDirectory(gameRoot);
    var local = Path.Combine(fixture.Path, "Local");
    var appRoot = Path.Combine(local, "GameTranslate");
    Directory.CreateDirectory(appRoot);
    try { Directory.CreateSymbolicLink(Path.Combine(appRoot, "session-logs"), gameRoot); }
    catch (UnauthorizedAccessException) { return; }
    catch (IOException error) when ((error.HResult & 0xFFFF) == 1314) { return; }
    Throws<InvalidDataException>(() => PortableStorage.SessionLogDirectory(gameRoot, local));
}

static void SharesAndMigratesToolCache()
{
    using var fixture = new TemporaryDirectory();
    var local = Path.Combine(fixture.Path, "Local");
    var firstGame = Path.Combine(fixture.Path, "First Game");
    var secondGame = Path.Combine(fixture.Path, "Second Game");
    Directory.CreateDirectory(firstGame);
    Directory.CreateDirectory(secondGame);
    var version = "1.0.8.4-0.2.3";
    var oldTools = Path.Combine(PortableStorage.GameDirectory(firstGame, local), "tools", version);
    Directory.CreateDirectory(oldTools);
    File.WriteAllText(Path.Combine(oldTools, "test-tool.exe"), "verified fixture");

    var shared = PortableStorage.PrepareSharedToolsDirectory(firstGame, version, local);

    Equal(shared, PortableStorage.PrepareSharedToolsDirectory(secondGame, version, local));
    False(Directory.Exists(oldTools));
    Equal("verified fixture", File.ReadAllText(Path.Combine(shared, "test-tool.exe")));
    False(Directory.Exists(Path.Combine(firstGame, ".game-translate")));
    False(Directory.Exists(Path.Combine(secondGame, ".game-translate")));
}

static void RejectsLinkedSharedToolDestination()
{
    using var fixture = new TemporaryDirectory();
    var game = Path.Combine(fixture.Path, "Game");
    var other = Path.Combine(fixture.Path, "Other");
    var local = Path.Combine(fixture.Path, "Local");
    Directory.CreateDirectory(game);
    Directory.CreateDirectory(other);
    var shared = Path.Combine(local, "GameTranslate", "shared", "tools", "v1");
    Directory.CreateDirectory(Path.GetDirectoryName(shared)!);
    try { Directory.CreateSymbolicLink(shared, other); }
    catch (UnauthorizedAccessException) { return; }
    catch (IOException error) when ((error.HResult & 0xFFFF) == 1314) { return; }
    Throws<InvalidDataException>(() => PortableStorage.PrepareSharedToolsDirectory(game, "v1", local));
    False(File.Exists(Path.Combine(other, "repak.exe")));
}

static void RejectsLinkedOldToolCache()
{
    using var fixture = new TemporaryDirectory();
    var game = Path.Combine(fixture.Path, "Game");
    var other = Path.Combine(fixture.Path, "Other");
    var local = Path.Combine(fixture.Path, "Local");
    Directory.CreateDirectory(game);
    Directory.CreateDirectory(other);
    var oldToolsRoot = Path.Combine(PortableStorage.GameDirectory(game, local), "tools");
    Directory.CreateDirectory(Path.GetDirectoryName(oldToolsRoot)!);
    try { Directory.CreateSymbolicLink(oldToolsRoot, other); }
    catch (UnauthorizedAccessException) { return; }
    catch (IOException error) when ((error.HResult & 0xFFFF) == 1314) { return; }
    Throws<InvalidDataException>(() => PortableStorage.PrepareSharedToolsDirectory(game, "v1", local));
    True(Directory.Exists(oldToolsRoot));
}

static void LockedOldToolsDoNotBlockSharedCache()
{
    using var fixture = new TemporaryDirectory();
    var local = Path.Combine(fixture.Path, "Local");
    var game = Path.Combine(fixture.Path, "Game");
    Directory.CreateDirectory(game);
    var oldTools = Path.Combine(PortableStorage.GameDirectory(game, local), "tools", "v1");
    Directory.CreateDirectory(oldTools);
    var oldFile = Path.Combine(oldTools, "busy.exe");
    File.WriteAllText(oldFile, "old cache");
    using var locked = new FileStream(oldFile, FileMode.Open, FileAccess.Read, FileShare.None);

    var shared = PortableStorage.PrepareSharedToolsDirectory(game, "v1", local);

    True(Directory.Exists(shared));
    True(File.Exists(Path.Combine(shared, "busy.exe")) || File.Exists(oldFile));
}

static void SharedToolsRespectGameOperationLease()
{
    using var fixture = new TemporaryDirectory();
    var local = Path.Combine(fixture.Path, "Local");
    var game = Path.Combine(fixture.Path, "Game");
    Directory.CreateDirectory(game);
    using var clearing = PortableGameOperationLease.Acquire(game, local);

    Throws<IOException>(() => PortableStorage.PrepareSharedToolsDirectory(game, "v1", local));
    False(Directory.Exists(PortableStorage.GameDirectory(game, local)));
}

static void ClearsOnlySelectedGameCache()
{
    using var fixture = new TemporaryDirectory();
    var local = Path.Combine(fixture.Path, "Local");
    var game = Path.Combine(fixture.Path, "Game A");
    var otherGame = Path.Combine(fixture.Path, "Game B");
    var paks = Path.Combine(game, "Example", "Content", "Paks");
    Directory.CreateDirectory(paks);
    Directory.CreateDirectory(otherGame);
    var patch = Path.Combine(paks, "pakchunk99-GameTranslate_zhHans_P.pak");
    File.WriteAllText(patch, "installed patch");
    var store = PortableStorage.GameDirectory(game, local);
    var otherStore = PortableStorage.GameDirectory(otherGame, local);
    var sharedTools = Path.Combine(local, "GameTranslate", "shared", "tools", "1.0.8.4-0.2.3");
    foreach (var path in new[] {
        Path.Combine(store, "work", "run", "probe.log"),
        Path.Combine(store, "dist", "compat", "patch.pak"),
        Path.Combine(store, "tools", "old", "legacy.exe"),
        Path.Combine(store, "logs", "old.log"),
        Path.Combine(store, "translation-memory", "zhconvert-taiwan.json"),
        Path.Combine(store, "install-state.json"),
        Path.Combine(store, "custom-note.txt"),
        Path.Combine(otherStore, "work", "other.log"),
        Path.Combine(sharedTools, "repak.exe") })
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "keep or clear");
    }
    var sessionLogs = PortableStorage.SessionLogDirectory(game, local);
    Directory.CreateDirectory(sessionLogs);
    var activeLog = Path.Combine(sessionLogs, "GameTranslate-active.log");
    var oldLog = Path.Combine(sessionLogs, "GameTranslate-old.log");
    File.WriteAllText(activeLog, "current session");
    File.WriteAllText(oldLog, "old session");

    var result = PortableCacheCleaner.ClearGameCache(game, activeLog, local);

    True(result.ReclaimedBytes > 0);
    foreach (var name in new[] { "work", "dist", "tools", "logs" })
        False(Directory.Exists(Path.Combine(store, name)));
    False(File.Exists(oldLog));
    True(File.Exists(activeLog));
    True(File.Exists(Path.Combine(store, "translation-memory", "zhconvert-taiwan.json")));
    True(File.Exists(Path.Combine(store, "install-state.json")));
    True(File.Exists(Path.Combine(store, "custom-note.txt")));
    True(File.Exists(Path.Combine(otherStore, "work", "other.log")));
    True(File.Exists(Path.Combine(sharedTools, "repak.exe")));
    True(File.Exists(patch));
}

static void ClearingAbsentCacheIsNoOp()
{
    using var fixture = new TemporaryDirectory();
    var game = Path.Combine(fixture.Path, "Game");
    var local = Path.Combine(fixture.Path, "Local");
    Directory.CreateDirectory(game);

    var result = PortableCacheCleaner.ClearGameCache(game, null, local);

    Equal(0L, result.ReclaimedBytes);
    False(Directory.Exists(Path.Combine(local, "GameTranslate")));
    False(Directory.Exists(Path.Combine(game, ".game-translate")));
}

static void ClearingRefusesUnexpectedStoreFile()
{
    using var fixture = new TemporaryDirectory();
    var game = Path.Combine(fixture.Path, "Game");
    var local = Path.Combine(fixture.Path, "Local");
    Directory.CreateDirectory(game);
    var store = PortableStorage.GameDirectory(game, local);
    Directory.CreateDirectory(Path.GetDirectoryName(store)!);
    File.WriteAllText(store, "user data");

    Throws<InvalidDataException>(() => PortableCacheCleaner.ClearGameCache(game, null, local));
    Equal("user data", File.ReadAllText(store));
}

static void ClearingRefusesActiveGameOperation()
{
    using var fixture = new TemporaryDirectory();
    var game = Path.Combine(fixture.Path, "Game");
    var local = Path.Combine(fixture.Path, "Local");
    Directory.CreateDirectory(game);
    var store = PortableStorage.GameDirectory(game, local);
    var workFile = Path.Combine(store, "work", "active.txt");
    Directory.CreateDirectory(Path.GetDirectoryName(workFile)!);
    File.WriteAllText(workFile, "still needed");
    using var operation = PortableGameOperationLease.Acquire(game, local);

    Throws<IOException>(() => PortableCacheCleaner.ClearGameCache(game, null, local));
    Equal("still needed", File.ReadAllText(workFile));
}

static void ClearingPreservesOtherLiveLog()
{
    using var fixture = new TemporaryDirectory();
    var game = Path.Combine(fixture.Path, "Game");
    var local = Path.Combine(fixture.Path, "Local");
    Directory.CreateDirectory(game);
    var logs = PortableStorage.SessionLogDirectory(game, local);
    Directory.CreateDirectory(logs);
    var current = Path.Combine(logs, "GameTranslate-current.log");
    var other = Path.Combine(logs, "GameTranslate-other.log");
    var old = Path.Combine(logs, "GameTranslate-old.log");
    File.WriteAllText(current, "current");
    File.WriteAllText(other, "other");
    File.WriteAllText(old, "old");
    using var live = new FileStream(other, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);

    PortableCacheCleaner.ClearGameCache(game, current, local);

    True(File.Exists(current));
    True(File.Exists(other));
    False(File.Exists(old));
}

static void SessionLogPathsAreUnique()
{
    using var fixture = new TemporaryDirectory();
    var game = Path.Combine(fixture.Path, "Game");
    Directory.CreateDirectory(game);
    var first = PortableStorage.NewSessionLogPath(game, fixture.Path);
    var second = PortableStorage.NewSessionLogPath(game, fixture.Path);
    False(first.Equals(second, StringComparison.OrdinalIgnoreCase));
    True(first.StartsWith(PortableStorage.SessionLogDirectory(game, fixture.Path) + Path.DirectorySeparatorChar,
        StringComparison.OrdinalIgnoreCase));
}

static void RejectsDriveRootStorage()
{
    using var fixture = new TemporaryDirectory();
    var driveRoot = Path.GetPathRoot(fixture.Path)!;
    Throws<InvalidDataException>(() => PortableStorage.GameDirectory(driveRoot, fixture.Path));
}

static async Task PersistsTranslationMemory()
{
    using var fixture = new TemporaryDirectory();
    var path = Path.Combine(fixture.Path, "memory.json");
    var upstream = new RecordingTranslationApi(values => values.Select(value => "T:" + value).ToArray());
    var first = new TranslationMemoryApi(path, upstream);
    SequenceEqual(new[] { "T:伤害" }, await first.ConvertBatchAsync(["伤害"], CancellationToken.None));

    var offline = new TranslationMemoryApi(path, new ThrowingTranslationApi());
    SequenceEqual(new[] { "T:伤害" }, await offline.ConvertBatchAsync(["伤害"], CancellationToken.None));
    Equal(1, upstream.Calls.Count);
}

static async Task DoesNotCacheFailedBatch()
{
    using var fixture = new TemporaryDirectory();
    var path = Path.Combine(fixture.Path, "memory.json");
    var memory = new TranslationMemoryApi(path, new ThrowingTranslationApi());
    try { await memory.ConvertBatchAsync(["伤害"], CancellationToken.None); }
    catch (InvalidOperationException) { }
    True(!File.Exists(path) || !File.ReadAllText(path).Contains("伤害"));
}

static void ReadsTranslationMemorySnapshots()
{
    var wrapped = TranslationMemoryDocuments.Parse("{\"version\":1,\"entries\":{\"加载\":\"載入\"}}");
    Equal("載入", wrapped["加载"]);
    var flat = TranslationMemoryDocuments.Parse("{\"entries\":\"項目\",\"退出\":\"退出\"}");
    Equal("項目", flat["entries"]);
    Equal("退出", flat["退出"]);
}

static void RejectsInvalidTranslationMemorySnapshots()
{
    Throws<InvalidDataException>(() => TranslationMemoryDocuments.Parse("{\"version\":999,\"entries\":{}}"));
    Throws<InvalidDataException>(() => TranslationMemoryDocuments.Parse("{\"version\":1}"));
    Throws<InvalidDataException>(() => TranslationMemoryDocuments.Parse("{\"加载\":42}"));
}

static void PreservesV3LocresTables()
{
    var (input, prefixLength) = LocresFixture(3, ["继续战斗", "No change"]);
    var result = InvokeLocresSurgery(input, new Dictionary<string, string> { ["继续战斗"] = "繼續戰鬥" });
    Equal((byte)3, (byte)result.GetType().GetProperty("Version")!.GetValue(result)!);
    var output = (byte[])result.GetType().GetProperty("Bytes")!.GetValue(result)!;
    True(input.AsSpan(0, prefixLength).SequenceEqual(output.AsSpan(0, prefixLength)));
    True(output.AsSpan().IndexOf(System.Text.Encoding.Unicode.GetBytes("繼續戰鬥")) >= 0);
}

static void PreservesV1LocresLayout()
{
    var (input, prefixLength) = LocresFixture(1, ["战斗"]);
    var result = InvokeLocresSurgery(input, new Dictionary<string, string> { ["战斗"] = "戰鬥" });
    Equal((byte)1, (byte)result.GetType().GetProperty("Version")!.GetValue(result)!);
    var output = (byte[])result.GetType().GetProperty("Bytes")!.GetValue(result)!;
    True(input.AsSpan(0, prefixLength).SequenceEqual(output.AsSpan(0, prefixLength)));
    True(output.AsSpan().IndexOf(System.Text.Encoding.Unicode.GetBytes("戰鬥")) >= 0);
}

static void PreservesV2Refcounts()
{
    var (input, _) = LocresFixture(2, ["战斗"]);
    var result = InvokeLocresSurgery(input, new Dictionary<string, string> { ["战斗"] = "戰鬥" });
    var output = (byte[])result.GetType().GetProperty("Bytes")!.GetValue(result)!;
    True(input.AsSpan(input.Length - 4).SequenceEqual(output.AsSpan(output.Length - 4)));
}

static void RejectsTrailingLocresBytes()
{
    var (input, _) = LocresFixture(3, ["战斗"]);
    Throws<InvalidDataException>(() => InvokeLocresSurgery([.. input, 0xAA], new Dictionary<string, string>()));
}

static void ArtifactRecordsDescribeCompanions()
{
    True(typeof(ArtifactRecord).GetProperty("Companions") is not null);
    True(typeof(ArtifactRecord).GetProperty("FileHashes") is not null);
}

static void RequiredIoStoreRecordRejectsNoCompanions()
{
    using var fixture = new TemporaryDirectory();
    var source = Path.Combine(fixture.Path, "artifact.pak");
    var paks = Path.Combine(fixture.Path, "Paks");
    Directory.CreateDirectory(paks);
    File.WriteAllText(source, "patch");
    var record = new ArtifactRecord("pakchunk99-GameTranslate_zhHans_P.pak", FileHashes.Sha256(source),
        "compat", "zh-Hans", [], new("../../../", "V11", "Zlib", "0", 0));
    var property = typeof(ArtifactRecord).GetProperty("RequiresIoStoreCompanions");
    True(property is not null);
    property!.SetValue(record, true);
    Throws<InvalidDataException>(() => PatchManager.InstallVerified(source, record, paks));
}

static void MapsRetocVersion()
{
    var type = typeof(FileHashes).Assembly.GetType("GameTranslate.Core.UnrealEngineVersions");
    True(type is not null);
    var method = type!.GetMethod("ToRetocVersion", [typeof(int), typeof(int)]);
    True(method is not null);
    Equal("UE5_7", (string)method!.Invoke(null, [5, 7])!);
    Throws<NotSupportedException>(() =>
    {
        try { method.Invoke(null, [5, 8]); }
        catch (System.Reflection.TargetInvocationException error) when (error.InnerException is not null) { throw error.InnerException; }
    });
}

static void ReadsProbeEngineVersion()
{
    var type = typeof(FileHashes).Assembly.GetType("GameTranslate.Core.UnrealEngineVersions")!;
    var method = type.GetMethod("FromProbeOutput", [typeof(string)])!;
    Equal("UE5_7", (string)method.Invoke(null, ["Loaded archives\r\nUE::Version: GAME_UE5_7\r\n"])!);
    Throws<NotSupportedException>(() =>
    {
        try { method.Invoke(null, ["UE::Version: GAME_UE5_LATEST"]); }
        catch (System.Reflection.TargetInvocationException error) when (error.InnerException is not null) { throw error.InnerException; }
    });
    Throws<NotSupportedException>(() =>
    {
        try { method.Invoke(null, ["UE::Version: GAME_UE5_6\r\nUE::Version: GAME_UE5_7\r\n"]); }
        catch (System.Reflection.TargetInvocationException error) when (error.InnerException is not null) { throw error.InnerException; }
    });
}

static void BuildsLocresTranslations()
{
    var type = typeof(FileHashes).Assembly.GetType("GameTranslate.Core.LocresTranslations");
    True(type is not null);
    var method = type!.GetMethod("FromCsv", [typeof(string), typeof(IReadOnlyDictionary<string, string>)]);
    True(method is not null);
    var memory = new Dictionary<string, string> { ["战斗"] = "戰鬥", ["退出"] = "退出" };
    var map = (IReadOnlyDictionary<string, string>)method!.Invoke(null,
        ["key,source,Translation\r\na,继续游戏,繼續遊戲\r\nb,战斗,战斗\r\n", memory])!;
    Equal("繼續遊戲", map["继续游戏"]);
    Equal("戰鬥", map["战斗"]);
    Equal("退出", map["退出"]);
}

static void AppliesExactOverrides()
{
    var converted = new Dictionary<string, string> { ["简体中文"] = "簡體中文", ["战斗"] = "戰鬥" };
    var map = LocresOverrides.Apply(converted);
    Equal("繁體中文", map["简体中文"]);
    Equal("戰鬥", map["战斗"]);
}

static void RenamesLanguageLabel()
{
    var (input, prefixLength) = LocresFixture(3, ["简体中文"]);
    var result = InvokeLocresSurgery(input, LocresOverrides.Apply(new Dictionary<string, string> { ["简体中文"] = "簡體中文" }));
    var output = (byte[])result.GetType().GetProperty("Bytes")!.GetValue(result)!;
    True(input.AsSpan(0, prefixLength).SequenceEqual(output.AsSpan(0, prefixLength)));
    True(output.AsSpan().IndexOf(System.Text.Encoding.Unicode.GetBytes("繁體中文")) >= 0);
}

static void ProbeVersionResolveReturnsNullForLatest()
{
    var type = typeof(FileHashes).Assembly.GetType("GameTranslate.Core.UnrealEngineVersions")!;
    var method = type.GetMethod("TryFromProbeOutput", [typeof(string)]);
    True(method is not null);
    Equal("UE5_7", (string?)method!.Invoke(null, ["Loaded archives\r\nUE::Version: GAME_UE5_7\r\n"]));
    Equal(null, (string?)method.Invoke(null, ["UE::Version: GAME_UE5_LATEST\r\n"]));
    Equal(null, (string?)method.Invoke(null, ["UE::Version: GAME_UE5_6\r\nUE::Version: GAME_UE5_7\r\n"]));
    Equal(null, (string?)method.Invoke(null, [""]));
}

static void FindsShippingExecutable()
{
    using var fixture = new TemporaryDirectory();
    var binaries = Path.Combine(fixture.Path, "Binaries", "Win64");
    Directory.CreateDirectory(binaries);
    var expected = Path.Combine(binaries, "Example-Win64-Shipping.exe");
    File.WriteAllText(expected, "");
    var type = typeof(FileHashes).Assembly.GetType("GameTranslate.Core.UnrealEngineVersions")!;
    var method = type.GetMethod("FindShippingExecutable", [typeof(string)]);
    True(method is not null);
    Equal(expected, (string)method!.Invoke(null, [fixture.Path])!);
    File.WriteAllText(Path.Combine(binaries, "Second-Win64-Shipping.exe"), "");
    Throws<NotSupportedException>(() =>
    {
        try { method.Invoke(null, [fixture.Path]); }
        catch (System.Reflection.TargetInvocationException error) when (error.InnerException is not null) { throw error.InnerException; }
    });
}

static void BuildsPortableUiState()
{
    var type = typeof(FileHashes).Assembly.GetType("GameTranslate.Core.PortableUi");
    True(type is not null);
    var method = type!.GetMethod("From", [typeof(PatchInspection)]);
    True(method is not null);

    var none = method!.Invoke(null, [new PatchInspection([], [])])!;
    Equal("開始翻譯並安裝 Patch", (string)none.GetType().GetProperty("TranslateButtonText")!.GetValue(none)!);
    Equal(false, (bool)none.GetType().GetProperty("CanRestore")!.GetValue(none)!);
    True(((string)none.GetType().GetProperty("PatchStatusLine")!.GetValue(none)!).Contains("未安裝"));

    var existing = method.Invoke(null, [new PatchInspection([@"C:\Paks\pakchunk99-GameTranslate_zhHans_P.pak"], [])])!;
    Equal("重新翻譯並安裝 Patch", (string)existing.GetType().GetProperty("TranslateButtonText")!.GetValue(existing)!);
    Equal(true, (bool)existing.GetType().GetProperty("CanRestore")!.GetValue(existing)!);
    True(((string)existing.GetType().GetProperty("PatchStatusLine")!.GetValue(existing)!).Contains("pakchunk99-GameTranslate_zhHans_P.pak"));
}

static void DeletesOwnedPatchFiles()
{
    using var fixture = new TemporaryDirectory();
    var paks = fixture.Path;
    string[] owned = [
        "pakchunk99-GameTranslate_zhHans_P.pak",
        "pakchunk99-GameTranslate_zhHans_P.utoc",
        "pakchunk99-GameTranslate_zhHans_P.ucas",
        "pakchunk99-GameTranslate_zhHans_P.pak.disabled",
        "pakchunk99-GameTranslate_zhHant_P.ucas.disabled",
        "pakchunk99-GameTranslate_zhHans_P.pak.failed.20260718000000000",
    ];
    string[] untouchable = ["OtherMod_P.pak", "pakchunk0-Windows.pak", "pakchunk0-Windows.utoc"];
    foreach (var name in owned) File.WriteAllText(Path.Combine(paks, name), "x");
    foreach (var name in untouchable) File.WriteAllText(Path.Combine(paks, name), "x");

    var method = typeof(PatchManager).GetMethod("DeleteOwned", [typeof(string)]);
    True(method is not null);
    var deleted = (IReadOnlyList<string>)method!.Invoke(null, [paks])!;

    Equal(owned.Length, deleted.Count);
    foreach (var name in owned) False(File.Exists(Path.Combine(paks, name)));
    foreach (var name in untouchable) True(File.Exists(Path.Combine(paks, name)));
}

static void EnablesDeleteForResiduals()
{
    var uiType = typeof(FileHashes).Assembly.GetType("GameTranslate.Core.PortableUi")!;
    var from = uiType.GetMethod("From", [typeof(PatchInspection)])!;

    var none = from.Invoke(null, [new PatchInspection([], [])])!;
    Equal(false, (bool)none.GetType().GetProperty("CanDelete")!.GetValue(none)!);

    var residualOnly = (PatchInspection)Activator.CreateInstance(typeof(PatchInspection),
        [new List<string>(), new List<string>(), new List<string> { @"C:\Paks\pakchunk99-GameTranslate_zhHans_P.pak.disabled" }])!;
    var state = from.Invoke(null, [residualOnly])!;
    Equal(false, (bool)state.GetType().GetProperty("CanRestore")!.GetValue(state)!);
    Equal(true, (bool)state.GetType().GetProperty("CanDelete")!.GetValue(state)!);
    Equal("開始翻譯並安裝 Patch", (string)state.GetType().GetProperty("TranslateButtonText")!.GetValue(state)!);
}

static void OffersEnableToggleForDisabledPatch()
{
    var uiType = typeof(FileHashes).Assembly.GetType("GameTranslate.Core.PortableUi")!;
    var from = uiType.GetMethod("From", [typeof(PatchInspection)])!;

    var active = from.Invoke(null, [new PatchInspection([@"C:\Paks\pakchunk99-GameTranslate_zhHans_P.pak"], [])])!;
    Equal("停用翻譯 Patch", (string)active.GetType().GetProperty("ToggleButtonText")!.GetValue(active)!);
    Equal(true, (bool)active.GetType().GetProperty("CanToggle")!.GetValue(active)!);

    var disabledOnly = new PatchInspection([], [], [@"C:\Paks\pakchunk99-GameTranslate_zhHans_P.pak.disabled"]);
    var state = from.Invoke(null, [disabledOnly])!;
    Equal("啟用翻譯 Patch", (string)state.GetType().GetProperty("ToggleButtonText")!.GetValue(state)!);
    Equal(true, (bool)state.GetType().GetProperty("CanToggle")!.GetValue(state)!);

    var none = from.Invoke(null, [new PatchInspection([], [])])!;
    Equal(false, (bool)none.GetType().GetProperty("CanToggle")!.GetValue(none)!);
}

static void EnablesDisabledOwnedPatch()
{
    using var fixture = new TemporaryDirectory();
    var paks = fixture.Path;
    File.WriteAllText(Path.Combine(paks, "pakchunk99-GameTranslate_zhHans_P.pak.disabled"), "x");
    File.WriteAllText(Path.Combine(paks, "pakchunk99-GameTranslate_zhHans_P.utoc.disabled"), "x");
    File.WriteAllText(Path.Combine(paks, "pakchunk99-GameTranslate_zhHans_P.ucas.disabled"), "x");
    File.WriteAllText(Path.Combine(paks, "pakchunk99-GameTranslate_zhHant_P.pak.disabled"), "x");
    File.WriteAllText(Path.Combine(paks, "pakchunk99-GameTranslate_zhHant_P.pak"), "already-active");
    File.WriteAllText(Path.Combine(paks, "OtherMod_P.pak.disabled"), "x");

    var method = typeof(PatchManager).GetMethod("EnableDisabled", [typeof(string)]);
    True(method is not null);
    var enabled = (IReadOnlyList<string>)method!.Invoke(null, [paks])!;

    Equal(3, enabled.Count);
    True(File.Exists(Path.Combine(paks, "pakchunk99-GameTranslate_zhHans_P.pak")));
    True(File.Exists(Path.Combine(paks, "pakchunk99-GameTranslate_zhHans_P.utoc")));
    True(File.Exists(Path.Combine(paks, "pakchunk99-GameTranslate_zhHans_P.ucas")));
    False(File.Exists(Path.Combine(paks, "pakchunk99-GameTranslate_zhHans_P.pak.disabled")));
    Equal("already-active", File.ReadAllText(Path.Combine(paks, "pakchunk99-GameTranslate_zhHant_P.pak")));
    True(File.Exists(Path.Combine(paks, "pakchunk99-GameTranslate_zhHant_P.pak.disabled")));
    True(File.Exists(Path.Combine(paks, "OtherMod_P.pak.disabled")));
}

static void DeletesOnlyResiduals()
{
    using var fixture = new TemporaryDirectory();
    var paks = fixture.Path;
    File.WriteAllText(Path.Combine(paks, "pakchunk99-GameTranslate_zhHans_P.pak"), "x");
    File.WriteAllText(Path.Combine(paks, "pakchunk99-GameTranslate_zhHans_P.utoc"), "x");
    File.WriteAllText(Path.Combine(paks, "pakchunk99-GameTranslate_zhHans_P.pak.disabled"), "x");
    File.WriteAllText(Path.Combine(paks, "pakchunk99-GameTranslate_zhHant_P.ucas.disabled"), "x");

    var method = typeof(PatchManager).GetMethod("DeleteResiduals", [typeof(string)]);
    True(method is not null);
    var deleted = (IReadOnlyList<string>)method!.Invoke(null, [paks])!;

    Equal(2, deleted.Count);
    True(File.Exists(Path.Combine(paks, "pakchunk99-GameTranslate_zhHans_P.pak")));
    True(File.Exists(Path.Combine(paks, "pakchunk99-GameTranslate_zhHans_P.utoc")));
    False(File.Exists(Path.Combine(paks, "pakchunk99-GameTranslate_zhHans_P.pak.disabled")));
    False(File.Exists(Path.Combine(paks, "pakchunk99-GameTranslate_zhHant_P.ucas.disabled")));
}

static void EnablePicksNewestCompleteGeneration()
{
    using var fixture = new TemporaryDirectory();
    var paks = fixture.Path;
    const string stem = "pakchunk99-GameTranslate_zhHans_P";
    foreach (var ext in new[] { "pak", "utoc", "ucas" })
    {
        File.WriteAllText(Path.Combine(paks, $"{stem}.{ext}.disabled"), "old");
        File.WriteAllText(Path.Combine(paks, $"{stem}.{ext}.disabled.20270101000000000"), "new");
    }

    var enabled = (IReadOnlyList<string>)typeof(PatchManager).GetMethod("EnableDisabled", [typeof(string)])!.Invoke(null, [paks])!;

    Equal(3, enabled.Count);
    foreach (var ext in new[] { "pak", "utoc", "ucas" })
    {
        Equal("new", File.ReadAllText(Path.Combine(paks, $"{stem}.{ext}")));
        True(File.Exists(Path.Combine(paks, $"{stem}.{ext}.disabled")));
    }
}

static void EnableNeverMixesGenerations()
{
    using var fixture = new TemporaryDirectory();
    var paks = fixture.Path;
    const string stem = "pakchunk99-GameTranslate_zhHans_P";
    // Older plain generation is incomplete (no ucas); newer timestamped generation is complete.
    File.WriteAllText(Path.Combine(paks, $"{stem}.pak.disabled"), "old");
    File.WriteAllText(Path.Combine(paks, $"{stem}.utoc.disabled"), "old");
    foreach (var ext in new[] { "pak", "utoc", "ucas" })
        File.WriteAllText(Path.Combine(paks, $"{stem}.{ext}.disabled.20270101000000000"), "new");

    var enabled = (IReadOnlyList<string>)typeof(PatchManager).GetMethod("EnableDisabled", [typeof(string)])!.Invoke(null, [paks])!;

    Equal(3, enabled.Count);
    foreach (var ext in new[] { "pak", "utoc", "ucas" })
        Equal("new", File.ReadAllText(Path.Combine(paks, $"{stem}.{ext}")));
    True(File.Exists(Path.Combine(paks, $"{stem}.pak.disabled")));
    True(File.Exists(Path.Combine(paks, $"{stem}.utoc.disabled")));
}

static void EnableRollsBackOnFailure()
{
    using var fixture = new TemporaryDirectory();
    var paks = fixture.Path;
    const string stem = "pakchunk99-GameTranslate_zhHans_P";
    foreach (var ext in new[] { "pak", "utoc", "ucas" })
        File.WriteAllText(Path.Combine(paks, $"{stem}.{ext}.disabled"), "x");
    using var locked = new FileStream(Path.Combine(paks, $"{stem}.ucas.disabled"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

    var method = typeof(PatchManager).GetMethod("EnableDisabled", [typeof(string)])!;
    Throws<IOException>(() =>
    {
        try { method.Invoke(null, [paks]); }
        catch (System.Reflection.TargetInvocationException error) when (error.InnerException is not null) { throw error.InnerException; }
    });

    foreach (var ext in new[] { "pak", "utoc", "ucas" })
    {
        False(File.Exists(Path.Combine(paks, $"{stem}.{ext}")));
        True(File.Exists(Path.Combine(paks, $"{stem}.{ext}.disabled")));
    }
}

static void UsesToolsDirectoryForUeExtractor()
{
    var method = typeof(PortableToolPaths).GetMethod("UeExtractorWorkingDirectory", Type.EmptyTypes);
    True(method is not null);

    var hosted = new PortableToolPaths(@"C:\Game\GameTranslate.exe",
        ["--ueextractor-host", @"C:\Users\Example\AppData\Local\GameTranslate\games\ABC\tools\1.0.8.4-0.2.3\UEExtractor.dll"],
        @"C:\tools\repak.exe", @"C:\tools\retoc.exe");
    Equal(@"C:\Users\Example\AppData\Local\GameTranslate\games\ABC\tools\1.0.8.4-0.2.3", (string)method!.Invoke(hosted, [])!);

    var bare = new PortableToolPaths(@"C:\Tools\UEExtractor.exe", [], @"C:\tools\repak.exe", @"C:\tools\retoc.exe");
    Equal(@"C:\Tools", (string)method.Invoke(bare, [])!);
}

static void ParsesZhCnCandidates()
{
    const string log = """
      Reading: RSDragonwilds/Content/Localization/Game/zh-CN/Game.locres (from RSDragonwilds-Windows.pak)
      Reading: RSDragonwilds/Content/Localization/Game/en/Game.locres (from RSDragonwilds-Windows.pak)
      Reading: Example/Content/Localization/Game/zh-Hans/Game.locres (from pakchunk0-Windows.pak)
      """;
    var candidates = UnrealProbeParser.ParseSimplifiedChineseCandidates(log);
    Equal(2, candidates.Count);
    var zhCn = candidates.Single(candidate => candidate.VirtualPath.Contains("/zh-CN/", StringComparison.Ordinal));
    Equal("RSDragonwilds-Windows.pak", zhCn.SourceArchive);
    Equal("zh-CN", (string)zhCn.GetType().GetProperty("Culture")!.GetValue(zhCn)!);
    var zhHans = candidates.Single(candidate => candidate.VirtualPath.Contains("/zh-Hans/", StringComparison.Ordinal));
    Equal("zh-Hans", (string)zhHans.GetType().GetProperty("Culture")!.GetValue(zhHans)!);
}

static void PicksDominantCulture()
{
    var type = typeof(FileHashes).Assembly.GetType("GameTranslate.Core.SimplifiedCultures");
    True(type is not null);
    var method = type!.GetMethod("Dominant", [typeof(IReadOnlyList<string>)]);
    True(method is not null);
    Equal("zh-CN", (string)method!.Invoke(null, [(IReadOnlyList<string>)["zh-CN", "zh-CN", "zh-Hans"]])!);
    Equal("zh-Hans", (string)method.Invoke(null, [(IReadOnlyList<string>)["zh-Hans"]])!);
}

static void DerivesCulturePatchNames()
{
    var type = typeof(FileHashes).Assembly.GetType("GameTranslate.Core.SimplifiedCultures")!;
    var method = type.GetMethod("PatchName", [typeof(string)]);
    True(method is not null);
    Equal("pakchunk99-GameTranslate_zhHans_P.pak", (string)method!.Invoke(null, ["zh-Hans"])!);
    Equal("pakchunk99-GameTranslate_zhCN_P.pak", (string)method.Invoke(null, ["zh-CN"])!);
}

static void ReadsInstalledTargetCulture()
{
    using var fixture = new TemporaryDirectory();
    var stateDirectory = Path.Combine(fixture.Path, ".game-translate");
    Directory.CreateDirectory(stateDirectory);
    File.WriteAllText(Path.Combine(stateDirectory, "install-state.json"),
        """{ "version": 3, "active": true, "artifact": { "TargetCulture": "zh-CN" } }""");
    Equal("zh-CN", SimplifiedCultures.InstalledTargetCulture(fixture.Path));
    Equal("zh-Hans", SimplifiedCultures.InstalledTargetCulture(Path.Combine(fixture.Path, "no-such-root")));
}

static void ReadsCultureFromAppData()
{
    using var fixture = new TemporaryDirectory();
    var gameRoot = Path.Combine(fixture.Path, "Game");
    Directory.CreateDirectory(gameRoot);
    var localRoot = Path.Combine(fixture.Path, "Local");
    var stateDirectory = PortableStorage.GameDirectory(gameRoot, localRoot);
    Directory.CreateDirectory(stateDirectory);
    File.WriteAllText(Path.Combine(stateDirectory, "install-state.json"),
        """{ "version": 3, "active": true, "artifact": { "TargetCulture": "zh-CN", "SourceCulture": "zh-Hans" } }""");
    var method = typeof(SimplifiedCultures).GetMethod("InstalledTargetCulture", [typeof(string), typeof(string)]);
    True(method is not null);
    Equal("zh-Hans", (string)method!.Invoke(null, [gameRoot, localRoot])!);
    False(Directory.Exists(Path.Combine(gameRoot, ".game-translate")));
}

static void ConflictingCultureStateRefusesGuess()
{
    using var fixture = new TemporaryDirectory();
    var game = Path.Combine(fixture.Path, "Game");
    var local = Path.Combine(fixture.Path, "Local");
    Directory.CreateDirectory(game);
    var legacy = Path.Combine(game, ".game-translate");
    var destination = PortableStorage.GameDirectory(game, local);
    Directory.CreateDirectory(legacy);
    Directory.CreateDirectory(destination);
    File.WriteAllText(Path.Combine(legacy, "install-state.json"),
        """{"artifact":{"SourceCulture":"zh-CN"}}""");
    File.WriteAllText(Path.Combine(destination, "install-state.json"),
        """{"artifact":{"SourceCulture":"zh-Hans"}}""");
    Throws<InvalidDataException>(() => SimplifiedCultures.InstalledTargetCulture(game, local));
}

static void BuildsUeExtractorVersionArgument()
{
    var type = typeof(FileHashes).Assembly.GetType("GameTranslate.Core.UnrealEngineVersions")!;
    var method = type.GetMethod("UeExtractorVersionArgument", [typeof(int), typeof(int)]);
    True(method is not null);
    Equal("--version=5.6", (string)method!.Invoke(null, [5, 6])!);
    Equal("--version=4.27", (string)method.Invoke(null, [4, 27])!);
}

static void UpdatesExistingCultureKey()
{
    var updated = CultureConfig.UpdateText("[Internationalization]\r\nCulture=zh-CN\r\nLanguage=zh-CN\r\nLocale=zh-CN\r\n", "zh-Hant");
    True(updated.Contains("Culture=zh-Hant", StringComparison.Ordinal));
    True(updated.Contains("Language=zh-Hant", StringComparison.Ordinal));
    True(updated.Contains("Locale=zh-Hant", StringComparison.Ordinal));
    False(updated.Contains("zh-CN", StringComparison.Ordinal));

    var withoutCulture = CultureConfig.UpdateText("[Internationalization]\r\nLanguage=zh-Hans\r\nLocale=zh-Hans\r\n", "zh-Hant");
    False(withoutCulture.Contains("Culture=", StringComparison.Ordinal));
}

static void MapsModeToTargetCulture()
{
    var type = typeof(FileHashes).Assembly.GetType("GameTranslate.Core.TranslationModes");
    True(type is not null);
    var method = type!.GetMethod("TargetCulture", [typeof(TranslationMode), typeof(string)]);
    True(method is not null);
    Equal("zh-CN", (string)method!.Invoke(null, [TranslationMode.Compatibility, "zh-CN"])!);
    Equal("zh-Hant", (string)method.Invoke(null, [TranslationMode.NativeTraditional, "zh-CN"])!);
    Equal("zh-Hant", (string)method.Invoke(null, [TranslationMode.NativeTraditional, "zh-Hans"])!);
}

static void RestoreCulturePrefersSource()
{
    using var fixture = new TemporaryDirectory();
    var stateDirectory = Path.Combine(fixture.Path, ".game-translate");
    Directory.CreateDirectory(stateDirectory);
    File.WriteAllText(Path.Combine(stateDirectory, "install-state.json"),
        """{ "version": 3, "active": true, "artifact": { "Mode": "native", "TargetCulture": "zh-Hant", "SourceCulture": "zh-CN" } }""");
    Equal("zh-CN", SimplifiedCultures.InstalledTargetCulture(fixture.Path));
}

static async Task RetriesToolRunOnce()
{
    var type = typeof(FileHashes).Assembly.GetType("GameTranslate.Core.Retry");
    True(type is not null);
    var method = type!.GetMethods().Single(candidate => candidate.Name == "OnceAsync").MakeGenericMethod(typeof(int));

    var attempts = 0;
    var flaky = (Func<Task<int>>)(() => Task.FromResult(++attempts));
    var succeedsOnSecond = (Func<int, bool>)(value => value >= 2);
    var retried = await (Task<int>)method.Invoke(null, [flaky, succeedsOnSecond, null, 1])!;
    Equal(2, retried);
    Equal(2, attempts);

    attempts = 0;
    var alwaysOk = (Func<int, bool>)(_ => true);
    var single = await (Task<int>)method.Invoke(null, [flaky, alwaysOk, null, 1])!;
    Equal(1, single);
    Equal(1, attempts);
}

static async Task RetriesMissingFocusedCsv()
{
    using var fixture = new TemporaryDirectory();
    var expected = Path.Combine(fixture.Path, "OnlineSubsystemSteam.csv");
    var runner = new ScriptedProcessRunner(attempt =>
    {
        if (attempt == 2) File.WriteAllText(expected, "key,source,Translation\nentry,简体,\n");
        return new ProcessResult(0, attempt == 1 ? "Mount() newly mounted: 0" : "Mount() newly mounted: 4");
    });
    var result = await UeExtractionFiles.ExtractWithRetryAsync(runner, "UEExtractor.exe", ["game", "output"], fixture.Path,
        fixture.Path, "OnlineSubsystemSteam", "pakchunk0-Windows", CancellationToken.None);
    Equal(2, runner.Attempts);
    Equal(expected, result.Csv);
    True(File.ReadAllText(result.Csv).Contains("简体", StringComparison.Ordinal));
}

static async Task MissingFocusedCsvFailsClosed()
{
    using var fixture = new TemporaryDirectory();
    var runner = new ScriptedProcessRunner(_ => new ProcessResult(0, "Mount() newly mounted: 0"));
    await ThrowsAsync<FileNotFoundException>(() => UeExtractionFiles.ExtractWithRetryAsync(runner, "UEExtractor.exe",
        ["game", "output"], fixture.Path, fixture.Path, "OnlineSubsystemSteam", "pakchunk0-Windows", CancellationToken.None));
    Equal(2, runner.Attempts);
}

static async Task RetriesEmptyFocusedCsv()
{
    using var fixture = new TemporaryDirectory();
    var csv = Path.Combine(fixture.Path, "Game.csv");
    var runner = new ScriptedProcessRunner(attempt =>
    {
        File.WriteAllText(csv, attempt == 1 ? string.Empty : "key,source,Translation\nentry,简体,\n");
        return new ProcessResult(0, "Mount() newly mounted: 4");
    });
    var result = await UeExtractionFiles.ExtractWithRetryAsync(runner, "UEExtractor.exe", ["game", "output"], fixture.Path,
        fixture.Path, "Game", "pakchunk0-Windows", CancellationToken.None);
    Equal(2, runner.Attempts);
    Equal(csv, result.Csv);
    True(new FileInfo(result.Csv).Length > 0);
}

static async Task DoesNotReuseFailedFocusedCsv()
{
    using var fixture = new TemporaryDirectory();
    var csv = Path.Combine(fixture.Path, "Game.csv");
    var runner = new ScriptedProcessRunner(attempt =>
    {
        if (attempt == 1)
        {
            File.WriteAllText(csv, "key,source,Translation\nentry,partial,\n");
            return new ProcessResult(1, "first attempt failed after a partial write");
        }
        return new ProcessResult(0, "Mount() newly mounted: 0");
    });
    await ThrowsAsync<FileNotFoundException>(() => UeExtractionFiles.ExtractWithRetryAsync(runner, "UEExtractor.exe",
        ["game", "output"], fixture.Path, fixture.Path, "Game", "pakchunk0-Windows", CancellationToken.None));
    Equal(2, runner.Attempts);
    False(File.Exists(csv));
}

static async Task RejectsPreexistingFocusedCsv()
{
    using var fixture = new TemporaryDirectory();
    var csv = Path.Combine(fixture.Path, "Game.csv");
    File.WriteAllText(csv, "older run");
    var runner = new ScriptedProcessRunner(_ => new ProcessResult(0, "Mount() newly mounted: 0"));
    await ThrowsAsync<InvalidDataException>(() => UeExtractionFiles.ExtractWithRetryAsync(runner, "UEExtractor.exe",
        ["game", "output"], fixture.Path, fixture.Path, "Game", "pakchunk0-Windows", CancellationToken.None));
    Equal(0, runner.Attempts);
    Equal("older run", File.ReadAllText(csv));
}

static object InvokeLocresSurgery(byte[] input, IReadOnlyDictionary<string, string> translations)
{
    var type = typeof(FileHashes).Assembly.GetType("GameTranslate.Core.UnrealLocresSurgery");
    True(type is not null);
    var method = type!.GetMethod("Patch", [typeof(byte[]), typeof(IReadOnlyDictionary<string, string>)]);
    True(method is not null);
    try { return method!.Invoke(null, [input, translations])!; }
    catch (System.Reflection.TargetInvocationException error) when (error.InnerException is not null) { throw error.InnerException; }
}

static (byte[] Bytes, int PrefixLength) LocresFixture(byte version, IReadOnlyList<string> strings)
{
    byte[] magic = [0x0E, 0x14, 0x74, 0x75, 0x67, 0x4A, 0x03, 0xFC, 0x4A, 0x15, 0x90, 0x9D, 0xC3, 0x37, 0x7F, 0x1B];
    var table = System.Text.Encoding.ASCII.GetBytes("namespace-key-and-cityhash-table");
    var arrayOffset = 25 + table.Length;
    using var stream = new MemoryStream();
    using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
    writer.Write(magic);
    writer.Write(version);
    writer.Write((long)arrayOffset);
    writer.Write(table);
    writer.Write(strings.Count);
    for (var index = 0; index < strings.Count; index++)
    {
        WriteFixtureFString(writer, strings[index]);
        if (version >= 2) writer.Write(index + 7);
    }
    return (stream.ToArray(), arrayOffset + 4);
}

static void WriteFixtureFString(BinaryWriter writer, string value)
{
    if (value.All(character => character <= 0x7F))
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(value);
        writer.Write(bytes.Length + 1);
        writer.Write(bytes);
        writer.Write((byte)0);
        return;
    }
    writer.Write(-(value.Length + 1));
    writer.Write(System.Text.Encoding.Unicode.GetBytes(value));
    writer.Write((ushort)0);
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected {expected}, got {actual}");
}

static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual)
{
    if (!expected.SequenceEqual(actual))
        throw new Exception($"Expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}]");
}

static void True(bool value)
{
    if (!value) throw new Exception("Expected true");
}

static void False(bool value)
{
    if (value) throw new Exception("Expected false");
}

static void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}");
}

static async Task AcceptsVerifiedNativeRuntime()
{
    using var fixture = new TemporaryDirectory();
    var dll = Path.Combine(fixture.Path, "sample.dll");
    await File.WriteAllBytesAsync(dll, [1, 2, 3]);
    var spec = new PinnedZipAsset("sample.dll", "bin/sample.dll", new Uri("https://example.test/native.zip"),
        "unused-for-existing-file", Convert.ToHexString(SHA256.HashData([1, 2, 3])));
    using var client = new HttpClient(new StubHttpHandler(_ => throw new Exception("Network must not be used")));
    await PinnedZipRuntime.EnsureAsync(fixture.Path, spec, client, CancellationToken.None);
    SequenceEqual(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(dll));
}

static async Task RejectsModifiedNativeRuntime()
{
    using var fixture = new TemporaryDirectory();
    var dll = Path.Combine(fixture.Path, "sample.dll");
    await File.WriteAllBytesAsync(dll, [4, 5, 6]);
    var spec = new PinnedZipAsset("sample.dll", "bin/sample.dll", new Uri("https://example.test/native.zip"),
        "unused-for-existing-file", Convert.ToHexString(SHA256.HashData([1, 2, 3])));
    using var client = new HttpClient(new StubHttpHandler(_ => throw new Exception("Network must not be used")));
    await ThrowsAsync<InvalidDataException>(() => PinnedZipRuntime.EnsureAsync(fixture.Path, spec, client, CancellationToken.None));
    SequenceEqual(new byte[] { 4, 5, 6 }, await File.ReadAllBytesAsync(dll));
}

static async Task ReplacesKnownLegacyNativeRuntime()
{
    using var fixture = new TemporaryDirectory();
    var dll = Path.Combine(fixture.Path, "sample.dll");
    var legacyBytes = new byte[] { 4, 5, 6 };
    var currentBytes = new byte[] { 7, 8, 9 };
    await File.WriteAllBytesAsync(dll, legacyBytes);
    byte[] archiveBytes;
    using (var buffer = new MemoryStream())
    {
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        using (var entry = archive.CreateEntry("bin/sample.dll").Open())
            entry.Write(currentBytes);
        archiveBytes = buffer.ToArray();
    }
    var spec = new PinnedZipAsset("sample.dll", "bin/sample.dll", new Uri("https://example.test/native.zip"),
        Convert.ToHexString(SHA256.HashData(archiveBytes)), Convert.ToHexString(SHA256.HashData(currentBytes)),
        LegacyFileSha256: Convert.ToHexString(SHA256.HashData(legacyBytes)));
    using var client = new HttpClient(new StubHttpHandler(_ => new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
    { Content = new ByteArrayContent(archiveBytes) }));
    await PinnedZipRuntime.EnsureAsync(fixture.Path, spec, client, CancellationToken.None);
    SequenceEqual(currentBytes, await File.ReadAllBytesAsync(dll));
    Equal(1, Directory.GetFiles(fixture.Path).Length);
}

static async Task KeepsKnownLegacyOnBadDownload()
{
    using var fixture = new TemporaryDirectory();
    var dll = Path.Combine(fixture.Path, "sample.dll");
    var legacyBytes = new byte[] { 4, 5, 6 };
    await File.WriteAllBytesAsync(dll, legacyBytes);
    var spec = new PinnedZipAsset("sample.dll", "bin/sample.dll", new Uri("https://example.test/native.zip"),
        "NOT_THE_DOWNLOADED_ARCHIVE", Convert.ToHexString(SHA256.HashData([7, 8, 9])),
        LegacyFileSha256: Convert.ToHexString(SHA256.HashData(legacyBytes)));
    using var client = new HttpClient(new StubHttpHandler(_ => new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
    { Content = new ByteArrayContent([1, 2, 3]) }));
    await ThrowsAsync<InvalidDataException>(() => PinnedZipRuntime.EnsureAsync(fixture.Path, spec, client, CancellationToken.None));
    SequenceEqual(legacyBytes, await File.ReadAllBytesAsync(dll));
    Equal(1, Directory.GetFiles(fixture.Path).Length);
}

static async Task VerifiesNativeRuntimeDownload()
{
    using var fixture = new TemporaryDirectory();
    var dllBytes = new byte[] { 7, 8, 9 };
    byte[] archiveBytes;
    using (var buffer = new MemoryStream())
    {
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        using (var entry = archive.CreateEntry("bin/sample.dll").Open())
            entry.Write(dllBytes);
        archiveBytes = buffer.ToArray();
    }
    var spec = new PinnedZipAsset("sample.dll", "bin/sample.dll", new Uri("https://example.test/native.zip"),
        Convert.ToHexString(SHA256.HashData(archiveBytes)), Convert.ToHexString(SHA256.HashData(dllBytes)));
    using var client = new HttpClient(new StubHttpHandler(_ => new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
    { Content = new ByteArrayContent(archiveBytes) }));
    await ThrowsAsync<InvalidDataException>(() => PinnedZipRuntime.EnsureAsync(fixture.Path,
        spec with { ArchiveSha256 = "BAD" }, client, CancellationToken.None));
    False(File.Exists(Path.Combine(fixture.Path, "sample.dll")));
    await ThrowsAsync<InvalidDataException>(() => PinnedZipRuntime.EnsureAsync(fixture.Path,
        spec with { FileSha256 = "BAD" }, client, CancellationToken.None));
    False(File.Exists(Path.Combine(fixture.Path, "sample.dll")));
    await PinnedZipRuntime.EnsureAsync(fixture.Path, spec, client, CancellationToken.None);
    SequenceEqual(dllBytes, await File.ReadAllBytesAsync(Path.Combine(fixture.Path, "sample.dll")));
}

static void RejectsUnverifiedRepakRuntime()
{
    using var fixture = new TemporaryDirectory();
    File.WriteAllBytes(Path.Combine(fixture.Path, "oo2core_9_win64.dll"), [1, 2, 3]);
    Throws<InvalidDataException>(() => NativeRuntimePreflight.VerifyExistingRepakRuntime(fixture.Path));
}

static async Task WorkflowRejectsUnverifiedRuntime()
{
    using var fixture = new TemporaryDirectory();
    var toolsDirectory = Path.Combine(fixture.Path, "tools");
    Directory.CreateDirectory(toolsDirectory);
    await File.WriteAllBytesAsync(Path.Combine(toolsDirectory, "oo2core_9_win64.dll"), [1, 2, 3]);
    var paths = new PortableToolPaths(Path.Combine(toolsDirectory, "UEExtractor.exe"), [],
        Path.Combine(toolsDirectory, "repak.exe"), Path.Combine(toolsDirectory, "retoc.exe"));
    var game = new GameDetection(fixture.Path, EngineKind.Unreal, PackagingKind.Pak, "Example",
        Path.Combine(fixture.Path, "Example", "Content", "Paks"), []);
    var workflow = new UnrealTranslationWorkflow(new NeverProcessRunner(), new ThrowingTranslationApi(), paths, _ => { });
    await ThrowsAsync<InvalidDataException>(() => workflow.BuildAndInstallAsync(game, TranslationMode.Compatibility, CancellationToken.None));
}

static async Task WorkflowRespectsGameOperationLease()
{
    using var fixture = new TemporaryDirectory();
    var gameRoot = Path.Combine(fixture.Path, "Game");
    var local = Path.Combine(fixture.Path, "Local");
    var paks = Path.Combine(gameRoot, "Example", "Content", "Paks");
    var toolsDirectory = Path.Combine(fixture.Path, "tools");
    Directory.CreateDirectory(paks);
    Directory.CreateDirectory(toolsDirectory);
    await File.WriteAllBytesAsync(Path.Combine(toolsDirectory, "oo2core_9_win64.dll"), [1, 2, 3]);
    var paths = new PortableToolPaths(Path.Combine(toolsDirectory, "UEExtractor.exe"), [],
        Path.Combine(toolsDirectory, "repak.exe"), Path.Combine(toolsDirectory, "retoc.exe"));
    var game = new GameDetection(gameRoot, EngineKind.Unreal, PackagingKind.Pak, "Example", paks, []);
    var workflow = new UnrealTranslationWorkflow(new NeverProcessRunner(), new ThrowingTranslationApi(), paths, _ => { }, local);
    using var clearing = PortableGameOperationLease.Acquire(gameRoot, local);

    await ThrowsAsync<IOException>(() => workflow.BuildAndInstallAsync(game, TranslationMode.Compatibility, CancellationToken.None));
    False(Directory.Exists(PortableStorage.GameDirectory(gameRoot, local)));
}

static async Task ThrowsAsync<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}");
}

sealed class TemporaryDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"game-translate-tests-{Guid.NewGuid():N}");

    public TemporaryDirectory() => Directory.CreateDirectory(Path);

    public void Dispose() => Directory.Delete(Path, recursive: true);
}

sealed class RecordingTranslationApi(Func<IReadOnlyList<string>, IReadOnlyList<string>> convert) : ITranslationApi
{
    public List<IReadOnlyList<string>> Calls { get; } = [];

    public Task<IReadOnlyList<string>> ConvertBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        Calls.Add(texts);
        return Task.FromResult(convert(texts));
    }
}

sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(respond(request));
}

sealed class NeverProcessRunner : IProcessRunner
{
    public Task<ProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments, string? workingDirectory, CancellationToken cancellationToken) =>
        throw new Exception("No external process may start before native runtime verification.");
}

sealed class ScriptedProcessRunner(Func<int, ProcessResult> run) : IProcessRunner
{
    public int Attempts { get; private set; }

    public Task<ProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments, string? workingDirectory, CancellationToken cancellationToken) =>
        Task.FromResult(run(++Attempts));
}

sealed class ThrowingTranslationApi : ITranslationApi
{
    public Task<IReadOnlyList<string>> ConvertBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("offline");
}
