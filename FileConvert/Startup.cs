using FileConvert.Core;
using FileConvert.Infrastructure;
using Microsoft.AspNetCore.Components.Builder;
using Microsoft.Extensions.DependencyInjection;


namespace FileConvert
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddTransient<IFileConvertors, FileConversionService>();
        }

        public void Configure(IComponentsApplicationBuilder app)
        {
            //dunno
            app.AddComponent<App>("app");
        }
    }
}
