using System.Text;
using Microsoft.SemanticKernel;

namespace SkOfflineCourse.Infra;

/// <summary>
/// Implementação de ISummarizer usando um modelo de linguagem de IA
/// </summary>
public class AISummarizer : ISummarizer
{
    private readonly Kernel _kernel;

    public AISummarizer(Kernel kernel)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
    }

    public string Summarize(string text, int maxChars = 120)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        
        var prompt = new StringBuilder()
            .AppendLine("Você é um assistente especializado em criar resumos concisos.")
            .AppendLine("Resumir o seguinte texto em no máximo 120 caracteres, mantendo as informações mais importantes:")
            .AppendLine()
            .AppendLine(text)
            .ToString();

        var result = _kernel.InvokePromptAsync(prompt).GetAwaiter().GetResult();
        var summary = result.ToString().Trim();
        
        // Garantir que o resumo não ultrapassa o tamanho máximo
        if (summary.Length > maxChars)
        {
            summary = summary[..maxChars] + "...";
        }
        
        return summary;
    }
}