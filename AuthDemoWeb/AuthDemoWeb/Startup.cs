using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using AspNetCore.Identity.Mongo;
using AspNetCore.Identity.Mongo.Model;
using AuthDemoWeb.Models;
using AuthDemoWeb.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace AuthDemoWeb
{
    public class Startup
    {
        public Startup(IWebHostEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables()
                .AddUserSecrets<Startup>(true, reloadOnChange: true);

            var configuration = builder.Build();
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddIdentityMongoDbProvider<AppUser, MongoRole>(identityOptions =>
            {
                identityOptions.Password.RequiredLength = 6;
                identityOptions.Password.RequireLowercase = false;
                identityOptions.Password.RequireUppercase = false;
                identityOptions.Password.RequireNonAlphanumeric = false;
                identityOptions.Password.RequireDigit = false;
                identityOptions.User.RequireUniqueEmail = true;
            },
            mongoIdentityOptions => {
                mongoIdentityOptions.ConnectionString = Configuration["IdentityDB"];
            }).AddDefaultTokenProviders();
            
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear(); // => remove default claims
            services
                .AddAuthentication(options =>
                {
                    ///TODO: If you plan to use both cookie and JWT auth on this API, you can use this attribute
                    ///[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;

                })
                .AddCookie(options =>
                {
                    options.Events.OnRedirectToLogin = options.Events.OnRedirectToAccessDenied = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    };
                })
                .AddJwtBearer(cfg =>
                {
                    cfg.RequireHttpsMetadata = false;
                    cfg.SaveToken = true;
                    cfg.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidIssuer = Configuration["JwtIssuer"],
                        ValidAudience = Configuration["JwtIssuer"],
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["JwtKey"])),
                        ClockSkew = TimeSpan.Zero // remove delay of token when expire
                    };
                })
                .AddFacebook(facebook =>
                {
                    facebook.AppId = Configuration["Authentication:Facebook:AppId"];
                    facebook.AppSecret = Configuration["Authentication:Facebook:AppSecret"];
                    facebook.SaveTokens = true;
                })
                .AddGoogle(google =>
                {
                    google.ClientSecret = Configuration["Authentication:Google:ClientSecret"];
                    google.ClientId = Configuration["Authentication:Google:ClientId"];
                    google.Scope.Add("profile");
                    google.Events.OnCreatingTicket = (context) =>
                    {
                        var picture = context.User.GetProperty("picture").GetString();

                        context.Identity.AddClaim(new Claim("picture", picture));

                        return Task.CompletedTask;
                    };
                    google.SaveTokens = true;
                });

            services.AddScoped<FacebookAuthService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
