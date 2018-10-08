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
                var outputLocationArgument = command.Argument("OutputLocation", "The folder to output split wavs to (with trailing slash)");

                command.OnExecute(() =>
                {
                    var idxLocation = idxLocationArgument.Value ?? throw new Exception("Must specify IDX location (absolute path)");
                    var wavLocation = wavLocationArgument.Value ?? throw new Exception("Must specify WAV location (absolute path)");
                    var outputLocation = outputLocationArgument.Value ?? throw new Exception("Must specify output location (with trailing slash)");

                    var idxData = LoadIdxFile(idxLocation);

                    int i = 0;
                    foreach (var row in idxData)
                    {
                        Console.WriteLine($"Splitting {row.Name}");
                        using (FileStream fileStream = new FileStream(wavLocation, FileMode.Open, FileAccess.Read))
                        {
                            var buffer = new byte[row.Length];
                            fileStream.Seek(row.StartByte, SeekOrigin.Begin);
                            fileStream.Read(buffer, 0, buffer.Length);

                            using (var fs = new FileStream(outputLocation + row.Name, FileMode.Create, FileAccess.Write))
                            {
                                
                                IEnumerable<byte> fmtBytes = StringToByteArray("1000000001000100112B0000112B000001000800");
                                IEnumerable<byte> headerBytes = Encoding.ASCII.GetBytes("RIFF")
                                    .Concat(BitConverter.GetBytes(row.Length))
                                    .Concat(Encoding.ASCII.GetBytes("WAVEfmt "))
                                    .Concat(fmtBytes)
                                    .Concat(Encoding.ASCII.GetBytes("data"))
                                    .Concat(BitConverter.GetBytes(row.Length));
                                fs.Write(headerBytes.ToArray(), 0, headerBytes.Count());
                                fs.Write(buffer, 0, buffer.Length);
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
