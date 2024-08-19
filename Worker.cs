using System.Security.Cryptography;
using System.Text;

namespace FolderSyncService
{
    public class FolderSyncWorker : BackgroundService
    {
        private readonly ILogger<FolderSyncWorker> _logger;
        private readonly IConfiguration _configuration;

        private readonly string? _sourceRootFolder;
        private readonly string? _destinationRootFolder;
        private readonly string? _logFilePath;

        public FolderSyncWorker(ILogger<FolderSyncWorker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            _sourceRootFolder = _configuration.GetValue<string>("FolderSettings:SourceFolder", defaultValue:"");
            _destinationRootFolder = _configuration.GetValue<string>("FolderSettings:DestinationFolder", defaultValue: "");
            _logFilePath = _configuration.GetValue<string>("FolderSettings:LogFilePath");

        }

        /// <summary>
        /// Main task
        /// </summary>
        /// <param name="stoppingToken">passa flag de cancelamento</param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var monitorTargetPath = !String.IsNullOrEmpty(_logFilePath) ? Path.GetDirectoryName(_logFilePath) : _sourceRootFolder;
      
            // Nova inst�ncia de FileSystemWatcher(pasta log)
           using( var watcher = new FileSystemWatcher(monitorTargetPath)) {
                watcher.IncludeSubdirectories = true;
                watcher.EnableRaisingEvents = true;
            

                //watcher.Created += async (sender, enventArgs) => {

                //    await SyncFile(enventArgs.FullPath);

                //};

                //watcher.Deleted += async (sender, eventArgs) =>
                //{
                //    var relativePath = GetRelativePath(eventArgs.FullPath);
                //    var destinationFile = Path.Combine(_destinationRootFolder, relativePath);
                //    if(File.Exists(destinationFile))
                //    {
                //        File.Delete(destinationFile);
                //    }
                //};

                // Incrementa novo evento de altera��o
                watcher.Changed += async (sender, eventArgs) => {

                    // caso n�o seja por log, ser� por rsync : para rever !!
                    //var ext = !String.IsNullOrEmpty(_logFilePath) ? Path.GetFileName(_logFilePath) : Path.GetExtension(Path.GetFileNameWithoutExtension(eventArgs.FullPath));
                    var ext = !String.IsNullOrEmpty(_logFilePath) ? Path.GetFileName(_logFilePath) : Path.GetExtension(eventArgs.FullPath);

                    // para rever !!!!
                    var filter = !String.IsNullOrEmpty(_logFilePath) ? Path.GetFileName(_logFilePath) : ".txt";

                    // procede se for ficheiro txt ou log
                    if (String.Equals(ext, filter))
                    {
                        // Console.WriteLine($"Change detected for : {eventArgs.Name}");
                        // Caso seja por log
                        if (!String.IsNullOrEmpty(_logFilePath))
                        {
                            try
                            {
                                // L� cada linha do log 
                                string[] updatedFiles = await File.ReadAllLinesAsync(_logFilePath);

                                if (updatedFiles.Length > 0)
                                {
                                    int i = 0;
                                    // para cada linha lida
                                    foreach (string file in updatedFiles)
                                    {
                                        if (!String.IsNullOrEmpty(file))
                                        {
                                            //Console.WriteLine($"File with contents : {file}");

                                            // Recebe booleano de valida��o de origem da altera��o
                                            var isOriginal = isOriginalFile(file);

                                            // Chama m�todo para sincroniza��o de ficheiros
                                            await SyncFile(file, !isOriginal);
                                        }
                                        i++;
                                    }
                                }
                                else
                                {
                                    //Console.WriteLine("Log file is empty.");
                                }
                            }
                            catch (Exception ex)
                            {

                                Console.WriteLine($"Error while reading log file: {ex.Message}");

                                //_logger.LogError($"Error while reading log file: {ex.Message}");
                            }
                        }
                        else
                        {
                            // Recebe booleano de valida��o de origem da altera��o
                            var isOriginal = isOriginalFile(eventArgs.FullPath);

                            // Chama m�todo para sincroniza��o de ficheiros
                            await SyncFile(eventArgs.FullPath, !isOriginal);
                        }
                    }
                                                     
                };


                while (!stoppingToken.IsCancellationRequested)
                {

                    //_logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);               
                    await Task.Delay(1000, stoppingToken);
            
                }
           };

        }

        /// <summary>
        /// M�todo para sincroniza��o de ficheiros
        /// </summary>
        /// <param name="sourceFile">passa caminho do ficheiro</param>
        /// <param name="reverse">Booleano para que define a dire��o da sincroniza��o</param>
        /// <returns></returns>
        private async Task SyncFile(string sourceFile, bool reverse = false)
        {
            // Constantes para tentativas de c�pia de ficheiros
            const int maxRetries = 5;
            const int delayMilliseconds = 100;

            int retryCount = 0;
            bool success = false;

            // Enquanto n�o haver sucesso na c�pia
            // Executa at� ao m�ximo de tentativas
            while(!success && retryCount < maxRetries)
            {

                try
                {

                    // Se a pasta de destino n�o existir, ser� criada
                    if (!Directory.Exists(_destinationRootFolder))
                    {
                        Directory.CreateDirectory(_destinationRootFolder);
                    }

                    // Devolve o caminho relativo do ficheiro
                    var relativePath = GetRelativePath(sourceFile);

                    // Cria o caminho do ficheiro na pasta de destino
                    var destinationFile = Path.Combine(_destinationRootFolder, relativePath);

                    // Nome da pasta onde ser� guardado o ficheiro
                    var destFolder = Path.GetDirectoryName(destinationFile);

                    // Caso a pasta de destino n�o exista
                    if (!Directory.Exists(destFolder))
                    {
                        Directory.CreateDirectory(destFolder);
                        //_logger.LogInformation($"Created directory: {destFolder}");
                    }

                    // Caso a sincroniza��o seja Destino -> Fonte
                    if (reverse)
                    {
                        //_logger.LogInformation($"copia {destinationFile}");
                        // copia ficheiro da chave para a fonte
                        // await Task.Run(() => File.Copy(destinationFile, sourceFile, true));
                        await CopyFileAsync(destinationFile, sourceFile);


                        // copia .csv para a fonte
                        //await Task.Run(() => File.Copy($"{destinationFile.Split(".")[0]}.csv", $"{sourceFile.Split(".")[0]}.csv", true));
                        await CopyFileAsync($"{destinationFile.Split(".")[0]}.csv", $"{sourceFile.Split(".")[0]}.csv");

                    }
                    // Caso a sincroniza��o seja Fonte -> Destino
                    else
                    {
                        
                        //_logger.LogInformation($"copying {sourceFile}");

                        // Copia ficheiro chave para destino
                        //await Task.Run(() => File.Copy(sourceFile, destinationFile, true));
                        await CopyFileAsync(sourceFile, destinationFile);


                        // Copia ficheiro .csv para destino
                        //await Task.Run(() => File.Copy($"{sourceFile.Split(".")[0]}.csv", $"{destinationFile.Split(".")[0]}.csv", true));
                        await CopyFileAsync($"{sourceFile.Split(".")[0]}.csv", $"{destinationFile.Split(".")[0]}.csv");
                    }

                    // Se a c�pia for bem sucedida
                    success= true;

                }
                catch (Exception ex)
                {
                    //_logger.LogError($"Error when syncing the file {sourceFile}: {ex.Message}");
                    //Console.WriteLine($"Error when syncing the file {sourceFile}: {ex.Message}");

                    // Incrementa a contagem de tentativas
                    retryCount++;
                    await Task.Delay(delayMilliseconds);

                }
            }

            if (!success)
            {
                Console.WriteLine($"Failed to sync file {sourceFile} after {maxRetries} attempts.");
            }

        }

        /// <summary>
        /// M�todo para valida��o da origem � altera��o ao ficheiro
        /// </summary>
        /// <param name="sourceFile">Caminho do ficheiro a validar</param>
        /// <returns>Devolve booleano</returns>
        private bool isOriginalFile(string sourceFile)
        {
            var clientId =_configuration.GetValue<string>("ShipSettings:ClientID");
            var shipId = _configuration.GetValue<string>("ShipSettings:ShipID");
            var fileName = sourceFile.Split(".");
            var csvContents=string.Empty;
                        
            try
            {
                //_logger.LogInformation($"Checking: {sourceFile}");
                // L� conte�do .csv
                csvContents = File.ReadAllText($"{fileName[0]}.csv");
                // calcula a chave
                var csvKey = calculateSHA1(clientId + DateTime.Now.Year + calculateSHA1(csvContents + shipId));
                // L� o conte�do do ficheiro da chave
                var txtKey = File.ReadAllText($"{fileName[0]}.txt").Trim();
                //_logger.LogInformation($"{sourceFile} is: {String.Equals(csvKey, txtKey)}");
                //_logger.LogInformation($"{txtKey} - {csvKey}");

                // Devolve se o c�lculo da chave coincide com a chave escrita
                return String.Equals(csvKey, txtKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while validating the file: {ex.Message}");
            }
           
            return false;
        }

        /// <summary>
        /// M�todo para o c�lculo da chave 
        /// </summary>
        /// <param name="input">Recebe string</param>
        /// <returns>Devolve string calculada baseada no input</returns>
        private static string calculateSHA1(string input)
        {
            // Usa algoritmo SHA1
            using (SHA1 sha1 = SHA1.Create())
            {
                // converte para bytes o input dado
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                // calcula com base nos bytes 
                byte[] bytes= sha1.ComputeHash(inputBytes);
                // devolve a chave calculada como string
                return BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();
            }
        }

        /// <summary>
        /// M�todo para que devolve o caminho relativo
        /// </summary>
        /// <param name="fullPath">passa o caminho inteiro do ficheiro</param>
        /// <returns>Devolve como string parte do caminho que prefaz o caminho relativo</returns>
        private string GetRelativePath(string fullPath)
        {
            return fullPath.Replace(_sourceRootFolder,"").TrimStart(Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// M�todo para c�pia ass�ncrona do ficheiro
        /// </summary>
        /// <param name="sourceFile">passa caminho do ficheiro de origem</param>
        /// <param name="destinationFile">passa caminho do ficheiro de destino</param>
        /// <returns></returns>
        private async Task CopyFileAsync(string sourceFile, string destinationFile)
        {
            using (var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true))
            using (var destinationStream = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
            {
                await sourceStream.CopyToAsync(destinationStream);
            }
        }
    }
}