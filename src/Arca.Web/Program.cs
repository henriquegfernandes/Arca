using Arca.Application.Auth;
using Arca.Application.Catalog;
using Arca.Application.Security;
using Arca.Application.Tenancy;
using Arca.Infrastructure;
using Arca.Infrastructure.Database;
using Arca.Infrastructure.Seed;
using Arca.Web.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddHealthChecks();
builder.Services.AddInfrastructure();
builder.Services.AddScoped<AuthenticateUserUseCase>();
builder.Services.AddScoped<TenantSetupService>();
builder.Services.AddScoped<UserProvisioningService>();
builder.Services.AddScoped<CatalogTemplateSeeder>();
builder.Services.AddScoped<ProductCatalogService>();
builder.Services.AddSingleton<IProductVariantGenerator, ProductVariantGenerator>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = builder.Configuration["Authentication:CookieName"] ?? "Arca.Admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(builder.Configuration.GetValue("Authentication:ExpireMinutes", 480));
        options.SlidingExpiration = true;
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/access-denied";
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdmin", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("arca:is_super_admin", bool.TrueString);
    });

    foreach (var permission in KnownPermissions.All)
    {
        options.AddPolicy(permission.Name, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.Requirements.Add(new PermissionRequirement(permission.Name));
        });
    }
});

var app = builder.Build();

if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<IDatabaseMigrationRunner>().MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<ISuperAdminSeedService>().SeedAsync();
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapDefaultControllerRoute();
app.Run();
