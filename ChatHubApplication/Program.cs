using ChatHubApplication;

var builder = WebApplication.CreateBuilder(args);

var startup = new Startup();
startup.ConfigureServices(builder.Services);

var app = builder.Build();
startup.Configure(app);

using (var client = new DatabaseContext())
{
    client.Database.EnsureCreated();
    client.Database.EnsureDeleted();
}

app.MapGet("/", () => "Hello World!");
app.Run();
