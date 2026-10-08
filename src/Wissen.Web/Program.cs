using Microsoft.AspNetCore.Identity;
using Wissen.Web.Areas.Identity;
using Wissen.Infrastructure;
using Wissen.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddErrorDescriber<GermanIdentityErrorDescriber>();
builder.Services.AddControllersWithViews();

// Ohne "RegistrierungErlaubt": true kann sich niemand selbst ein Konto anlegen.
if (!builder.Configuration.GetValue<bool>("RegistrierungErlaubt"))
{
    builder.Services.AddRazorPages(options =>
    {
        foreach (var page in new[] { "/Account/Register", "/Account/RegisterConfirmation" })
        {
            options.Conventions.AddAreaPageApplicationModelConvention("Identity", page,
                model => model.Filters.Add(new RegistrierungGesperrtFilter()));
        }
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapGet("/", () => Results.Redirect("/doc"));

app.MapControllers()
   .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();
