using Microsoft.EntityFrameworkCore;
using Sistema_Suporte;
using Sistema_Suporte.Data;
using Sistema_Suporte.Services;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// ✅ Configurar Session (IMPORTANTE para login)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ✅ Configurar MySQL
var connectionString = builder.Configuration.GetConnectionString("MySqlConnection");

builder.Services.AddDbContext<BancoContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
);

// Registrar o serviço da IA Gemini
builder.Services.AddSingleton<IGeminiAIService>(provider =>
    new GeminiAIService("AIzaSyB_bipO6AegQyd-Iirp9B5Ga2lMQXcOiC8"));
//builder.Services.AddSingleton<IGeminiAIService>(sp =>
//    new GeminiAIService(builder.Configuration["GeminiApi:ApiKey"]));

//builder.Services.AddSingleton<IGeminiAIService>(provider =>
//    new GeminiAIService("AIzaSyD52rfzRjMrxRtQQQ0yZnd-W9uVUpIxrb8"));


// ✅ SignalR
builder.Services.AddSignalR();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // ✅ IMPORTANTE: Para servir CSS, JS, imagens
app.UseRouting();

// ✅ IMPORTANTE: Adicionar Authentication se estiver usando
app.UseAuthorization();

// ✅ IMPORTANTE: Usar Session
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.MapHub<ChatHub>("/chatHub");

app.Run();