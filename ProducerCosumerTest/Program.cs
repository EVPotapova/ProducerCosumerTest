using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ProducerCosumerTest
{
    class Program
    {
        static void Main(string[] args)
        {
            //Start
            var stopwatch = Stopwatch.StartNew();

            #region Params
            if (args.Length == 0 || args.Length < 2)
            {
                Console.WriteLine("Please enter a directory path and string length.");
                Finish(stopwatch);
                return;
            }

            string filePath = args[0];

            var tryParse = int.TryParse(args[1], out int stringLength);

            if (!tryParse || stringLength < 1)
            {
                Console.WriteLine("Please enter a correct string length.");
                Finish(stopwatch);
                return;
            }
            #endregion

            #region StartFilesQueue
            BlockingCollection<string> fileNamesQueue = new BlockingCollection<string>();
            try
            {
                //TODO: Validate input settings
                foreach(var pattern in Settings.Default.TextFilePatterns)
                {
                    var files = Directory.GetFiles(filePath, pattern, Settings.Default.WithSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                    foreach(var file in files)
                    {
                        fileNamesQueue.Add(file);
                    }
                }

                fileNamesQueue.CompleteAdding();
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine("Invalid directory path.");
                Finish(stopwatch);
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Finish(stopwatch);
                return;
            }
            #endregion


            //Количество одновременно обрабатываемых файлов, количество строк в очереди и количество обрабатываемых строк так же можно вынести в настройки.
            //Текущие значения подобраны с учетом лучшего показанного времени в демонстрационных целях
            BlockingCollection<string> stringQueue = new BlockingCollection<string>(500);

            //Потокобезопасная реализация словаря
            ConcurrentDictionary<string, int> result = new ConcurrentDictionary<string, int>();


            //При данной постановке задачи Tasks подходят лучше
            //т.к. необходимо параллельно выполнить некоторые операции
            //дождаться их завершения в определенный момент времени
            var readingTasks =
                Enumerable.Range(0, 7)
                    .Select(_ =>
                        Task.Run(() =>
                        {
                            foreach (var fileName in fileNamesQueue.GetConsumingEnumerable())
                            {
                                ReadDocumentFromSourceStore(fileName, stringQueue);
                            }
                        }))
                    .ToArray();

            var countWords =
                Enumerable.Range(0, 7)
                    .Select(_ =>
                        Task.Run(() =>
                        {
                            // Use GetConsumingEnumerable() instead of just blocking collection because the
                            // former will block waiting for completion and the latter will
                            // simply take a snapshot of the current state of the underlying collection.
                            foreach (var readDocument in stringQueue.GetConsumingEnumerable())
                            {
                                GetWord(readDocument, result, stringLength);
                            }
                        }))
                    .ToArray();



            Task.WaitAll(readingTasks);

            stringQueue.CompleteAdding();

            Task.WaitAll(countWords);

            foreach (var r in result.OrderByDescending(k => k.Value).Take(10))
            {
                Console.WriteLine($"{r.Key} - {r.Value}");
            }
            
            Finish(stopwatch);
        }


        private static void Finish(Stopwatch stopwatch)
        {
            //Time elapsed
            stopwatch.Stop();

            Console.WriteLine();
            Console.WriteLine(string.Format("Time elapsed: {0}s", stopwatch.Elapsed.TotalSeconds));
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }

        private static void GetWord(string inputString, ConcurrentDictionary<string, int> result, int stringLength)
        {
            string pattern = @"\w{" + stringLength + ",}";//TODO: Format
            Regex rgx = new Regex(pattern, RegexOptions.IgnoreCase);

            foreach (Match match in rgx.Matches(inputString))
            {
                result.AddOrUpdate(match.Value, 1, (k, v) => v = v + 1);
            }
        }

        private static void ReadDocumentFromSourceStore(string fileName, BlockingCollection<string> stringQueue)
        {
            //Файлы могут быть достаточно большими
            //Считываем построчно и отправляем в очередь на обработку
            try
            {
                using (StreamReader fs = new StreamReader(fileName))
                {
                    while (true)
                    {
                        string temp = fs.ReadLine();

                        if (temp == null) break;

                        if (!string.IsNullOrWhiteSpace(temp))
                            stringQueue.Add(temp);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error on {fileName}. {ex.Message}");//TODO: В идеале - это в лог. NLog, Log4Net, Serilog etc.
            }

        }
    }
}
