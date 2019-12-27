using Microsoft.AspNetCore.Blazor.Hosting;

namespace FileConvert
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

#pragma warning disable CA1801 // Remove unused parameter
        public static IWebAssemblyHostBuilder CreateHostBuilder(string[] args) =>
#pragma warning restore CA1801 // Remove unused parameter
            BlazorWebAssemblyHost.CreateDefaultBuilder()
                .UseBlazorStartup<Startup>();
    }
}
