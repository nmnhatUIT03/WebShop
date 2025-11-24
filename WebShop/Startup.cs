using AspNetCoreHero.ToastNotification;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using WebShop.Models;

namespace WebShop
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
            _env = env;
        }

        public IConfiguration Configuration { get; }
        private readonly IWebHostEnvironment _env;

        public void ConfigureServices(IServiceCollection services)
        {
            var stringConnectdb = Configuration.GetConnectionString("DataContext");
            services.AddDbContext<webshopContext>(options => options.UseSqlServer(stringConnectdb));

            services.AddSingleton<HtmlEncoder>(HtmlEncoder.Create(allowedRanges: new[] { UnicodeRanges.All }));

            services.AddDataProtection();

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "CustomerAuthentication";
                options.DefaultChallengeScheme = "CustomerAuthentication";
                options.DefaultSignInScheme = "CustomerAuthentication";
            })
            .AddCookie("CustomerAuthentication", config =>
            {
                config.Cookie.Name = "CustomerLoginCookie";
                config.ExpireTimeSpan = TimeSpan.FromDays(1);
                config.LoginPath = "/dang-nhap.html";
                config.AccessDeniedPath = "/thong-bao.html";
                config.SlidingExpiration = true;
                config.Cookie.SameSite = SameSiteMode.Lax;
                config.Cookie.SecurePolicy = _env.IsDevelopment() ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
            })
            .AddCookie("AdminAuthentication", config =>
            {
                config.Cookie.Name = "AdminLoginCookie";
                config.ExpireTimeSpan = TimeSpan.FromDays(1);
                config.LoginPath = "/Admin/AdminLogin";
                config.AccessDeniedPath = "/Admin/AdminLogin/AccessDeny";
                config.SlidingExpiration = true;
                config.Cookie.SameSite = SameSiteMode.Lax;
                config.Cookie.SecurePolicy = _env.IsDevelopment() ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
            });

            services.AddDistributedMemoryCache();
            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = _env.IsDevelopment() ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
            });

            services.AddMemoryCache();

            services.AddControllersWithViews()
                .AddRazorRuntimeCompilation()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
                    options.JsonSerializerOptions.MaxDepth = 128;
                });

            services.AddNotyf(config =>
            {
                config.DurationInSeconds = 3;
                config.IsDismissable = true;
                config.Position = NotyfPosition.TopRight;
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
                app.UseHttpsRedirection();
            }

            app.UseStaticFiles();
            app.UseRouting();
            app.UseSession();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "areas",
                    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}"
                );
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}"
                );
                endpoints.MapControllerRoute(
                    name: "login",
                    pattern: "dang-nhap.html",
                    defaults: new { controller = "Accounts", action = "Login" }
                );
                endpoints.MapControllerRoute(
                    name: "checkout",
                    pattern: "checkout.html",
                    defaults: new { controller = "Checkout", action = "Index" }
                );
                endpoints.MapControllerRoute(
                    name: "dashboard",
                    pattern: "tai-khoan-cua-toi.html",
                    defaults: new { controller = "Accounts", action = "Dashboard" }
                );
                endpoints.MapControllerRoute(
                    name: "success",
                    pattern: "dat-hang-thanh-cong.html",
                    defaults: new { controller = "Checkout", action = "Success" }
                );
                endpoints.MapControllerRoute(
                   name: "register",
                   pattern: "dang-ky.html",
                    defaults: new { controller = "Accounts", action = "DangKyTaiKhoan" } // Thêm route này
                );
            });
        }

    }
}