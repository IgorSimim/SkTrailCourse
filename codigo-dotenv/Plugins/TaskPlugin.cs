using System.ComponentModel;
using Microsoft.SemanticKernel;
using SkOfflineCourse.Infra;

namespace SkOfflineCourse.Plugins;

public class TaskPlugin
{
    private readonly JsonMemoryStore _store;
    private readonly Kernel _kernel;
    private const string Key = "tasks";

    public TaskPlugin(JsonMemoryStore store, Kernel kernel)
    {
        _store = store;
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
    }

    public record TaskItem(string Title, bool Done);

    [KernelFunction, Description("Adiciona uma tarefa")]
    public async Task<string> AddTask([Description("Título da tarefa")] string title)
    {
        var list = await _store.LoadListAsync<TaskItem>(Key);
        list.Add(new TaskItem(title, false));
        await _store.SaveListAsync(Key, list);
        return $"✅ Tarefa adicionada: {title}";
    }

    [KernelFunction, Description("Lista as tarefas")]
    public async Task<string> ListTasks()
    {
        var list = await _store.LoadListAsync<TaskItem>(Key);
        if (list.Count == 0) return "📭 Sem tarefas.";
        return string.Join(Environment.NewLine,
            list.Select((t, i) => $"{i+1}. [{(t.Done ? 'x' : ' ')}] {t.Title}"));
    }

    [KernelFunction, Description("Conclui tarefa pelo índice (1-based)")]
    public async Task<string> CompleteTask([Description("Índice da tarefa (1-based)")] int index)
    {
        var list = await _store.LoadListAsync<TaskItem>(Key);
        if (index < 1 || index > list.Count) return "❌ Índice inválido.";
        var item = list[index - 1];
        list[index - 1] = item with { Done = true };
        await _store.SaveListAsync(Key, list);
        return $"🎉 Concluída: {item.Title}";
    }

    [KernelFunction, Description("Sugere próxima tarefa usando IA")]
    public async Task<string> RecommendNext()
    {
        var list = await _store.LoadListAsync<TaskItem>(Key);
        
        // Se não houver tarefas pendentes
        var pendingTasks = list.Where(t => !t.Done).ToList();
        if (pendingTasks.Count == 0) return "😎 Tudo em dia!";
        
        // Versão com IA
        var allTasksText = string.Join("\n", list.Select((t, i) => 
            $"{i+1}. [{(t.Done ? "CONCLUÍDA" : "PENDENTE")}] {t.Title}"));
        
        var prompt = $@"
Com base na lista de tarefas abaixo, recomende qual seria a mais importante a ser feita em seguida.
Considere fatores como: tarefas já concluídas, prioridades implícitas, e dependências lógicas.
Escolha apenas entre as tarefas PENDENTES.

LISTA DE TAREFAS:
{allTasksText}

Forneça sua recomendação com o seguinte formato:
'Recomendo a tarefa X: [título da tarefa]' (onde X é o número da tarefa)
";

        var result = await _kernel.InvokePromptAsync(prompt);
        var recommendation = result.ToString().Trim();
        
        return $"🤖 {recommendation}";
    }
}
