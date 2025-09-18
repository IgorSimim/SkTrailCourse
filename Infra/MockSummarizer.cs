namespace SkOfflineCourse.Infra;

/// <summary>
/// Mock configurável para testes: sempre retorna valor fixo ou padrão.
/// </summary>
public class MockSummarizer : ISummarizer
{
    public string Fixed { get; set; } = "RESUMO_MOCK";
    public string Summarize(string text, int maxChars = 120) => Fixed;
}
