using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;
using Newtonsoft.Json.Serialization;
using Hangfire;
using Hangfire.PostgreSql;
using HetsApi.Authorization;
using HetsApi.Authentication;
using HetsData.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;
using HetsData.Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace HetsApi
{
    /// <summary>
    /// Application startup class
    /// </summary>
    public class Startup
    {
        private readonly IWebHostEnvironment _hostingEnv;

        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            _hostingEnv = env;
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            string connectionString = GetConnectionString();

            // add http context accessor
            services.AddHttpContextAccessor();

            // add database context
            services.AddDbContext<DbAppContext>(options => options.UseNpgsql(connectionString));
            services.AddDbContext<DbAppMonitorContext>(options => options.UseNpgsql(connectionString));
            services.AddScoped<IAnnualRollover, AnnualRollover>();

            services
                .AddControllers(options =>
                {
                    var policy = new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .Build();
                    options.Filters.Add(new AuthorizeFilter(policy));
                })
                .AddNewtonsoftJson(options =>
                {
                    options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                    options.SerializerSettings.Formatting = Newtonsoft.Json.Formatting.Indented;
                    options.SerializerSettings.DateFormatHandling = Newtonsoft.Json.DateFormatHandling.IsoDateFormat;
                    options.SerializerSettings.DateTimeZoneHandling = Newtonsoft.Json.DateTimeZoneHandling.Utc;
                    options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
                })
                .SetCompatibilityVersion(CompatibilityVersion.Version_3_0);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.Authority = Configuration.GetValue<string>("JWT:Authority");
                options.Audience = Configuration.GetValue<string>("JWT:Audience");
                options.RequireHttpsMetadata = false;
                options.IncludeErrorDetails = true;
                options.EventsType = typeof(HetsJwtBearerEvents);
            });

            // setup authorization
            services.AddAuthorization();
            services.RegisterPermissionHandler();
            services.AddScoped<HetsJwtBearerEvents>();

            // allow for large files to be uploaded
            services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 1073741824; // 1 GB
            });

            //enable Hangfire
            services.AddHangfire(configuration =>
                configuration
                .UseSerilogLogProvider()
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(connectionString)
            );

            services.AddHangfireServer(options =>
            {
                options.WorkerCount = 1;
            });

            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version = "v1",
                    Title = "HETS REST API",
                    Description = "Hired Equipment Tracking System"
                });
            });
        }

        /// <summary>
        /// Configure the HTTP request pipeline
        /// </summary>
        /// <param name="app"></param>
        /// <param name="env"></param>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // web site error handler  (Testing: app.UseDeveloperExceptionPage();)
            app.UseWhen(x => !x.Request.Path.Value.StartsWith("/api"), builder =>
            {
                builder.UseExceptionHandler(Configuration.GetSection("Constants:ErrorUrl").Value);
            });

            // disable the back to site link
            DashboardOptions dashboardOptions = new DashboardOptions
            {
                AppPath = null,
                Authorization = new[] { new HangfireAuthorizationFilter() }
            };

            app.UseHangfireDashboard(Configuration.GetSection("Constants:HangfireUrl").Value, dashboardOptions);

            //app.UseHealthChecks("/healthz", healthCheckOptions);

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            app.UseSwagger();
            string swaggerApi = Configuration.GetSection("Constants:SwaggerApiUrl").Value;
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint(swaggerApi, "HETS REST API v1");
                options.DocExpansion(DocExpansion.None);
            });
        }

        /// <summary>
        /// Retrieve database connection string
        /// </summary>
        /// <returns></returns>
        private string GetConnectionString()
        {
            string connectionString;

            string host = Configuration["DATABASE_SERVICE_NAME"];
            string username = Configuration["POSTGRESQL_USER"];
            string password = Configuration["POSTGRESQL_PASSWORD"];
            string database = Configuration["POSTGRESQL_DATABASE"];

            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(database))
            {
                connectionString = Configuration.GetConnectionString("HETS");
            }
            else
            {
                // environment variables override all other settings (OpenShift)
                connectionString = $"Host={host};Username={username};Password={password};Database={database}";
            }

            connectionString += ";Timeout=600;CommandTimeout=0;";

            return connectionString;
        }
    }
}
