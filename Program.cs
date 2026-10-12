using Project_Malawi_Resto.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("ReactDevelopment", policy =>
        {
            policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();

if (app.Environment.IsDevelopment())
{
    app.UseCors("ReactDevelopment");
    app.MapGet("/", () => Results.Ok(new
    {
        application = "Malawi Resto API",
        frontend = "http://localhost:5173/client/",
        health = "/api/health"
    }));
}
else
{
    app.MapGet("/", () => Results.Redirect("/client/"));
    app.MapFallbackToFile("client/{*path:nonfile}", "client/index.html");
}

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    DatabaseHelper.Initialize(config);
}

app.Run();
