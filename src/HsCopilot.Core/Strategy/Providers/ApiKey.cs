namespace HsCopilot.Core.Strategy.Providers;

/// <summary>
/// Lecture d'une clé d'API : variable du processus, sinon celle enregistrée pour ton compte Windows (setx). setx n'atteint
/// pas les fenêtres déjà ouvertes : sans ce repli, une application lancée depuis l'une d'elles ne voyait pas une clé
/// fraîchement enregistrée (26/10/2026, ANTHROPIC_API_KEY). La clé n'est jamais écrite ni affichée en entier.
/// </summary>
public static class ApiKey
{
    public static string? Read(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (!string.IsNullOrWhiteSpace(value)) return value;
        if (!OperatingSystem.IsWindows()) return null;
        try { value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User); }
        catch (System.Security.SecurityException) { return null; }
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
