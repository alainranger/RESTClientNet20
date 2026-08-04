var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
}

app.UseHttpsRedirection();

// Custom JSON health check endpoint
app.UseHealthChecks("/health");
//app.UseHealthChecks("/health", new HealthCheckOptions
//{
//	ResponseWriter = async (context, report) =>
//	{
//		context.Response.ContentType = MediaTypeNames.Application.Json;

//		var response = new
//		{
//			status = report.Status.ToString(),
//			totalDuration = report.TotalDuration.TotalMilliseconds,
//			checks = report.Entries.Select(e => new
//			{
//				name = e.Key,
//				status = e.Value.Status.ToString(),
//				description = e.Value.Description,
//				duration = e.Value.Duration.TotalMilliseconds
//			})
//		};

//		JsonSerializerOptions jsonSerializerOptions = new() { WriteIndented = true };
//		await context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonSerializerOptions));
//	}
//});

var summaries = new[]
{
	"Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
	var forecast = Enumerable.Range(1, 5).Select(index =>
		new WeatherForecast
		(
			DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
			Random.Shared.Next(-20, 55),
			summaries[Random.Shared.Next(summaries.Length)]
		))
		.ToArray();
	return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
	public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
