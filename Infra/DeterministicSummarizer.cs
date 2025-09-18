using System.Text;

namespace SkOfflineCourse.Infra;

/// <summary>
/// Implementação determinística simples: pega a primeira frase ou corta em maxChars.
/// Não usa LLM.
/// </summary>
public class DeterministicSummarizer : ISummarizer
{
    public string Summarize(string text, int maxChars = 120)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var dot = text.IndexOf('.', StringComparison.Ordinal);
        string candidate = dot > 0 ? text[..(dot+1)] : text;
        if (candidate.Length > maxChars)
        {
            candidate = candidate[..maxChars] + "...";
        }
        return candidate;
    }
}
