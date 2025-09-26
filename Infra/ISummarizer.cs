namespace SkOfflineCourse.Infra;

/// <summary>
/// Abstração para criar resumos de textos. Permite mock em testes sem LLM.
/// </summary>
public interface ISummarizer
{
    string Summarize(string text, int maxChars = 120);
}
