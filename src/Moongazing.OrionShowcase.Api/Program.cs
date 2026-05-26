var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/", () => "OrionShowcase scaffolded; real composition in Task 12.");
app.Run();
public partial class Program;
