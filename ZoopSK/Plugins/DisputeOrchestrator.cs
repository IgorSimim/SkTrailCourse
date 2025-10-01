using Microsoft.SemanticKernel;
using SkTrailCourse.Infra;
using System.Threading.Tasks;

namespace SkTrailCourse.Plugins;

public class DisputeOrchestrator
{
    private readonly Kernel _kernel; 
    private readonly JsonMemoryStore _store;
    private readonly KernelFunction _analyzeFunction; 

    public DisputeOrchestrator(Kernel kernel, JsonMemoryStore store)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        
        // 1. Carregamento da função semântica
        try
        {
            // O nome do plugin que você quer criar: "AnalysisPlugin"
            const string PluginName = "AnalysisPlugin"; 
            
            var promptPlugin = _kernel.ImportPluginFromPromptDirectory(
                pluginDirectory: "Prompts",
                pluginName: PluginName
            );
            
            // 2. Obtém a função "Analysis" (nome da pasta dentro de Prompts)
            _analyzeFunction = promptPlugin["Analysis"];
            
            if (_analyzeFunction == null)
            {
                throw new InvalidOperationException("A função 'Analysis' não foi encontrada. Verifique se o arquivo skprompt.txt está na pasta 'Prompts/Analysis'.");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Erro ao carregar o plugin semântico de 'Prompts'.", ex);
        }
    }

    public async Task<string> AnalyzeAndResolveDispute(string customerText)
    {
        // O LLM usa a função _analyzeFunction 
        var result = await _kernel.InvokeAsync(
            _analyzeFunction, 
            new KernelArguments { ["input"] = customerText }
        );

        // O resultado já é o texto final humanizado
        return result.GetValue<string>() ?? "Desculpe, não foi possível concluir a análise. Verifique o log.";
    }
}