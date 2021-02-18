using AdvocatesEventSource.CliCommands;
using AdvocatesEventSource.Model;
using System;
using System.Threading.Tasks;

namespace AdvocatesEventSource
{
    class Program
    {
        private static string gitPath = @"C:\git_ws\MicrosoftDocs\cloud-developer-advocates";
        private static string connectionString = Environment.GetEnvironmentVariable("StorageAccountConnectionString");

        public static async Task Main(string[] args)
        {
            if (args.Length != 1) { Console.WriteLine("No arguments specified."); return; } ;

            switch (args[0])
            {
                case nameof(GenerateAllEvents):
                    await new GenerateAllEvents(gitPath, connectionString).ExecuteAsync();
                    break;
                case nameof(GenerateCurrentState):
                    await new GenerateCurrentState(connectionString).ExecuteAsync(); 
                    break;
                case nameof(GenerateAdvocatesForDashboard):
                    await new GenerateAdvocatesForDashboard(connectionString).ExecuteAsync();
                    break;
                default:
                    break;
            }

        }


    }
}
