using SkOfflineCourse.Infra;
using SkOfflineCourse.Plugins;
using Xunit;

namespace SkOfflineCourse.Tests;

public class PluginTests
{
    [Fact]
    public async Task TaskPlugin_AddListComplete()
    {
        var store = new JsonMemoryStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        var plugin = new TaskPlugin(store);
        var add = await plugin.AddTask("Comprar café");
        Assert.Contains("Comprar café", add);
        var list = await plugin.ListTasks();
        Assert.Contains("Comprar café", list);
        var complete = await plugin.CompleteTask(1);
        Assert.Contains("Concluída", complete);
        var list2 = await plugin.ListTasks();
        Assert.Contains("[x]", list2);
    }

    [Fact]
    public async Task NotesPlugin_AddSearchSummarize()
    {
        var store = new JsonMemoryStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        var mockSumm = new MockSummarizer { Fixed = "RESUMO_MOCK" };
        var plugin = new NotesPlugin(store, mockSumm);
        await plugin.AddNote("Ideia: criar app offline usando SK");
        var list = await plugin.ListNotes();
        Assert.Contains("Ideia", list);
        var search = await plugin.SearchNotes("offline");
        Assert.Contains("Ideia", search);
        var summary = await plugin.SummarizeNote(1);
        Assert.Contains("RESUMO_MOCK", summary);
    }
}