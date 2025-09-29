using System.ComponentModel;
using Microsoft.SemanticKernel;
using SkOfflineCourse.Infra;

namespace SkOfflineCourse.Plugins;

public class TaskPlugin
{
    private readonly JsonMemoryStore _store;
    private const string Key = "tasks";

    public TaskPlugin(JsonMemoryStore store) => _store = store;

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

    [KernelFunction, Description("Sugere próxima tarefa (heurística simples)")]
    public async Task<string> RecommendNext()
    {
        var list = await _store.LoadListAsync<TaskItem>(Key);
        var next = list.FindIndex(t => !t.Done);
        if (next == -1) return "😎 Tudo em dia!";
        return $"➡️ Próxima tarefa sugerida: {next+1}. {list[next].Title}";
    }
}
