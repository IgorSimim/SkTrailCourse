﻿using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using SkTrailCourse.Infra;
using SkTrailCourse.Plugins;
using DotNetEnv;
using Microsoft.AspNetCore.DataProtection;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

// 🔧 CONFIGURAÇÃO DATA PROTECTION PARA DESENVOLVIMENTO
var keysDirectory = Path.Combine(Directory.GetCurrentDirectory(), "data-protection-keys");
if (!Directory.Exists(keysDirectory))
{
    Directory.CreateDirectory(keysDirectory);
}

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDirectory))
    .SetApplicationName("ZoopIA")
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90));

// Para desenvolvimento, podemos usar criptografia simulada
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDataProtection()
        .UseEphemeralDataProtectionProvider();
}

// Add services to the container
builder.Services.AddControllersWithViews();

// 🔧 CONFIGURAÇÃO DE SESSÃO CORRIGIDA
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "ZoopIA.Session";
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.None; // Para desenvolvimento
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

// 🔧 CORREÇÃO: Criar o JsonMemoryStore ANTES dos plugins que dependem dele
var store = new JsonMemoryStore("data");

// === Plugins ===
var orchestrator = new DisputeOrchestrator(kernel, store);
var disputes = new DisputePlugin(store, kernel, orchestrator);
var boletoLookup = new BoletoLookupPlugin();
var support = new SupportPlugin();

// 🔧 CORREÇÃO: Usar ImportPluginFromObject (síncrono) em vez do assíncrono
try
{
    kernel.ImportPluginFromObject(disputes, "Disputes");
    kernel.ImportPluginFromObject(boletoLookup, "BoletoLookup");
    kernel.ImportPluginFromObject(support, "Support");
    Console.WriteLine("✅ Plugins carregados com sucesso!");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Erro ao carregar plugins: {ex.Message}");
}

// Router
var router = new AIIntentRouter(kernel);

// 🔧 CORREÇÃO: Registrar serviços no DI na ordem correta
builder.Services.AddSingleton(kernel);
builder.Services.AddSingleton(router);
builder.Services.AddSingleton(orchestrator);
builder.Services.AddSingleton(disputes);
builder.Services.AddSingleton(boletoLookup);
builder.Services.AddSingleton(support);
builder.Services.AddSingleton(store);

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    // 🔧 PARA DESENVOLVIMENTO: Configurações mais relaxadas
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// 🔧 CRÍTICO: UseSession() deve vir ANTES do UseAuthorization()
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Mensagem de inicialização
Console.WriteLine("🚀 ZoopIA Web iniciado!");
Console.WriteLine("📱 Acesse: https://localhost:5000");
Console.WriteLine("🔐 Sessão configurada com sucesso!");
Console.WriteLine("🤖 Modelo Gemini conectado com sucesso!");
app.Run();