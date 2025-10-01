var builder = WebApplication.CreateBuilder(args);

// Adiciona serviços ao contêiner
builder.Services.AddControllers();
// O AddControllers já configura o roteamento e a serialização JSON básica.
var app = builder.Build();

// Configura o pipeline de requisições HTTP.
app.UseAuthorization();
app.MapControllers(); 

app.Run();
