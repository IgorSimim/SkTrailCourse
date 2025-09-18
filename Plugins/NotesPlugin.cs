using System.ComponentModel;
using Microsoft.SemanticKernel;
using SkOfflineCourse.Infra;

namespace SkOfflineCourse.Plugins;

public class NotesPlugin
{
    private readonly JsonMemoryStore _store;
    private const string Key = "notes";

    public NotesPlugin(JsonMemoryStore store) => _store = store;

    public record Note(string Content, DateTime CreatedAt);

    [KernelFunction, Description("Adiciona uma nota")]
    public async Task<string> AddNote([Description("Conteúdo da nota")] string content)
    {
        var list = await _store.LoadListAsync<Note>(Key);
        list.Add(new Note(content, DateTime.Now));
        await _store.SaveListAsync(Key, list);
        return "📝 Nota salva.";
    }

    [KernelFunction, Description("Lista notas")]
    public async Task<string> ListNotes()
    {
        var list = await _store.LoadListAsync<Note>(Key);
        if (list.Count == 0) return "📭 Sem notas.";
        return string.Join(Environment.NewLine,
            list.Select((n, i) => $"{i+1}. {n.CreatedAt:dd/MM HH:mm} – {Trim(n.Content, 80)}"));
    }

    [KernelFunction, Description("Busca notas por termo (case-insensitive)")]
    public async Task<string> SearchNotes([Description("Termo de busca")] string term)
    {
        var list = await _store.LoadListAsync<Note>(Key);
        var q = term?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(q)) return "❌ Informe um termo.";
        var found = list.Select((n, i) => (note: n, idx: i+1))
                        .Where(x => x.note.Content.Contains(q, StringComparison.OrdinalIgnoreCase))
                        .ToList();
        if (found.Count == 0) return "🔎 Nenhuma nota encontrada.";
        return string.Join(Environment.NewLine,
            found.Select(x => $"{x.idx}. {Trim(x.note.Content, 80)}"));
    }

    [KernelFunction, Description("Resumo curto e determinístico da nota (sem LLM)")]
    public async Task<string> SummarizeNote([Description("Índice da nota (1-based)")] int index)
    {
        var list = await _store.LoadListAsync<Note>(Key);
        if (index < 1 || index > list.Count) return "❌ Índice inválido.";
        var text = list[index - 1].Content;

        // “Resumo” determinístico: pega 1ª frase ou até 120 chars + “...”
        var dot = text.IndexOf('.', StringComparison.Ordinal);
        string summary = dot > 0 ? text[..(dot+1)] : Trim(text, 120);
        return $"🧾 Resumo: {summary}";
    }

    private static string Trim(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";
}
