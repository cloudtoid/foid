namespace Cloudtoid.GatewayCore.FunctionalTests
{
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using static Contract;

    internal sealed class UpstreamStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            CheckValue(services, nameof(services));

            _ = services.AddControllers();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            CheckValue(app, nameof(app));
            CheckValue(env, nameof(env));

            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();

            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }

        internal static IWebHost BuildWebHost(int port)
        {
            return WebHost.CreateDefaultBuilder()
                .ConfigureKestrel(o =>
                {
                    o.ListenLocalhost(
                        port,
                        lo =>
                        {
                            lo.Protocols = HttpProtocols.Http1AndHttp2;
                            lo.UseHttps();
                        });
                })
                .UseStartup<UpstreamStartup>()
                .Build();
        }
    }
}
