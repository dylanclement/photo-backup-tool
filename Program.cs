﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CommandLine;

namespace photo_organiser
{
    class Program
    {
        public class Options
        {
            [Option('i', "inputdir", Required = true, HelpText = "Input directory containing photos and movies.")]
            public string InputDir { get; set; }
            [Option('o', "outputdir", Required = true, HelpText = "Output directory to copy sorted and shrunk photos and movies.")]
            public string OutputDir { get; set; }

        }

        public class FileAndDirectoryService
        {
            private Regex r;
            private Dictionary<string, List<string>> fileList;
            public FileAndDirectoryService()
            {
              r = new Regex(":");
              fileList = new Dictionary<string, List<string>>();
            }
            public void ProcessDir(string inputDir, string outputDir)
            {
                var files = Directory.GetFiles(inputDir, "*", SearchOption.AllDirectories);
                foreach(var f in files)
                {
                  try
                  {
                    var extension = Path.GetExtension(f).ToLower();
                    if (extension == ".jpg") {
                      AddImage(f);
                    } else if (extension == ".mp4") {
                      AddMovie(f);
                    } else {
                      Console.WriteLine($"Unknown extension {extension} for file {f}.");
                    }
                  }
                  catch (Exception ex)
                  {
                    Console.WriteLine($"Exception Occured: {ex.Message} for file {f}.");
                  }
                }
            }
            public void SaveOutput(string outputDir)
            {
                foreach(var f in fileList)
                {
                  var year = f.Key.Substring(0,4);

                  var destDir = Path.Join(outputDir, Path.Join(year, f.Key));
                  if (!Directory.Exists(destDir))
                  {
                    Directory.CreateDirectory(destDir);
                  }
                  foreach (var fileName in f.Value)
                  {
                    File.Copy(fileName, Path.Join(destDir, Path.GetFileName(fileName)));
                  }
                }
            }

            private void AddImage(string f)
            {
                var date = GetDateTakenFromImage(f);
                var dateKey = date.ToString("yyyy-MM-dd");
                Console.WriteLine($"File {f} was taken on {date.ToShortDateString()}");                  
                if (!fileList.ContainsKey(dateKey)) {
                  fileList[dateKey] = new List<string>();
                }
                fileList[dateKey].Add(f);
            }

            private void AddMovie(string fileName)
            {
              var strDateIndex = fileName.IndexOf("20");
              if (strDateIndex == -1) 
              {
                Console.WriteLine($"Unable to find a date in fileName {fileName}.");
                return;
              }

              var dateKey = $"{fileName.Substring(strDateIndex, 4)}-{fileName.Substring(strDateIndex, 2)}-{fileName.Substring(strDateIndex, 2)}";
              Console.WriteLine($"File {fileName} was taken on {dateKey}");

              // Attempt to shrink movie using ffmpeg, and use new filename if it is smaller
              // Create a temp file with same name in $TMP folder
              var destFile = Path.GetTempPath() + Path.GetFileName(fileName);
              // Delete file if already exists
              File.Delete(destFile);
              // Create new temp file

              // Run ffmpeg to try and shrink the file
              var procInfo = new ProcessStartInfo("ffmpeg", $"-i {fileName} -c:v libx264 -preset medium -crf 25 -movflags +faststart -acodec aac -strict experimental -ab 96k {destFile}");
              try
              {
                var procResult = Process.Start(procInfo);
                procResult.WaitForExit();
              }
              catch (Exception ex)
              {
                Console.WriteLine($"Exception Occured: {ex.Message} for file {fileName}.");
              }

              // See if new file is indeed smaller
              var fileSize = new FileInfo(fileName).Length; 
              var newFileSize = new FileInfo(destFile).Length; 
              
              var ratio = newFileSize / (float)fileSize;
              if (ratio < 0.93)
              {
		            var newRatio = (1 - ratio) * 100;
		            Console.WriteLine($"Using shrunk movie file {newRatio}% reduction).");

                // Use new file as 
                fileName = destFile;
              }

              // Get date string from filename
              if (!fileList.ContainsKey(dateKey)) {
                fileList[dateKey] = new List<string>();
              }
              fileList[dateKey].Add(fileName);
            }

            //we init this once so that if the function is repeatedly called
            //it isn't stressing the garbage man

            //retrieves the datetime WITHOUT loading the whole image
            public DateTime GetDateTakenFromImage(string path)
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (Image myImage = Image.FromStream(fs, false, false))
                {
                    PropertyItem propItem = myImage.GetPropertyItem(36867);
                    string dateTaken = r.Replace(Encoding.UTF8.GetString(propItem.Value), "-", 2);
                    return DateTime.Parse(dateTaken);
                }
            }
        }

        static void Main(string[] args)
        {
            var fadService = new FileAndDirectoryService();

            Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(o =>
            {
              if (!Directory.Exists(o.InputDir) || !Directory.Exists(o.OutputDir))
              {
                Console.WriteLine($"Make sure directories exist.");
                return;
              }
              fadService.ProcessDir(o.InputDir, o.OutputDir);              
              fadService.SaveOutput(o.OutputDir);
            });
        }
    }
}
