using DentalLab.Application.Abstractions;
using DentalLab.Infrastructure.Data;
using DentalLab.Infrastructure.Email;
using DentalLab.Infrastructure.Identity;
using DentalLab.Infrastructure.Services;
using DentalLab.Web.Components;
using DentalLab.Web.Components.Account;
using DentalLab.Web.Middleware;
using DentalLab.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Web
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            builder.Services.AddCascadingAuthenticationState();
            builder.Services.AddScoped<IdentityRedirectManager>();
            builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
                .AddIdentityCookies();

            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            // Scoped context - required by Identity's UserStore.
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.MigrationsAssembly(typeof(Program).Assembly.FullName);
                    sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
                    sql.CommandTimeout(60);
                }));

            // Factory - used by application services.
            // Blazor circuits are long-lived and renders can overlap, but DbContext
            // is not thread-safe. Each operation creates and disposes its own.
            builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.MigrationsAssembly(typeof(Program).Assembly.FullName);
                    sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
                    sql.CommandTimeout(60);
                }), ServiceLifetime.Scoped);


            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            builder.Services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = true;
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
            })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddSignInManager()
                .AddDefaultTokenProviders();

            builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
            builder.Services.AddScoped<IApplicationEmailService,SmtpApplicationEmailService>();
            builder.Services.AddScoped<IEmailSender<ApplicationUser>,SmtpIdentityEmailSender>();

            // ---- Application services ----
            builder.Services.AddScoped<IDoctorService, DoctorService>();
            builder.Services.AddScoped<IDoctorRateService, DoctorRateService>();
            builder.Services.AddScoped<ICaseTypeService, CaseTypeService>();
            builder.Services.AddScoped<IDoctorRateService, DoctorRateService>();
            builder.Services.AddScoped<IUserManagementService, UserManagementService>();
            builder.Services.AddScoped<ICaseService, CaseService>();
            builder.Services.AddScoped<ICaseApprovalService, CaseApprovalService>();
            builder.Services.AddScoped<IDashboardService, DashboardService>();

            // ---- Access control (Owner can switch Admin / Staff permissions at runtime) ----
            builder.Services.AddAuthorization();
            builder.Services.AddSingleton<PermissionStore>();
            builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
            builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();

            // Signed public "pay this bill" links (UPI)
            builder.Services.AddSingleton<DentalLab.Web.Payments.PayLinkService>();
            builder.Services.AddSingleton<DentalLab.Web.Settings.AppSettingsStore>();
            // Doctor payments, opening balances and saved bills (old due carry-forward)
            builder.Services.AddSingleton<DentalLab.Web.Ledger.DoctorLedgerStore>();
            builder.Services.AddScoped<DentalLab.Web.Ledger.DoctorBillBuilder>();

            var app = builder.Build();

            await IdentitySeeder.SeedAsync(app.Services, builder.Configuration);

            // Creates dbo.RolePermissions if missing and loads the switches.
            await app.Services.GetRequiredService<PermissionStore>().InitializeAsync();
            // Creates dbo.AppSettings if missing (saved bill defaults such as the UPI ID).
            await app.Services.GetRequiredService<DentalLab.Web.Settings.AppSettingsStore>().InitializeAsync();
            // Creates dbo.DoctorPayments, dbo.DoctorOpeningBalances and dbo.DoctorBills if missing.
            await app.Services.GetRequiredService<DentalLab.Web.Ledger.DoctorLedgerStore>().InitializeAsync();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for
                // production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            app.UseHttpsRedirection();
            app.UseMiddleware<TemporaryPasswordMiddleware>();

            app.UseAntiforgery();

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            // Add additional endpoints required by the Identity /Account Razor components.
            app.MapAdditionalIdentityEndpoints();

            app.Run();
        }
    }
}
