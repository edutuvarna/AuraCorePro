using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AuraCore.UI.Avalonia.Helpers;

/// <summary>
/// Generates a PDF system health report from scan data collected by SystemHealthView.
/// Self-contained version for the Avalonia UI project.
/// </summary>
public static class HealthReportPdfExporter
{
    public sealed record HealthData
    {
        public int HealthScore { get; init; }
        public string OsName { get; init; } = "";
        public string OsVersion { get; init; } = "";
        public string OsArch { get; init; } = "";
        public string MachineName { get; init; } = "";
        public string Uptime { get; init; } = "";

        public string CpuName { get; init; } = "";
        public string CpuCores { get; init; } = "";

        public string MemUsed { get; init; } = "";
        public string MemTotal { get; init; } = "";
        public int MemUsagePct { get; init; }

        public List<DriveData> Drives { get; init; } = new();
        public int ProcessCount { get; init; }
        public List<GpuData> Gpus { get; init; } = new();
    }

    public sealed record DriveData(string Name, double TotalGb, double FreeGb, int UsedPct);
    public sealed record GpuData(string Name, string Vram, string Driver);

    public static void Generate(HealthData data, string outputPath)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Aura Core Pro")
                            .FontSize(22).Bold().FontColor(Colors.Blue.Darken2);
                        row.ConstantItem(200).AlignRight().Text("System Health Report")
                            .FontSize(12).FontColor(Colors.Grey.Darken1);
                    });
                    col.Item().PaddingTop(4)
                        .Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}  |  Machine: {data.MachineName}")
                        .FontSize(9).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Blue.Darken2);
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    // Health Score
                    AddSection(col, "Health Score", stack =>
                    {
                        var (label, color) = data.HealthScore switch
                        {
                            >= 85 => ("Excellent", Colors.Green.Darken1),
                            >= 70 => ("Good", Colors.Teal.Darken1),
                            >= 50 => ("Fair", Colors.Orange.Darken1),
                            _     => ("Needs Attention", Colors.Red.Darken1)
                        };
                        AddRow(stack, "Score", $"{data.HealthScore} / 100");
                        AddRow(stack, "Status", label);
                        AddBar(stack, data.HealthScore);
                    });

                    // OS
                    AddSection(col, "Operating System", stack =>
                    {
                        AddRow(stack, "OS", data.OsName);
                        AddRow(stack, "Version", data.OsVersion);
                        AddRow(stack, "Architecture", data.OsArch);
                        AddRow(stack, "Machine", data.MachineName);
                        AddRow(stack, "Uptime", data.Uptime);
                    });

                    // CPU
                    AddSection(col, "Processor", stack =>
                    {
                        AddRow(stack, "CPU", data.CpuName);
                        AddRow(stack, "Cores", data.CpuCores);
                    });

                    // Memory
                    AddSection(col, "Memory", stack =>
                    {
                        AddRow(stack, "Used", data.MemUsed);
                        AddRow(stack, "Total", data.MemTotal);
                        AddRow(stack, "Usage", $"{data.MemUsagePct}%");
                        AddBar(stack, data.MemUsagePct);
                    });

                    // Drives
                    if (data.Drives.Count > 0)
                    {
                        AddSection(col, "Storage Drives", stack =>
                        {
                            foreach (var drive in data.Drives)
                            {
                                stack.Item().PaddingBottom(6).Column(driveCol =>
                                {
                                    driveCol.Item()
                                        .Text($"{drive.Name}  —  {drive.FreeGb:F1} GB free of {drive.TotalGb:F1} GB ({drive.UsedPct}% used)")
                                        .FontSize(10);
                                    AddBar(driveCol, drive.UsedPct);
                                });
                            }
                        });
                    }

                    // Processes
                    AddSection(col, "Processes", stack =>
                    {
                        AddRow(stack, "Running", $"{data.ProcessCount} processes");
                    });

                    // GPU
                    if (data.Gpus.Count > 0)
                    {
                        AddSection(col, "Graphics", stack =>
                        {
                            foreach (var gpu in data.Gpus)
                            {
                                stack.Item().PaddingBottom(4).Column(gpuCol =>
                                {
                                    gpuCol.Item().Text(gpu.Name).Bold().FontSize(10);
                                    gpuCol.Item().Text($"VRAM: {gpu.Vram}  |  Driver: {gpu.Driver}")
                                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                                });
                            }
                        });
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Aura Core Pro  —  ").FontSize(8).FontColor(Colors.Grey.Medium);
                    text.Span("https://auracore.pro").FontSize(8).FontColor(Colors.Blue.Darken1);
                });
            });
        }).GeneratePdf(outputPath);
    }

    private static void AddSection(ColumnDescriptor col, string title, Action<ColumnDescriptor> content)
    {
        col.Item().PaddingTop(10).Column(section =>
        {
            section.Item().PaddingBottom(6).Text(title).FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
            content(section);
        });
    }

    private static void AddRow(ColumnDescriptor col, string label, string value)
    {
        col.Item().PaddingBottom(2).Row(row =>
        {
            row.ConstantItem(120).Text(label).FontSize(10).FontColor(Colors.Grey.Darken2);
            row.RelativeItem().Text(value).FontSize(10);
        });
    }

    private static void AddBar(ColumnDescriptor col, int pct)
    {
        var barColor = pct > 85 ? Colors.Red.Darken1
                     : pct > 60 ? Colors.Orange.Darken1
                     : Colors.Green.Darken1;
        col.Item().PaddingTop(2).Height(6).Background(Colors.Grey.Lighten3)
            .Layers(layers =>
            {
                layers.PrimaryLayer().Height(6).Background(Colors.Grey.Lighten3);
                layers.Layer().Width(pct * 4.5f).Height(6).Background(barColor);
            });
    }
}
