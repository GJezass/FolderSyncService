using FolderSyncService;

public class Program { 
    
    public static IConfiguration? Configuration { get; set; }

    public static void Main(string[] args)
    {
        IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile("appsettings.json", optional:false, reloadOnChange: true);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((hostingContext,services) =>
            {
                Configuration = hostingContext.Configuration;
                services.AddHostedService<FolderSyncWorker>();
            })
            .Build();

        host.Run();
    }

}


