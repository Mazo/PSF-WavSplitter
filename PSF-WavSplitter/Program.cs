using System;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.CommandLineUtils;

namespace PSF_WavSplitter
{
    class Program
    {
        static void Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "PSF-WavSplitter",
                Description = "Manages Planetside 1 WAV files using an IDX file"
            };

            app.HelpOption("-?|-h|--help");
            app.VersionOption("-v|--version", () => string.Format("Version {0}", Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion));

            app.Command("split", (command) =>
            {
                command.Description = "Split a WAV file using an existing IDX file";
                command.HelpOption("-?|-h|--help");

                var idxLocationArgument = command.Argument("IDXLocation", "The full path of the IDX file to use");
                var wavLocationArgument = command.Argument("WAVLocation", "The full path of the WAV file to use");
                var outputLocationArgument = command.Argument("OutputLocation", "The folder to output split wavs to (with trailing slash - folder must exist)");

                command.OnExecute(() =>
                {
                    var idxLocation = idxLocationArgument.Value ?? throw new Exception("Must specify IDX location (absolute path)");
                    var wavLocation = wavLocationArgument.Value ?? throw new Exception("Must specify WAV location (absolute path)");
                    var outputLocation = outputLocationArgument.Value ?? throw new Exception("Must specify output location (with trailing slash - folder must exist)");

                    var idxData = LoadIdxFile(idxLocation);

                    // For anyone that's curious, this site was extremely helpful with working out the format of the wav file, even though it isn't an exact match.
                    // https://blogs.msdn.microsoft.com/dawate/2009/06/23/intro-to-audio-programming-part-2-demystifying-the-wav-format/

                    // Pull out the FMT header bytes from the original file to reuse for each new wav
                    IEnumerable<byte> fmtHeaderBytes;
                    using (FileStream fileStream = new FileStream(wavLocation, FileMode.Open, FileAccess.Read))
                    {
                        fileStream.Seek(16, SeekOrigin.Begin); // Seeking forward 16 bytes leaves us right at the end of the beginning "fmt " header
                        var buffer = new byte[24]; // 24 bytes is just enough to get the fmt headers and to the end of the "data" marker
                        fileStream.Read(buffer, 0, buffer.Length);
                        fmtHeaderBytes = buffer;
                        fileStream.Close();
                    }

                    int i = 0;
                    foreach (var row in idxData)
                    {
                        Console.WriteLine($"Splitting {row.Name}");
                        using (FileStream fileStream = new FileStream(wavLocation, FileMode.Open, FileAccess.Read))
                        {
                            var buffer = new byte[row.Length];
                            var start = i == 0 ? row.StartByte + 38 : row.StartByte; // If this is the first wav written we need to skip over the pre-existing header or it'll be duplicated.
                            fileStream.Seek(start, SeekOrigin.Begin);
                            fileStream.Read(buffer, 0, buffer.Length);

                            using (var fs = new FileStream(outputLocation + row.Name, FileMode.Create, FileAccess.Write))
                            {
                                
                                IEnumerable<byte> headerBytes = Encoding.ASCII.GetBytes("RIFF")
                                    .Concat(BitConverter.GetBytes(row.Length - 8)) // Remove 8 bytes as RIFF & WAVE headers shouldn't be included in total size
                                    .Concat(Encoding.ASCII.GetBytes("WAVEfmt "))
                                    .Concat(fmtHeaderBytes);

                                var headerBytesCount = headerBytes.Count();

                                headerBytes = headerBytes.Concat(BitConverter.GetBytes(row.Length - headerBytesCount - 4)); // Remove the existing header length (including this itself) from the total bytes to get the actual data length

                                fs.Write(headerBytes.ToArray(), 0, headerBytes.Count()); // Write out the headers
                                fs.Write(buffer, 0, buffer.Length); // Write the rest of the data
                                fs.Close();
                            }

                            fileStream.Close();
                        }

                        i++;
                    }

                    return 0;
                });
            });

            app.Execute(args);
            Console.WriteLine("Done. Press any key to exit.");
            Console.ReadKey();
        }

        public static byte[] StringToByteArray(String hex)
        {
            int numberChars = hex.Length;
            byte[] bytes = new byte[numberChars / 2];
            for (int i = 0; i < numberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        static List<IdxDataRow> LoadIdxFile(string idxPath)
        {
            var rows = new List<IdxDataRow>();
            using (StreamReader sr = new StreamReader(idxPath))
            {
                while (sr.Peek() >= 0)
                {
                    var str = sr.ReadLine();
                    var strArray = str.Split(' ');

                    rows.Add(new IdxDataRow()
                    {
                        Name = strArray[0],
                        StartByte = uint.Parse(strArray[1]),
                        Length = uint.Parse(strArray[2]),
                        Hertz = uint.Parse(strArray[3]),
                        Bits = uint.Parse(strArray[4])
                    });
                }
            }

            return rows;
        }
    }

    class IdxDataRow
    {
        public string Name { get; set; }
        public uint StartByte { get; set; }
        public uint Length { get; set; }
        public uint Hertz { get; set; }
        public uint Bits { get; set; }
    }
}
