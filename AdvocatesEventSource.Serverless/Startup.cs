using AdvocatesEventSource.Infrastructure;
using AdvocatesEventSource.Serverless;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

[assembly: FunctionsStartup(typeof(Startup))]
namespace AdvocatesEventSource.Serverless
{    
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddScoped(sp => new AzureStorageHelper(Environment.GetEnvironmentVariable("AdvocateDashboardStorageConnectionString")));
        }
    }
}
