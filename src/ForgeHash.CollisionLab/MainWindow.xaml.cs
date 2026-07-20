using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ForgeHash.Analysis;
using ForgeHashX;
using Microsoft.Win32;
using ForgeHashParameters = global::ForgeHash.ForgeHashParameters;

namespace Forgeh.CollisionLab;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<string> _log = [];
    private CancellationTokenSource? _cts;
    private CollisionCampaignResult? _lastResult;
    private bool _suppressPresetEvent;
    private bool _suppressAlgorithmEvent;

    private enum LabAlgorithm
    {
        B3,
        X,
    }

    private static readonly (string Label, CollisionCampaignKind Kind)[] Campaigns =
    [
        ("Distinct passwords (fixed salt)", CollisionCampaignKind.DistinctPasswords),
        ("Distinct salts (fixed password)", CollisionCampaignKind.DistinctSalts),
        ("Random password + salt pairs", CollisionCampaignKind.RandomPairs),
        ("Nearby password bit-flips", CollisionCampaignKind.NearbyPasswordBitFlips),
        ("Distinct parameter sets", CollisionCampaignKind.DistinctParameterSets),
        ("Truncated 16-byte outputs", CollisionCampaignKind.TruncatedOutputs),
    ];

    public MainWindow()
    {
        InitializeComponent();
        LogList.ItemsSource = _log;

        _suppressAlgorithmEvent = true;
        AlgorithmCombo.Items.Add("ForgeHash-B3 (forgeh)");
        AlgorithmCombo.Items.Add("ForgeHash-X (forgehx) — experimental");
        AlgorithmCombo.SelectedIndex = 0;
        _suppressAlgorithmEvent = false;

        foreach ((string label, _) in Campaigns)
        {
            CampaignCombo.Items.Add(label);
        }

        CampaignCombo.SelectedIndex = 2; // random pairs — mass-hunt default

        RebuildPresetsForAlgorithm(LabAlgorithm.B3);
        AppendLog("Ready. Pick algorithm + campaign and press Start.");
    }

    private LabAlgorithm SelectedAlgorithm =>
        AlgorithmCombo.SelectedIndex == 1 ? LabAlgorithm.X : LabAlgorithm.B3;

    private void AlgorithmCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressAlgorithmEvent || !IsLoaded)
        {
            return;
        }

        RebuildPresetsForAlgorithm(SelectedAlgorithm);
        AppendLog(SelectedAlgorithm == LabAlgorithm.X
            ? "Switched to ForgeHash-X (experimental sandbox)."
            : "Switched to ForgeHash-B3.");
    }

    private void RebuildPresetsForAlgorithm(LabAlgorithm algorithm)
    {
        _suppressPresetEvent = true;
        PresetCombo.Items.Clear();
        if (algorithm == LabAlgorithm.X)
        {
            PresetCombo.Items.Add("Toy");
            PresetCombo.Items.Add("Custom");
            PresetCombo.SelectedIndex = 0;
            _suppressPresetEvent = false;
            ApplyPreset("Toy");
            WorkersBox.Text = CollisionCampaign.SuggestDegreeOfParallelism(ForgeHashXParameters.Toy.MemoryKiB).ToString();
        }
        else
        {
            PresetCombo.Items.Add("Development");
            PresetCombo.Items.Add("Interactive");
            PresetCombo.Items.Add("Custom");
            PresetCombo.SelectedIndex = 0;
            _suppressPresetEvent = false;
            ApplyPreset("Development");
            WorkersBox.Text = CollisionCampaign.SuggestDegreeOfParallelism(ForgeHashParameters.Development).ToString();
        }
    }

    private void PresetCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressPresetEvent || PresetCombo.SelectedItem is not string name)
        {
            return;
        }

        if (name is "Development" or "Interactive" or "Toy")
        {
            ApplyPreset(name);
            int memory = name switch
            {
                "Interactive" => ForgeHashParameters.Interactive.MemoryKiB,
                "Toy" => ForgeHashXParameters.Toy.MemoryKiB,
                _ => ForgeHashParameters.Development.MemoryKiB,
            };
            WorkersBox.Text = CollisionCampaign.SuggestDegreeOfParallelism(memory).ToString();
        }
    }

    private void ApplyPreset(string name)
    {
        if (name == "Toy")
        {
            ForgeHashXParameters p = ForgeHashXParameters.Toy;
            MemoryBox.Text = p.MemoryKiB.ToString();
            IterationsBox.Text = p.Iterations.ToString();
            ParallelismBox.Text = p.Parallelism.ToString();
            OutputLengthBox.Text = p.OutputLength.ToString();
            UpdateCostWarning(p.MemoryKiB, LabAlgorithm.X);
            return;
        }

        ForgeHashParameters b3 = name switch
        {
            "Interactive" => ForgeHashParameters.Interactive,
            _ => ForgeHashParameters.Development,
        };
        MemoryBox.Text = b3.MemoryKiB.ToString();
        IterationsBox.Text = b3.Iterations.ToString();
        ParallelismBox.Text = b3.Parallelism.ToString();
        OutputLengthBox.Text = b3.OutputLength.ToString();
        UpdateCostWarning(b3.MemoryKiB, LabAlgorithm.B3);
    }

    private void UpdateCostWarning(int memoryKiB, LabAlgorithm algorithm)
    {
        if (algorithm == LabAlgorithm.X)
        {
            WarningBanner.Text = memoryKiB > ForgeHashXParameters.Toy.MemoryKiB
                ? $"Warning: X memory is {memoryKiB} KiB. Prefer Toy (1024 KiB) for mass runs."
                : "Tip: ForgeHash-X Toy (1 MiB) is for sandbox hunts only — not production, not B3-compatible.";
            return;
        }

        if (memoryKiB > ForgeHashParameters.MinimumMemoryKiB)
        {
            WarningBanner.Text =
                $"Warning: memory is {memoryKiB} KiB. Mass campaigns will be very slow. Prefer Development (8192 KiB) unless you mean it.";
        }
        else
        {
            WarningBanner.Text =
                "Tip: use the Development profile (8 MiB) for mass runs. Larger profiles are extremely slow.";
        }
    }

    private async void StartButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_cts is not null)
        {
            return;
        }

        if (!TryReadInputs(
                out LabAlgorithm algorithm,
                out CollisionCampaignKind kind,
                out int samples,
                out CollisionCostSnapshot cost,
                out ICollisionHasher hasher,
                out int? rngSeed,
                out int workers,
                out string error))
        {
            MessageBox.Show(this, error, "Invalid settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        UpdateCostWarning(cost.MemoryKiB, algorithm);
        long approxMiB = (long)cost.MemoryKiB * workers / 1024;
        int slowThreshold = algorithm == LabAlgorithm.X ? 1024 : 8192;
        if (cost.MemoryKiB > slowThreshold && samples >= 100)
        {
            MessageBoxResult confirm = MessageBox.Show(
                this,
                $"Running {samples} samples at {cost.MemoryKiB} KiB will take a long time.\n\nContinue?",
                "Slow campaign",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }
        }
        else if (approxMiB >= 1024)
        {
            MessageBoxResult confirm = MessageBox.Show(
                this,
                $"Workers × memory ≈ {approxMiB} MiB in-flight.\n\nContinue?",
                "High memory campaign",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }
        }

        _cts = new CancellationTokenSource();
        _lastResult = null;
        SetRunning(true);
        AppendLog(
            $"Starting {algorithm} · {kind} · N={samples} · workers={workers} · {cost.Summary}");

        var progress = new Progress<CollisionProgress>(OnProgress);
        CancellationToken token = _cts.Token;
        bool stopOnFirst = StopOnFirstCheck.IsChecked == true;

        try
        {
            CollisionCampaignResult result = await Task.Run(
                () => CollisionCampaign.Run(
                    kind,
                    samples,
                    cost,
                    hasher,
                    stopOnFirstCollision: stopOnFirst,
                    rngSeed: rngSeed,
                    maxDegreeOfParallelism: workers,
                    progress: progress,
                    cancellationToken: token),
                token);

            _lastResult = result;
            OnProgress(new CollisionProgress(
                result.CompletedSamples,
                result.RequestedSamples,
                result.CollisionCount,
                result.HashesPerSecond,
                result.Elapsed,
                null,
                result.StopReason.ToString()));

            foreach (CollisionHit hit in result.Collisions)
            {
                AppendLog(
                    $"COLLISION [{hit.Channel}] sample {hit.SampleIndex} == prior {hit.PriorSampleIndex}: {hit.DigestHex}",
                    isHit: true);
            }

            AppendLog(
                $"Done · {result.StopReason} · algo={result.Cost.Algorithm} · collisions={result.CollisionCount} · unique={result.UniqueDigests} · workers={result.DegreeOfParallelism} · {result.HashesPerSecond:F2} H/s · {result.Elapsed.TotalSeconds:F1}s");
            StatusText.Text = result.CollisionCount == 0
                ? "Finished with no collisions in this sample set."
                : $"Finished with {result.CollisionCount} collision(s).";
            ExportJsonButton.IsEnabled = true;
            ExportCsvButton.IsEnabled = true;
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Cancelled.";
            AppendLog("Cancelled.");
        }
        catch (Exception ex)
        {
            StatusText.Text = "Failed.";
            AppendLog("Error: " + ex.Message, isHit: true);
            MessageBox.Show(this, ex.Message, "Campaign failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            SetRunning(false);
        }
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        StatusText.Text = "Cancelling…";
    }

    private void ClearButton_OnClick(object sender, RoutedEventArgs e)
    {
        _log.Clear();
        _lastResult = null;
        ExportJsonButton.IsEnabled = false;
        ExportCsvButton.IsEnabled = false;
        RunProgress.Value = 0;
        CompletedText.Text = "0 / 0";
        RateText.Text = "—";
        ElapsedText.Text = "0.0s";
        CollisionsText.Text = "0";
        StatusText.Text = "Idle.";
        AppendLog("Cleared.");
    }

    private void ExportJsonButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_lastResult is null)
        {
            return;
        }

        string algo = _lastResult.Cost.Algorithm;
        var dialog = new SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json",
            FileName = $"{algo}-collision-{_lastResult.Kind}-{DateTime.Now:yyyyMMdd-HHmmss}.json",
        };
        if (dialog.ShowDialog(this) == true)
        {
            File.WriteAllText(dialog.FileName, _lastResult.ToJson());
            AppendLog("Wrote " + dialog.FileName);
        }
    }

    private void ExportCsvButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_lastResult is null)
        {
            return;
        }

        string algo = _lastResult.Cost.Algorithm;
        var dialog = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"{algo}-collision-{_lastResult.Kind}-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
        };
        if (dialog.ShowDialog(this) == true)
        {
            File.WriteAllText(dialog.FileName, _lastResult.ToCsv());
            AppendLog("Wrote " + dialog.FileName);
        }
    }

    private void OnProgress(CollisionProgress p)
    {
        double pct = p.Total <= 0 ? 0 : 100.0 * p.Completed / p.Total;
        RunProgress.Value = Math.Clamp(pct, 0, 100);
        CompletedText.Text = $"{p.Completed} / {p.Total}";
        RateText.Text = p.HashesPerSecond.ToString("F2");
        ElapsedText.Text = $"{p.Elapsed.TotalSeconds:F1}s";
        CollisionsText.Text = p.CollisionCount.ToString();
        StatusText.Text = p.LatestMessage ?? $"Running… last={Truncate(p.LastDigestHex, 16)}";
    }

    private bool TryReadInputs(
        out LabAlgorithm algorithm,
        out CollisionCampaignKind kind,
        out int samples,
        out CollisionCostSnapshot cost,
        out ICollisionHasher hasher,
        out int? rngSeed,
        out int workers,
        out string error)
    {
        algorithm = SelectedAlgorithm;
        kind = default;
        samples = 0;
        cost = CollisionCostSnapshot.FromB3(ForgeHashParameters.Development);
        hasher = B3CollisionHasher.Instance;
        rngSeed = null;
        workers = 1;
        error = string.Empty;

        int idx = CampaignCombo.SelectedIndex;
        if (idx < 0 || idx >= Campaigns.Length)
        {
            error = "Select a campaign.";
            return false;
        }

        kind = Campaigns[idx].Kind;

        if (kind == CollisionCampaignKind.DistinctParameterSets)
        {
            samples = 6;
        }
        else if (!int.TryParse(SampleCountBox.Text.Trim(), out samples) || samples < 1)
        {
            error = "Samples (N) must be a positive integer.";
            return false;
        }

        if (!int.TryParse(MemoryBox.Text.Trim(), out int memory) ||
            !int.TryParse(IterationsBox.Text.Trim(), out int iterations) ||
            !int.TryParse(ParallelismBox.Text.Trim(), out int parallelism) ||
            !int.TryParse(OutputLengthBox.Text.Trim(), out int outputLength))
        {
            error = "Memory, iterations, parallelism, and output length must be integers.";
            return false;
        }

        if (!int.TryParse(WorkersBox.Text.Trim(), out workers) || workers < 1 || workers > 64)
        {
            error = "Workers must be an integer from 1 to 64.";
            return false;
        }

        if (algorithm == LabAlgorithm.X)
        {
            hasher = XCollisionHasher.Instance;
            var xParams = new ForgeHashXParameters
            {
                MemoryKiB = memory,
                Iterations = iterations,
                Parallelism = parallelism,
                OutputLength = outputLength,
                SaltLength = 16,
            };
            try
            {
                xParams.Validate();
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            cost = CollisionCostSnapshot.FromX(xParams);
        }
        else
        {
            hasher = B3CollisionHasher.Instance;
            cost = CollisionCostSnapshot.FromB3(new ForgeHashParameters
            {
                MemoryKiB = memory,
                Iterations = iterations,
                Parallelism = parallelism,
                OutputLength = outputLength,
                SaltLength = 16,
            });
        }

        string seedText = RngSeedBox.Text.Trim();
        if (seedText.Length > 0)
        {
            if (!int.TryParse(seedText, out int seed))
            {
                error = "RNG seed must be an integer or blank.";
                return false;
            }

            rngSeed = seed;
        }

        if (PresetCombo.SelectedItem is string preset &&
            preset != "Custom" &&
            !MatchesPreset(preset, cost))
        {
            _suppressPresetEvent = true;
            PresetCombo.SelectedItem = "Custom";
            _suppressPresetEvent = false;
        }

        return true;
    }

    private static bool MatchesPreset(string name, CollisionCostSnapshot cost)
    {
        CollisionCostSnapshot expected = name switch
        {
            "Toy" => CollisionCostSnapshot.FromX(ForgeHashXParameters.Toy),
            "Interactive" => CollisionCostSnapshot.FromB3(ForgeHashParameters.Interactive),
            _ => CollisionCostSnapshot.FromB3(ForgeHashParameters.Development),
        };
        return cost.MemoryKiB == expected.MemoryKiB
               && cost.Iterations == expected.Iterations
               && cost.Parallelism == expected.Parallelism
               && cost.OutputLength == expected.OutputLength;
    }

    private void SetRunning(bool running)
    {
        StartButton.IsEnabled = !running;
        CancelButton.IsEnabled = running;
        AlgorithmCombo.IsEnabled = !running;
        CampaignCombo.IsEnabled = !running;
        SampleCountBox.IsEnabled = !running;
        PresetCombo.IsEnabled = !running;
        MemoryBox.IsEnabled = !running;
        IterationsBox.IsEnabled = !running;
        ParallelismBox.IsEnabled = !running;
        OutputLengthBox.IsEnabled = !running;
        RngSeedBox.IsEnabled = !running;
        WorkersBox.IsEnabled = !running;
        StopOnFirstCheck.IsEnabled = !running;
    }

    private void AppendLog(string line, bool isHit = false)
    {
        string stamped = $"[{DateTime.Now:HH:mm:ss}] {(isHit ? "!! " : "")}{line}";
        _log.Add(stamped);
        if (_log.Count > 5000)
        {
            _log.RemoveAt(0);
        }

        if (LogList.Items.Count > 0)
        {
            LogList.ScrollIntoView(LogList.Items[^1]);
        }

        if (isHit)
        {
            CollisionsText.Foreground = (Brush)FindResource("HitBrush");
        }
    }

    private static string Truncate(string? value, int chars)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "—";
        }

        return value.Length <= chars ? value : value[..chars] + "…";
    }
}
