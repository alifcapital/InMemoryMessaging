using System.Reflection;
using InMemoryMessaging.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

Assembly[] assembliesToRegisterMessageHandlers = [typeof(Program).Assembly];
builder.Services.AddInMemoryMessaging(assembliesToRegisterMessageHandlers);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();