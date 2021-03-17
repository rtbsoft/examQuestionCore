using System;
using ExamQuestion.Hubs;
using ExamQuestion.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace ExamQuestion
{
    public class Startup
    {
        public Startup(IConfiguration configuration) => Configuration = configuration;

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            //add session support
            services.AddDistributedMemoryCache();
            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(20);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SameSite = SameSiteMode.Unspecified;
            });

            //switch to using the Newtonsoft Json library -- it has some functionality the default does not
            services.AddControllers().AddNewtonsoftJson(opts =>
            {
                //stop infinite loops when getting children of elements
                opts.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                //change case between JS and C# versions of objects
                opts.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                //don't pass JSON properties that are null
                opts.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
                //convert enums to their text equivalent for passing to client
                opts.SerializerSettings.Converters.Add(new StringEnumConverter());
            });

            //connect to the SQL Server database
            services.AddDbContext<AppDbContext>(opts =>
                opts.UseSqlServer(Configuration.GetConnectionString("DefaultConnection"))
                    .EnableSensitiveDataLogging());

            //add signalr
            services.AddSignalR();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();
            else
                app.UseHsts();

            //add data to database for testing
            SeedData.CreateSeedData(app.ApplicationServices);

            app.UseSession();
            app.UseRouting();
            app.UseAuthorization();

            //add these when hosting in production
            //build the vuejs app for prod and copy its output to the wwwroot directory
            app.UseHttpsRedirection();
            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<AllocationHub>("/allocationHub");
            });
        }
    }
}