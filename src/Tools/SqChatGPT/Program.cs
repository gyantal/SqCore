using SqCommon;

// Prepare the Builder
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

string appName = typeof(Program).Assembly.GetName().Name ?? "UnknownAppName";
string sensitiveConfigFullPath = Utils.SensitiveConfigFolderPath() + $"SqCore.Tools.{appName}.NoGitHub.json";
builder.Configuration.AddJsonFile(sensitiveConfigFullPath, optional: true, reloadOnChange: true);

// Build the App
var app = builder.Build();

// Test that configuration is loaded properly
// IWebHostEnvironment environment = app.Environment;
// IConfiguration configuration = app.Configuration;
// string? logLevel = configuration["Logging:LogLevel:Default"];
// string? openAIApiKey = configuration["ConnectionStrings:OpenAIApiKey"];

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();


app.MapControllerRoute(
    name: "default",
    pattern: "{controller}/{action=Index}/{id?}");

app.MapFallbackToFile("index.html");

app.Run();