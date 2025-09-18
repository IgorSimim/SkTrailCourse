using Microsoft.SemanticKernel;

namespace SkOfflineCourse.Infra;

public class IntentRouter
{
    // Retorna (plugin, function, arguments)
    public (string? plugin, string? function, KernelArguments args) Route(string input)
    {
        input = input.Trim();
        var args = new KernelArguments();

        // ----- Tarefas -----
        if (input.StartsWith("add tarefa", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("adicionar tarefa", StringComparison.OrdinalIgnoreCase))
        {
            var title = input[(input.IndexOf("tarefa", StringComparison.OrdinalIgnoreCase) + 6)..].Trim();
            args["title"] = string.IsNullOrWhiteSpace(title) ? "Sem título" : title;
            return ("Tasks", "AddTask", args);
        }
        if (input.Equals("listar tarefas", StringComparison.OrdinalIgnoreCase))
            return ("Tasks", "ListTasks", args);

        if (input.StartsWith("concluir", StringComparison.OrdinalIgnoreCase))
        {
            var num = ExtractInt(input);
            args["index"] = num;
            return ("Tasks", "CompleteTask", args);
        }
        if (input.Contains("sugerir", StringComparison.OrdinalIgnoreCase) &&
            input.Contains("proxima", StringComparison.OrdinalIgnoreCase))
            return ("Tasks", "RecommendNext", args);

        // ----- Notas -----
        if (input.StartsWith("add nota", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("adicionar nota", StringComparison.OrdinalIgnoreCase))
        {
            var text = input[(input.IndexOf("nota", StringComparison.OrdinalIgnoreCase) + 4)..].Trim();
            args["content"] = string.IsNullOrWhiteSpace(text) ? "Vazio" : text;
            return ("Notes", "AddNote", args);
        }
        if (input.Equals("listar notas", StringComparison.OrdinalIgnoreCase))
            return ("Notes", "ListNotes", args);

        if (input.StartsWith("buscar nota", StringComparison.OrdinalIgnoreCase))
        {
            var term = input[(input.IndexOf("nota", StringComparison.OrdinalIgnoreCase) + 4)..].Trim();
            args["term"] = term;
            return ("Notes", "SearchNotes", args);
        }
        if (input.StartsWith("resumo", StringComparison.OrdinalIgnoreCase))
        {
            var num = ExtractInt(input);
            args["index"] = num;
            return ("Notes", "SummarizeNote", args);
        }

        return (null, null, args);
    }

    private static int ExtractInt(string s)
        => int.TryParse(new string(s.Where(char.IsDigit).ToArray()), out var n) ? n : -1;
}
