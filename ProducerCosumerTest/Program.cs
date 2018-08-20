using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ProducerCosumerTest
{
    class Program
    {
        static void Main(string[] args)
        {
            //Start
            var stopwatch = Stopwatch.StartNew();


            string filePath = @"C:\Work\Test\";//TODO: To params
            string[] documents = Directory.GetFiles(filePath, "*.txt");//TODO: To settings

            var fileNamesQueue = CreateInputQueue(documents);

            BlockingCollection<string> stringQueue = new BlockingCollection<string>(500);

            //Потокобезопасная реализация словаря
            ConcurrentDictionary<string, int> result = new ConcurrentDictionary<string, int>();



            var countWords =
                Enumerable.Range(0, 10)
                    .Select(_ =>
                        Task.Run(() =>
                        {
                            foreach (var readDocument in stringQueue.GetConsumingEnumerable())
                            {
                                GetWord(readDocument, result);
                                
                            }
                        }))
                    .ToArray();

            var readingTasks =
                Enumerable.Range(0, 5)
                    .Select(_ =>
                        Task.Run(() =>
                        {
                            foreach (var fileName in fileNamesQueue.GetConsumingEnumerable())
                            {
                                ReadDocumentFromSourceStore(fileName, stringQueue);
                            }
                        }))
                    .ToArray();

            Task.WaitAll(readingTasks);

            stringQueue.CompleteAdding();

            Task.WaitAll(countWords);

            var res = result.OrderByDescending(k => k.Value).Take(10);
            foreach(var r in res)
            {
                Console.WriteLine($"{r.Key} - {r.Value}");
            }

            //Time elapsed
            stopwatch.Stop();

            Console.WriteLine();
            Console.WriteLine(string.Format("Time elapsed: {0}s", stopwatch.Elapsed.TotalSeconds));
            Console.ReadKey();
        }

        private static void GetWord(string inputString, ConcurrentDictionary<string, int> result)
        {
            inputString = inputString.ToLower();//TODO: Это создание новой строки! Ресурсы

            
            Regex rgx = new Regex(@"\w{5,}");//TODO: to params

            foreach (Match match in rgx.Matches(inputString))
            {
                result.AddOrUpdate(match.Value, 1, (k, v) => v=v+1);
            }
        }

        private static void ReadDocumentFromSourceStore(string fileName, BlockingCollection<string> stringQueue)
        {


            using (StreamReader fs = new StreamReader(fileName))
            {
                while (true)
                {
                    string temp = fs.ReadLine();
                    
                    if (temp == null) break;

                    if(!string.IsNullOrWhiteSpace(temp))
                    stringQueue.Add(temp);
                }
            }


        }
        private static BlockingCollection<string> CreateInputQueue(string[] fileNames)
        {
            var inputQueue = new BlockingCollection<string>();

            foreach (var id in fileNames)
                inputQueue.Add(id);

            inputQueue.CompleteAdding();

            return inputQueue;
        }
    }
}
