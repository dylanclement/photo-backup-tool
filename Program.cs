using System;
using System.Collections.Generic;
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
                    } if (extension == ".mp4") {
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

            private void AddMovie(string f)
            {
                throw new NotImplementedException();
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
              fadService.ProcessDir(o.InputDir, o.OutputDir);
            });
        }
    }
}
