﻿using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using SkTrailCourse.Infra;
using SkTrailCourse.Plugins;
using DotNetEnv;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();
// Add session support for conversation state
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".ZoopIA.Session";
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
});

// Configuração do Semantic Kernel
var kernelBuilder = Kernel.CreateBuilder();

try
{
    var apiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
    var modelId = Environment.GetEnvironmentVariable("AI_MODEL_ID") ?? "gemini-2.0-flash-exp";

    if (string.IsNullOrWhiteSpace(apiKey))
        throw new InvalidOperationException("GOOGLE_API_KEY não encontrada. Verifique seu arquivo .env");

    kernelBuilder.AddGoogleAIGeminiChatCompletion(
        modelId: modelId,
        apiKey: apiKey,
        apiVersion: GoogleAIVersion.V1);

    Console.WriteLine("✅ Modelo Gemini conectado com sucesso!");
    Console.WriteLine($"🤖 Modelo: {modelId}");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Erro na configuração da IA: {ex.Message}");
    Console.WriteLine("💡 Verifique se a GOOGLE_API_KEY está configurada no arquivo .env");
    return;
}

var kernel = kernelBuilder.Build();
var store = new JsonMemoryStore("data"); // ← CRIAR A INSTÂNCIA

// === Plugins ===
var orchestrator = new DisputeOrchestrator(kernel, store);
var disputes = new DisputePlugin(store, kernel, orchestrator);
var boletoLookup = new BoletoLookupPlugin();
var support = new SupportPlugin();

kernel.ImportPluginFromObject(disputes, "Disputes");
kernel.ImportPluginFromObject(boletoLookup, "BoletoLookup");
kernel.ImportPluginFromObject(support, "Support");

// Router
var router = new AIIntentRouter(kernel);

// Registrar serviços no DI
builder.Services.AddSingleton(kernel);
builder.Services.AddSingleton(router);
builder.Services.AddSingleton(orchestrator);
builder.Services.AddSingleton(disputes);
builder.Services.AddSingleton(boletoLookup);
builder.Services.AddSingleton(support);
builder.Services.AddSingleton(store); // ← REGISTRAR O STORE NO DI

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Mensagem de inicialização
Console.WriteLine("🚀 ZoopIA Web iniciado!");
Console.WriteLine("📱 Acesse: https://localhost:5000");

app.Run();