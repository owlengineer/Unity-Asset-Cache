﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
 using System.Runtime.CompilerServices;
 using CommandLine;
using System.Text;
 using System.Threading;

 namespace IAssetCacheJB
{
    internal class Program
    {
        public static Action interrupter = () =>
        {
            Random random = new Random();  
            int interrupt_factor = random.Next(0, 100);
            if (interrupt_factor < 2)
            {
                throw new OperationCanceledException("Interrupter call received. Waiting...");
            }
        };

        public static void writeDownSlashNByChance(string path)
        {
            Random random = new Random();
            int filechange_factor = random.Next(0, 100);
            if (filechange_factor < 50)
                return;

            StreamWriter sw = new StreamWriter(path, true);
            sw.Write("\n");
            sw.Close();
        }
        
        public static string DownloadAsset(Options o, string path)
        {
            string link = o.Link;
            string filename = o.Filename;
            try
            {
                string filepath = path + "/" + filename;
                FileDownloader.DownloadFileFromURLToPath(
                    link, filepath);
                if (filename.Contains(".zip"))
                {
                    var unitypath = path + "/" + filename.Substring(0, filename.Length - 4) + ".unity";

                    if (File.Exists(unitypath))
                        File.Delete(unitypath);
                    Console.WriteLine("Unzipping the asset...");
                    ZipFile.ExtractToDirectory(filepath, path + "/");
                    filepath = unitypath;
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Asset successfully downloaded!");
                Console.ResetColor();
                Console.WriteLine("Building cache for asset...");

                return filepath;
            }
            catch (Exception e)
            {
                throw e;
            }
        } 
        
        public static bool RequestCacheBuild(CustomAssetCache cache, string filepath)
        {
            object assetObj = null;
            try
            {
                assetObj = cache.Build(filepath, interrupter);
                cache.Merge(filepath, assetObj);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("\n\nCache successfully built!\n");
            }
            catch (Exception e)
            {
                cache.SetProgressBarErrorState(e.Message);
                return false;
            }
            
            Console.Write("\n");
            return true;
        }

        public static void TestAPI(CustomAssetCache cache)
        {
            bool exit = false;
            Console.ResetColor();
            while (!exit)
            {
                Console.Write("API Testing\n" +
                                  "1. Test GetLocalAnchorUsages(...) \n" +
                                  "2. Test GetGuidUsages(...) \n" +
                                  "3. Test GetComponentsFor(...)\n" +
                                  "0. Exit\n" +
                                  "Input number: ");
                int command;
                try
                {
                    string input = Console.ReadLine();
                    command = Convert.ToInt32(input);
                    if (command < 0 || command > 3)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("Invalid input. Try again.\n");
                        Console.ResetColor();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
                if (command == 1)
                {
                    Console.Write("Input GameObject ID: ");
                    ulong id = Convert.ToUInt64(Console.ReadLine());
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    int usages = cache.GetLocalAnchorUsages(id);
                    watch.Stop();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("FileID: " + id + ". count: " + usages + " TIME: " + Math.Round(watch.Elapsed.TotalMilliseconds, 5) + " ms");
                    Console.ResetColor();
                } else if (command == 2)
                {
                    Console.Write("Input Resource GUID: ");
                    string id = Console.ReadLine();
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    int usages = cache.GetGuidUsages(id);
                    watch.Stop();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Resource GUID: " + id + ". count: " + usages + " TIME: " + Math.Round(watch.Elapsed.TotalMilliseconds, 5) + " ms");
                    Console.ResetColor();
                } else if (command == 3)
                {
                    Console.Write("Input GameObject ID: ");
                    ulong id = Convert.ToUInt64(Console.ReadLine());
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    IEnumerable<ulong> components = cache.GetComponentsFor(id);
                    watch.Stop();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("LIST:");
                    foreach (var comp in components)
                        Console.WriteLine("\tITEM: " + comp);
                    Console.WriteLine("TIME: " + Math.Round(watch.Elapsed.TotalMilliseconds, 5) + " ms");
                    Console.ResetColor();
                } else if (command == 0)
                {
                    exit = true;
                    continue;
                }
                Console.Write("Press Enter to continue testing...");
                Console.Read();
            }
        }
        
        public static void Main(string[] args)
        {
            string path = AssetsHelper.CreateAssetsDir();
            
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(o =>
                    {
                        CustomAssetCache cache = new CustomAssetCache();
                        try
                        {
                            string filepath = o.FilePath;
                            if (filepath == null)
                            {
                                Console.WriteLine("Downloading the asset from Google Drive...");       
                                filepath = DownloadAsset(o, path);
                            }
                                
                            while (!RequestCacheBuild(cache, filepath))
                            {
                                writeDownSlashNByChance(filepath);
                                Thread.Sleep(2500);
                            }
                            
                            TestAPI(cache);
                            
                        } catch (Exception e)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Error: " + e.Message + " Aborting.");
                        }

                    }
                );
        }
    }
    
    public class Options
    {
        [Option('l', "link", Required = false, Default = "https://drive.google.com/file/d/1zLV8MmwiXazvpv-6LMWNbuewPabzBFD9", HelpText = "Input Google Drive link to the asset.")]
        public string Link { get; set; }
        
        [Option('p', "path", Required = false, Default = null, HelpText = "Input full path to .unity file. Don't use with other options")]
        public string FilePath { get; set; }
        
        [Option('f', "filename", Required = false, Default = "SampleScene.zip", HelpText = "Input Google Drive link to the asset.")]
        public string Filename { get; set; }
    }
    
    public static class AssetsHelper
    {
        public static string CreateAssetsDir()
        {
            string path = Directory.GetCurrentDirectory();
            path += "/assets";
            if (!Directory.Exists(path)) 
                Directory.CreateDirectory(path);
            return path;
        }
        
    }
}