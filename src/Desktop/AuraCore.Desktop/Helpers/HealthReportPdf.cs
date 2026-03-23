using AuraCore.Desktop.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AuraCore.Desktop.Helpers;

/// <summary>
/// Generates a PDF system health report from scan data collected by SystemHealthPage.
/// </summary>
public static class HealthReportPdf
{
    public sealed record HealthData
    {
        public string OsName { get; init; } = "";
        public string OsVersion { get; init; } = "";
        public string OsArch { get; init; } = "";
        public string MachineName { get; init; } = "";
        public string Uptime { get; init; } = "";

        public string CpuName { get; init; } = "";
        public string CpuCores { get; init; } = "";
        public int CpuLoad { get; init; }

        public string MemTotal { get; init; } = "";
        public string MemAvail { get; init; } = "";
        public int MemUsagePct { get; init; }

        public List<DriveData> Drives { get; init; } = new();
        public int ProcessCount { get; init; }
        public string TopProcesses { get; init; } = "";

        public List<GpuData> Gpus { get; init; } = new();
        public string BatteryInfo { get; init; } = "";
        public int BatteryPct { get; init; }

        public List<StartupData> StartupPrograms { get; init; } = new();
    }

    public sealed record DriveData(string Name, double TotalGb, double FreeGb, int UsedPct);
    public sealed record GpuData(string Name, string Vram, string Driver);
    public sealed record StartupData(string Name, string Command, string Hive, string Impact);

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
                        row.RelativeItem().Text(S._("pdf.title"))
                            .FontSize(22).Bold().FontColor(Colors.Blue.Darken2);
                        row.ConstantItem(200).AlignRight().Text(S._("pdf.reportTitle"))
                            .FontSize(12).FontColor(Colors.Grey.Darken1);
                    });
                    col.Item().PaddingTop(4).Text($"{S._("pdf.generated")}: {DateTime.Now:yyyy-MM-dd HH:mm:ss}  |  {S._("pdf.machine")}: {data.MachineName}")
                        .FontSize(9).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Blue.Darken2);
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    // OS Section
                    AddSection(col, S._("pdf.os"), stack =>
                    {
                        AddRow(stack, "OS", data.OsName);
                        AddRow(stack, S._("pdf.version"), data.OsVersion);
                        AddRow(stack, S._("pdf.architecture"), data.OsArch);
                        AddRow(stack, S._("pdf.machine"), data.MachineName);
                        AddRow(stack, S._("pdf.uptime"), data.Uptime);
                    });

                    // CPU Section
                    AddSection(col, S._("health.processor"), stack =>
                    {
                        AddRow(stack, S._("pdf.cpu"), data.CpuName);
                        AddRow(stack, S._("pdf.cores"), data.CpuCores);
                        AddRow(stack, S._("pdf.load"), $"{data.CpuLoad}%");
                        AddBar(stack, data.CpuLoad);
                    });

                    // Memory Section
                    AddSection(col, S._("health.memory"), stack =>
                    {
                        AddRow(stack, S._("pdf.total"), data.MemTotal);
                        AddRow(stack, S._("pdf.availableMem"), data.MemAvail);
                        AddRow(stack, S._("pdf.usage"), $"{data.MemUsagePct}%");
                        AddBar(stack, data.MemUsagePct);
                    });

                    // Drives
                    AddSection(col, S._("pdf.drives"), stack =>
                    {
                        foreach (var drive in data.Drives)
                        {
                            stack.Item().PaddingBottom(6).Column(driveCol =>
                            {
                                driveCol.Item().Text(string.Format(S._("pdf.freeOf"), $"{drive.FreeGb:F1}", $"{drive.TotalGb:F1}", drive.UsedPct))
                                    .FontSize(10);
                                AddBar(driveCol, drive.UsedPct);
                            });
                        }
                    });

                    // Processes
                    AddSection(col, S._("pdf.processes"), stack =>
                    {
                        AddRow(stack, S._("pdf.running"), $"{data.ProcessCount} processes");
                        AddRow(stack, S._("pdf.topMemory"), data.TopProcesses);
                    });

                    // GPU
                    if (data.Gpus.Count > 0)
                    {
                        AddSection(col, S._("pdf.graphics"), stack =>
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

                    // Battery
                    if (!string.IsNullOrEmpty(data.BatteryInfo))
                    {
                        AddSection(col, S._("pdf.battery"), stack =>
                        {
                            AddRow(stack, S._("pdf.status"), data.BatteryInfo);
                            if (data.BatteryPct > 0) AddBar(stack, data.BatteryPct);
                        });
                    }

                    // Startup
                    if (data.StartupPrograms.Count > 0)
                    {
                        AddSection(col, string.Format(S._("pdf.startupPrograms"), data.StartupPrograms.Count), stack =>
                        {
                            foreach (var prog in data.StartupPrograms)
                            {
                                stack.Item().PaddingBottom(3).Row(row =>
                                {
                                    row.RelativeItem(3).Text(prog.Name).FontSize(9);
                                    row.RelativeItem(1).Text(prog.Hive).FontSize(9).FontColor(Colors.Grey.Darken1);
                                    row.ConstantItem(60).AlignRight().Text(prog.Impact).FontSize(9)
                                        .FontColor(prog.Impact == "Low" ? Colors.Green.Darken2 : Colors.Orange.Darken2);
                                });
                            }
                        });
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Aura Core Pro — ").FontSize(8).FontColor(Colors.Grey.Medium);
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
        var barColor = pct > 85 ? Colors.Red.Darken1 : pct > 60 ? Colors.Orange.Darken1 : Colors.Green.Darken1;
        col.Item().PaddingTop(2).Height(6).Background(Colors.Grey.Lighten3)
            .Layers(layers =>
            {
                layers.PrimaryLayer().Height(6).Background(Colors.Grey.Lighten3);
                layers.Layer().Width(pct * 4.5f).Height(6).Background(barColor);
            });
    }
}
