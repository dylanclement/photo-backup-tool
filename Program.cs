using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
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
                    } else if (extension == ".mp4" || extension == ".mpg" || extension == ".3gp" || extension == ".avi" || extension == ".mov") {
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
                  DateTime date;

                  if (!DateTime.TryParse(f.Key, out date))
                  {
                    date = DateTime.UnixEpoch;
                  }
                  
                  var year = date.Year.ToString();

                  var destDir = Path.Join(outputDir, Path.Join(year, f.Key));
                  
                  // Make sure directory exists
                  Directory.CreateDirectory(destDir);
                  
                  foreach (var fileName in f.Value)
                  {

                    var destFileName = Path.Join(destDir, Path.GetFileName(fileName));
                    File.Copy(fileName, destFileName);
                    // Set date since we might use this to determine when the video was taken
                    File.SetLastWriteTime(destFileName, date);
                    File.SetCreationTime(destFileName, date);
                  }
                }
            }

            private void AddImage(string f)
            {
                var date = GetDateTakenFromImage(f);
                var dateKey = date.ToString("yyyy-MM-dd");
                Console.WriteLine($"File {f} was taken on {dateKey}");                  
                if (!fileList.ContainsKey(dateKey)) {
                  fileList[dateKey] = new List<string>();
                }
                fileList[dateKey].Add(f);
            }

            private void AddMovie(string fullPath)
            {
              var fileName = Path.GetFileName(fullPath);
              var strDateIndex = fileName.LastIndexOf("20");
              var dateKey = "";
              
              // If the string contains a 20 and it's at least 8 characters long
              if (strDateIndex != -1 && fileName.Length - strDateIndex > 8) 
              {
                dateKey = $"{fileName.Substring(strDateIndex, 4)}-{fileName.Substring(strDateIndex + 4, 2)}-{fileName.Substring(strDateIndex + 6, 2)}";                
              } else {
                dateKey = File.GetLastWriteTime(fullPath).ToString("yyyy-MM-dd");
              }           
              
              Console.WriteLine($"File {fullPath} was taken on {dateKey}");

              // Attempt to shrink movie using ffmpeg, and use new filename if it is smaller
              // Create a temp file with same name in $TMP folder
              var destFile = Path.GetTempPath() + fileName;
              // Delete file if already exists
              File.Delete(destFile);

              // Run ffmpeg to try and shrink the file
              // libx264 = CPU 
              // H264_AMF = GPU
              
              // 264
              //var cmdLineArgs = $"-i \"{fullPath}\" -c:v h264_amf -preset medium -crf 25 -movflags +faststart -acodec aac -strict experimental -ab 96k \"{destFile}\"";

              // 265
              //var cmdLineArgs = $"-i \"{fullPath}\" -c:v libx265 -preset medium -crf 25 -movflags +faststart -acodec aac -strict experimental -ab 96k \"{destFile}\"";
              var cmdLineArgs = $"-i \"{fullPath}\" -c:v hevc_amf -rc cqp -qp_i 26 -qp_p 36 -c:a copy -movflags +faststart  \"{destFile}\"";
              Console.WriteLine($"Executing ffmpeg {cmdLineArgs}");
              
              // Set process info
              var procInfo = new ProcessStartInfo("ffmpeg", cmdLineArgs);
              procInfo.RedirectStandardOutput = true;
              procInfo.UseShellExecute = false;

              // wrap in exception trying to execute external process
              try
              {
                var procResult = Process.Start(procInfo);
                procResult.WaitForExit();
              }
              catch (Exception ex)
              {
                Console.WriteLine($"Exception Occured: {ex.Message} for file {fullPath}.");
              }

              // See if new file is indeed smaller
              var fileSize = new FileInfo(fullPath).Length; 
              var newFileSize = new FileInfo(destFile).Length; 
              
              // Check if the newly produced file is smallere than the
              var ratio = newFileSize / (float)fileSize;
              if (ratio < 0.93)
              {
		            var newRatio = (1 - ratio) * 100;
		            Console.WriteLine($"Using shrunk movie file {newRatio}% reduction).");

                // Use new file as 
                fullPath = destFile;
              }

              // Get date string from filename
              if (!fileList.ContainsKey(dateKey)) {
                fileList[dateKey] = new List<string>();
              }
              fileList[dateKey].Add(fullPath);
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
                    DateTime dateTime;
                    if (DateTime.TryParse(dateTaken, out dateTime))
                    {
                      return dateTime;
                    }
                    Console.WriteLine($"Unable to parse date {dateTaken} for file {path}.");
                    return DateTime.UnixEpoch;
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
