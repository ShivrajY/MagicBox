using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
using MagicBox.Browser;
using MagicBox.Core;

namespace MagicBox.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            return await BuildRootCommand().Parse(args).InvokeAsync(new InvocationConfiguration
            {
                EnableDefaultExceptionHandler = false
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return 130;
        }
        catch (InvalidDataException ex)
        {
            Console.Error.WriteLine($"validation error: {ex.Message}");
            return 2;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"input error: {ex.Message}");
            return 2;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine($"secret or permission error: {ex.Message}");
            return 3;
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"file error: {ex.Message}");
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"run failed: {ex.Message}");
            return 4;
        }
    }

    private static RootCommand BuildRootCommand()
    {
        var root = new RootCommand("MagicBox universal configuration-driven scraper");

        var adapter = new Command("adapter", "Validate or list YAML adapters.");
        var adapterList = new Command("list", "List validated adapters.");
        var adapterListDir = AdaptersDirectoryOption();
        adapterList.Options.Add(adapterListDir);
        adapterList.SetAction(parseResult =>
        {
            var registry = LoadRegistry(parseResult.GetValue(adapterListDir));
            foreach (var item in registry.All.OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine($"{item.Id}\t{item.Kind}");
            }
        });

        var adapterValidate = new Command("validate", "Validate one adapter file or every YAML file in a directory.");
        var validatePath = new Argument<string>("file-or-directory");
        var validateDir = AdaptersDirectoryOption();
        adapterValidate.Arguments.Add(validatePath);
        adapterValidate.Options.Add(validateDir);
        adapterValidate.SetAction(parseResult =>
        {
            var target = Path.GetFullPath(parseResult.GetRequiredValue(validatePath));
            var schemaPath = FindSchemaPath();
            var directories = Directory.Exists(target)
                ? new[] { target }
                : new[] { Path.GetDirectoryName(target) ?? Directory.GetCurrentDirectory() };
            _ = parseResult.GetValue(validateDir);
            var registry = AdapterRegistry.Load(directories, schemaPath);
            Console.WriteLine($"validated {registry.All.Count} adapter(s)");
        });
        adapter.Subcommands.Add(adapterList);
        adapter.Subcommands.Add(adapterValidate);
        root.Subcommands.Add(adapter);

        var scrape = new Command("scrape", "Run a configured adapter and stream records.");
        var scrapeAdapter = new Argument<string>("adapter-id");
        scrape.Arguments.Add(scrapeAdapter);
        AddCommonOptions(scrape, out var scrapeOptions);
        var scrapeInput = new Option<string[]>("--input", "-i")
        {
            Description = "Adapter input as key=value. Repeat for multiple inputs.",
            Arity = ArgumentArity.ZeroOrMore
        };
        scrape.Options.Add(scrapeInput);
        scrape.SetAction((parseResult, cancellationToken) => RunScrapeAsync(parseResult, cancellationToken, scrapeAdapter, scrapeInput, scrapeOptions));
        root.Subcommands.Add(scrape);

        var search = new Command("search", "Run one or more search adapters and stream standardized results.");
        var searchQuery = new Argument<string>("query");
        var searchEngines = new Option<string?>("--engines", "-e")
        {
            Description = "Comma-separated adapter IDs. Defaults to all built-in search adapters."
        };
        search.Arguments.Add(searchQuery);
        search.Options.Add(searchEngines);
        AddCommonOptions(search, out var searchOptions);
        search.SetAction((parseResult, cancellationToken) => RunSearchAsync(parseResult, cancellationToken, searchQuery, searchEngines, searchOptions));
        root.Subcommands.Add(search);

        var login = new Command("login", "Run a configured browser login flow for an adapter.");
        var loginAdapter = new Argument<string>("adapter-id");
        var loginHeaded = new Option<bool>("--headed") { Description = "Open a visible Chromium window for manual challenge completion." };
        var loginAdaptersDir = AdaptersDirectoryOption();
        login.Arguments.Add(loginAdapter);
        login.Options.Add(loginHeaded);
        login.Options.Add(loginAdaptersDir);
        login.SetAction((parseResult, cancellationToken) => RunLoginAsync(parseResult, cancellationToken, loginAdapter, loginHeaded, loginAdaptersDir));
        root.Subcommands.Add(login);

        var secrets = BuildSecretsCommand();
        root.Subcommands.Add(secrets);

        var doctor = new Command("doctor", "Check the local MagicBox installation.");
        var doctorBrowser = new Option<bool>("--browser") { Description = "Also check whether the Playwright Chromium binary is installed." };
        doctor.Options.Add(doctorBrowser);
        doctor.SetAction((parseResult, cancellationToken) => RunDoctorAsync(parseResult.GetValue(doctorBrowser), cancellationToken));
        root.Subcommands.Add(doctor);

        return root;
    }

    private static void AddCommonOptions(Command command, out CommonOptions options)
    {
        var profile = new Option<string>("--profile") { DefaultValueFactory = _ => "local", Description = "Execution profile: local or high-volume." };
        var pages = new Option<int>("--pages") { DefaultValueFactory = _ => 1, Description = "Maximum pages." };
        var records = new Option<int>("--records") { DefaultValueFactory = _ => 100, Description = "Maximum records." };
        var format = new Option<string>("--format") { DefaultValueFactory = _ => "jsonl", Description = "Output format: jsonl or csv." };
        var output = new Option<string?>("--output", "-o") { Description = "Output file; stdout is the default." };
        var adaptersDir = AdaptersDirectoryOption();
        var proxyPool = new Option<string?>("--proxy-pool") { Description = "Logical proxy pool name." };
        var allowPrivate = new Option<bool>("--allow-private-network") { Description = "Allow explicitly requested private or loopback targets." };
        var timeout = new Option<int>("--timeout") { DefaultValueFactory = _ => 300, Description = "Overall timeout in seconds." };
        var browser = new Option<bool>("--browser") { Description = "Use the browser renderer." };
        var headed = new Option<bool>("--headed") { Description = "Use headed browser mode where supported." };
        command.Options.Add(profile);
        command.Options.Add(pages);
        command.Options.Add(records);
        command.Options.Add(format);
        command.Options.Add(output);
        command.Options.Add(adaptersDir);
        command.Options.Add(proxyPool);
        command.Options.Add(allowPrivate);
        command.Options.Add(timeout);
        command.Options.Add(browser);
        command.Options.Add(headed);
        options = new CommonOptions(profile, pages, records, format, output, adaptersDir, proxyPool, allowPrivate, timeout, browser, headed);
    }

    private static async Task<int> RunScrapeAsync(
        ParseResult parseResult,
        CancellationToken cancellationToken,
        Argument<string> adapterArgument,
        Option<string[]> inputOption,
        CommonOptions options)
    {
        var registry = LoadRegistry(parseResult.GetValue(options.AdaptersDirectory));
        var profile = ParseProfile(parseResult.GetValue(options.Profile));
        var request = new ScrapeRequest
        {
            AdapterId = parseResult.GetRequiredValue(adapterArgument),
            Inputs = ParseInputs(parseResult.GetValue(inputOption) ?? Array.Empty<string>()),
            Profile = profile,
            MaxPages = Positive(parseResult.GetValue(options.Pages), "pages"),
            MaxRecords = Positive(parseResult.GetValue(options.Records), "records"),
            OverallTimeout = TimeSpan.FromSeconds(Positive(parseResult.GetValue(options.Timeout), "timeout")),
            ProxyPoolName = parseResult.GetValue(options.ProxyPool),
            AllowPrivateNetwork = parseResult.GetValue(options.AllowPrivateNetwork)
        };

        await using var fetcher = new BrowserFetcher();
        var effectiveAdapter = registry.Get(request.AdapterId);
        var renderMode = parseResult.GetValue(options.Browser) ? RenderMode.Browser : ParseRenderMode(effectiveAdapter.Render.Mode);
        var orchestrator = new ScrapeOrchestrator(registry, new RenderOverrideFetcher(fetcher, renderMode, parseResult.GetValue(options.Headed)));
        await StreamEventsAsync(orchestrator.RunAsync(request, cancellationToken), parseResult.GetValue(options.Format), parseResult.GetValue(options.Output), cancellationToken).ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> RunSearchAsync(
        ParseResult parseResult,
        CancellationToken cancellationToken,
        Argument<string> queryArgument,
        Option<string?> enginesOption,
        CommonOptions options)
    {
        var registry = LoadRegistry(parseResult.GetValue(options.AdaptersDirectory));
        var engineText = parseResult.GetValue(enginesOption);
        var engines = string.IsNullOrWhiteSpace(engineText)
            ? registry.All.Where(item => item.Kind.Equals("search", StringComparison.OrdinalIgnoreCase)).Select(item => item.Id).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToArray()
            : engineText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var profile = ParseProfile(parseResult.GetValue(options.Profile));
        var request = new SearchRequest
        {
            Query = parseResult.GetRequiredValue(queryArgument),
            EngineIds = engines,
            Profile = profile,
            MaxPages = Positive(parseResult.GetValue(options.Pages), "pages"),
            MaxResults = Positive(parseResult.GetValue(options.Records), "records"),
            OverallTimeout = TimeSpan.FromSeconds(Positive(parseResult.GetValue(options.Timeout), "timeout")),
            ProxyPoolName = parseResult.GetValue(options.ProxyPool),
            AllowPrivateNetwork = parseResult.GetValue(options.AllowPrivateNetwork),
            Options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };

        await using var fetcher = new BrowserFetcher();
        var browserMode = parseResult.GetValue(options.Browser);
        var orchestrator = new ScrapeOrchestrator(registry, new RenderOverrideFetcher(fetcher, browserMode ? RenderMode.Browser : null, parseResult.GetValue(options.Headed)));
        var service = new SearchService(orchestrator);
        await StreamSearchAsync(service.SearchAsync(request, cancellationToken), parseResult.GetValue(options.Format), parseResult.GetValue(options.Output), cancellationToken).ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> RunLoginAsync(ParseResult parseResult, CancellationToken cancellationToken, Argument<string> adapterArgument, Option<bool> headedOption, Option<string?> adaptersDirectoryOption)
    {
        var registry = LoadRegistry(parseResult.GetValue(adaptersDirectoryOption));
        var adapter = registry.Get(parseResult.GetRequiredValue(adapterArgument));
        if (adapter.Auth is null || string.IsNullOrWhiteSpace(adapter.Auth.LoginUrl))
        {
            throw new InvalidDataException($"Adapter '{adapter.Id}' has no configured login flow.");
        }

        var store = new EncryptedSecretStore();
        var storePath = SecretPath(null);
        var needsStore = adapter.Auth.SessionName is { Length: > 0 } || adapter.Auth.Steps.Any(step => step.SecretName is { Length: > 0 });
        var passphrase = needsStore ? ReadPassphrase("Store passphrase: ") : null;
        var values = needsStore && File.Exists(storePath)
            ? await store.ReadAsync(storePath, passphrase!, cancellationToken).ConfigureAwait(false)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sessionStore = adapter.Auth.SessionName is { Length: > 0 } ? new EncryptedSessionStore(storePath, store) : null;
        await using var fetcher = new BrowserFetcher(new ManualChallengeHandler());
        var response = await fetcher.FetchAsync(new FetchRequest
        {
            Uri = new Uri(adapter.Auth.LoginUrl),
            RenderMode = RenderMode.Browser,
            Auth = adapter.Auth,
            Headers = adapter.Request.Headers,
            Timeout = TimeSpan.FromMilliseconds(adapter.Request.TimeoutMs),
            Headed = parseResult.GetValue(headedOption),
            SecretResolver = needsStore ? new DictionarySecretResolver(values) : null,
            SessionStore = sessionStore,
            SessionPassphrase = passphrase
        }, cancellationToken).ConfigureAwait(false);
        Console.Error.WriteLine($"login flow completed with HTTP {response.StatusCode} at {response.FinalUri}");
        return 0;
    }

    private static Command BuildSecretsCommand()
    {
        var secrets = new Command("secrets", "Manage the local encrypted secret store.");
        var init = new Command("init", "Create an empty encrypted secret store.");
        var set = new Command("set", "Set a secret value without echoing it.");
        var list = new Command("list", "List secret names only.");
        var remove = new Command("remove", "Remove a secret.");
        var rotate = new Command("rotate", "Re-encrypt the store with a new passphrase.");

        var initFile = SecretsFileOption();
        var setFile = SecretsFileOption();
        var listFile = SecretsFileOption();
        var removeFile = SecretsFileOption();
        var rotateFile = SecretsFileOption();
        init.Options.Add(initFile);
        set.Options.Add(setFile);
        list.Options.Add(listFile);
        remove.Options.Add(removeFile);
        rotate.Options.Add(rotateFile);

        var nameArgument = new Argument<string>("name");
        set.Arguments.Add(nameArgument);
        var removeName = new Argument<string>("name");
        remove.Arguments.Add(removeName);

        init.SetAction(async parseResult =>
        {
            var path = SecretPath(parseResult.GetValue(initFile));
            var store = new EncryptedSecretStore();
            await store.WriteAsync(path, new Dictionary<string, string>(), ReadPassphrase("New secrets passphrase: "), CancellationToken.None).ConfigureAwait(false);
            Console.Error.WriteLine($"initialized {path}");
        });
        set.SetAction(async parseResult =>
        {
            var path = SecretPath(parseResult.GetValue(setFile));
            var store = new EncryptedSecretStore();
            var secretsMap = await ReadSecretsAsync(store, path).ConfigureAwait(false);
            secretsMap[parseResult.GetRequiredValue(nameArgument)] = ReadPassphrase("Secret value: ");
            await store.WriteAsync(path, secretsMap, ReadPassphrase("Store passphrase: "), CancellationToken.None).ConfigureAwait(false);
        });
        list.SetAction(async parseResult =>
        {
            var path = SecretPath(parseResult.GetValue(listFile));
            var secretsMap = await ReadSecretsAsync(new EncryptedSecretStore(), path).ConfigureAwait(false);
            foreach (var name in secretsMap.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine(name);
            }
        });
        remove.SetAction(async parseResult =>
        {
            var path = SecretPath(parseResult.GetValue(removeFile));
            var store = new EncryptedSecretStore();
            var secretsMap = await ReadSecretsAsync(store, path).ConfigureAwait(false);
            secretsMap.Remove(parseResult.GetRequiredValue(removeName));
            await store.WriteAsync(path, secretsMap, ReadPassphrase("Store passphrase: "), CancellationToken.None).ConfigureAwait(false);
        });
        rotate.SetAction(async parseResult =>
        {
            var path = SecretPath(parseResult.GetValue(rotateFile));
            var store = new EncryptedSecretStore();
            var secretsMap = await store.ReadAsync(path, ReadPassphrase("Current passphrase: ")).ConfigureAwait(false);
            await store.WriteAsync(path, secretsMap, ReadPassphrase("New passphrase: "), CancellationToken.None).ConfigureAwait(false);
        });

        secrets.Subcommands.Add(init);
        secrets.Subcommands.Add(set);
        secrets.Subcommands.Add(list);
        secrets.Subcommands.Add(remove);
        secrets.Subcommands.Add(rotate);
        return secrets;
    }

    private static async Task<int> RunDoctorAsync(bool browser, CancellationToken cancellationToken)
    {
        var schema = FindSchemaPath();
        var registry = LoadRegistry(null);
        Console.WriteLine($"runtime: {Environment.Version}");
        Console.WriteLine($"schema: {schema}");
        Console.WriteLine($"adapters: {registry.All.Count}");
        if (browser)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var playwright = await Microsoft.Playwright.Playwright.CreateAsync().ConfigureAwait(false);
            var executable = playwright.Chromium.ExecutablePath;
            Console.WriteLine($"playwright chromium: {(File.Exists(executable) ? "installed" : "missing")}");
            if (!File.Exists(executable))
            {
                return 5;
            }
        }

        return 0;
    }

    private static async Task StreamEventsAsync(IAsyncEnumerable<ScrapeEvent> events, string? format, string? output, CancellationToken cancellationToken)
    {
        await using var writer = await OutputWriter.OpenAsync(output, cancellationToken).ConfigureAwait(false);
        var csv = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase);
        if (!csv && !string.Equals(format, "jsonl", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("format must be jsonl or csv.");
        }

        await using var sink = csv ? null : new JsonLinesSink(writer.Writer);
        CsvSink? csvSink = csv ? new CsvSink(writer.Writer) : null;
        try
        {
            await foreach (var scrapeEvent in events.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (scrapeEvent.Kind == ScrapeEventKind.Diagnostic)
                {
                    await WriteDiagnosticAsync(scrapeEvent.Diagnostic!, cancellationToken).ConfigureAwait(false);
                }
                else if (scrapeEvent.Kind == ScrapeEventKind.RunCompleted)
                {
                    await WriteDiagnosticAsync(scrapeEvent.Summary!, cancellationToken).ConfigureAwait(false);
                }
                else if (csv)
                {
                    await csvSink!.WriteAsync(scrapeEvent, cancellationToken).ConfigureAwait(false);
                }
                else if (scrapeEvent.Kind == ScrapeEventKind.Record)
                {
                    await sink!.WriteAsync(scrapeEvent, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            if (csvSink is not null)
            {
                await csvSink.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task StreamSearchAsync(IAsyncEnumerable<SearchResult> results, string? format, string? output, CancellationToken cancellationToken)
    {
        if (!string.Equals(format, "jsonl", StringComparison.OrdinalIgnoreCase) && !string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("format must be jsonl or csv.");
        }

        await using var writer = await OutputWriter.OpenAsync(output, cancellationToken).ConfigureAwait(false);
        var csv = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase);
        var headerWritten = false;
        await foreach (var result in results.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (!csv)
            {
                await writer.Writer.WriteLineAsync(JsonSerializer.Serialize(result, JsonSerializerOptions.Web)).ConfigureAwait(false);
            }
            else
            {
                if (!headerWritten)
                {
                    await writer.Writer.WriteLineAsync("engineId,title,url,snippet,rank,page,retrievedAt").ConfigureAwait(false);
                    headerWritten = true;
                }

                var fields = new[] { result.EngineId, result.Title, result.Url.AbsoluteUri, result.Snippet ?? string.Empty, result.Rank.ToString(), result.Page.ToString(), result.RetrievedAt.ToString("O") };
                await writer.Writer.WriteLineAsync(string.Join(',', fields.Select(CsvEscape))).ConfigureAwait(false);
            }

            await writer.Writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task WriteDiagnosticAsync(object diagnostic, CancellationToken cancellationToken)
    {
        await Console.Error.WriteLineAsync(JsonSerializer.Serialize(diagnostic, JsonSerializerOptions.Web)).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static AdapterRegistry LoadRegistry(string? directory)
    {
        var adaptersDirectory = directory is null ? FindBuiltInAdapterDirectory() : Path.GetFullPath(directory);
        return AdapterRegistry.Load(new[] { adaptersDirectory }, FindSchemaPath());
    }

    private static string FindBuiltInAdapterDirectory()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "adapters", "builtin");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        var outputCandidate = Path.Combine(AppContext.BaseDirectory, "adapters", "builtin");
        if (Directory.Exists(outputCandidate))
        {
            return outputCandidate;
        }

        throw new DirectoryNotFoundException("Could not locate adapters/builtin. Use --adapters-dir.");
    }

    private static string FindSchemaPath()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "schemas", "adapter-v1.schema.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        var outputCandidate = Path.Combine(AppContext.BaseDirectory, "schemas", "adapter-v1.schema.json");
        return File.Exists(outputCandidate) ? outputCandidate : throw new FileNotFoundException("Could not locate schemas/adapter-v1.schema.json.");
    }

    private static Dictionary<string, string> ParseInputs(IEnumerable<string> values)
    {
        var inputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            var separator = value.IndexOf('=');
            if (separator <= 0)
            {
                throw new ArgumentException($"Input '{value}' must use key=value syntax.");
            }

            inputs[value[..separator]] = value[(separator + 1)..];
        }

        return inputs;
    }

    private static ExecutionProfile ParseProfile(string? value) => value?.ToLowerInvariant() switch
    {
        "local" => ExecutionProfile.Local,
        "high-volume" or "highvolume" => ExecutionProfile.HighVolume,
        _ => throw new ArgumentException("profile must be local or high-volume.")
    };

    private static RenderMode ParseRenderMode(string value) => value.ToLowerInvariant() switch
    {
        "browser" => RenderMode.Browser,
        "auto" => RenderMode.Auto,
        _ => RenderMode.Http
    };

    private static int Positive(int value, string name) => value > 0 ? value : throw new ArgumentException($"{name} must be positive.");

    private static Option<string?> AdaptersDirectoryOption() => new("--adapters-dir") { Description = "Directory containing YAML adapters." };

    private static Option<string?> SecretsFileOption() => new("--file") { Description = "Encrypted secrets file path." };

    private static string SecretPath(string? value) => Path.GetFullPath(value ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".magicbox", "secrets.json.enc"));

    private static async Task<Dictionary<string, string>> ReadSecretsAsync(EncryptedSecretStore store, string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Secrets file does not exist. Run 'magicbox secrets init' first.", path);
        }

        return await store.ReadAsync(path, ReadPassphrase("Store passphrase: ")).ConfigureAwait(false);
    }

    private static string ReadPassphrase(string prompt)
    {
        var environmentValue = Environment.GetEnvironmentVariable("MAGICBOX_SECRETS_PASSPHRASE");
        if (!string.IsNullOrEmpty(environmentValue))
        {
            Console.Error.WriteLine("warning: using MAGICBOX_SECRETS_PASSPHRASE; environment variables may be exposed.");
            return environmentValue;
        }

        Console.Error.Write(prompt);
        var buffer = new List<char>();
        ConsoleKeyInfo key;
        while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
        {
            if (key.Key == ConsoleKey.Backspace && buffer.Count > 0)
            {
                buffer.RemoveAt(buffer.Count - 1);
            }
            else if (!char.IsControl(key.KeyChar))
            {
                buffer.Add(key.KeyChar);
            }
        }

        Console.Error.WriteLine();
        return new string(buffer.ToArray());
    }

    private static string CsvEscape(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private sealed record CommonOptions(
        Option<string> Profile,
        Option<int> Pages,
        Option<int> Records,
        Option<string> Format,
        Option<string?> Output,
        Option<string?> AdaptersDirectory,
        Option<string?> ProxyPool,
        Option<bool> AllowPrivateNetwork,
        Option<int> Timeout,
        Option<bool> Browser,
        Option<bool> Headed);

    private sealed class OutputWriter : IAsyncDisposable
    {
        public TextWriter Writer { get; }
        private readonly bool ownsWriter;

        private OutputWriter(TextWriter writer, bool ownsWriter)
        {
            Writer = writer;
            this.ownsWriter = ownsWriter;
        }

        public static Task<OutputWriter> OpenAsync(string? path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(path))
            {
                return Task.FromResult(new OutputWriter(Console.Out, false));
            }

            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            return Task.FromResult(new OutputWriter(new StreamWriter(new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read), Console.OutputEncoding), true));
        }

        public async ValueTask DisposeAsync()
        {
            await Writer.FlushAsync().ConfigureAwait(false);
            if (ownsWriter)
            {
                await Writer.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private sealed class RenderOverrideFetcher : IFetcher
    {
        private readonly IFetcher inner;
        private readonly RenderMode? overrideMode;
        private readonly bool headed;

        public RenderOverrideFetcher(IFetcher inner, RenderMode? overrideMode, bool headed)
        {
            this.inner = inner;
            this.overrideMode = overrideMode;
            this.headed = headed;
        }

        public Task<FetchResponse> FetchAsync(FetchRequest request, CancellationToken cancellationToken) =>
            inner.FetchAsync(request with
            {
                RenderMode = overrideMode ?? request.RenderMode,
                Headed = headed || request.Headed
            }, cancellationToken);
    }
}
