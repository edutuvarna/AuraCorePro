namespace AuraCore.Module.CronManager.Models;

public sealed record CronJobInfo(
    string Source,          // "user" | "/etc/crontab" | "/etc/cron.d/xxx" | "/etc/cron.daily/xxx"
    int LineNumber,         // 1-based line number in source (0 for directory-based periodic)
    string Schedule,        // e.g. "0 5 * * *", "@daily", or "periodic:daily"
    string Command,         // the command/script to run
    bool IsValid,           // cron syntax valid
    bool CommandExists,     // first binary/script in command exists
    string? Issue);         // description of problem, null if healthy
