using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using MyProject.Adapters.Email;
using MyProject.Adapters.Local;
using MyProject.Adapters.S3;
using MyProject.Core.Data;
using MyProject.Core.Errors;
using MyProject.Features.Products;
using MyProject.Shared.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddValidation();   // built-in checks for [Required], [MaxLength] etc. on endpoint parameters

// Database
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Errors
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// File storage: local disk in Development, S3 otherwise
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<IFileStorage, LocalFileStorage>();
}
else
{
    builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
    builder.Services.AddAWSService<IAmazonS3>();
    builder.Services.AddScoped<IFileStorage, S3FileStorage>();
}

// Email
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

// Features
builder.Services.AddScoped<ProductService>();

var app = builder.Build();

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();

app.MapProducts();
// app.MapOrders();

app.Run();

public partial class Program { }
