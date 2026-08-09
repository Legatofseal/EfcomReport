using EfcomReport.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPortalDataServices(builder.Configuration);
builder.Services.AddPortalAuthentication(builder.Configuration);
builder.Services.AddRazorPages(options => options.Conventions.AuthorizeFolder("/Invoices"));

var app = builder.Build();

if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

PortalDatabaseInitializer.EnsureCreated(app.Services);
app.MapPortalAccountEndpoints();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.Run();
