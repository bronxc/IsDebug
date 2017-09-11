using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace IsDebug
{
    internal class Program
    {
        public enum MachineType : ushort
        {
            IMAGE_FILE_MACHINE_UNKNOWN = 0x0,
            IMAGE_FILE_MACHINE_AM33 = 0x1d3,
            IMAGE_FILE_MACHINE_AMD64 = 0x8664,
            IMAGE_FILE_MACHINE_ARM = 0x1c0,
            IMAGE_FILE_MACHINE_EBC = 0xebc,
            IMAGE_FILE_MACHINE_I386 = 0x14c,
            IMAGE_FILE_MACHINE_IA64 = 0x200,
            IMAGE_FILE_MACHINE_M32R = 0x9041,
            IMAGE_FILE_MACHINE_MIPS16 = 0x266,
            IMAGE_FILE_MACHINE_MIPSFPU = 0x366,
            IMAGE_FILE_MACHINE_MIPSFPU16 = 0x466,
            IMAGE_FILE_MACHINE_POWERPC = 0x1f0,
            IMAGE_FILE_MACHINE_POWERPCFP = 0x1f1,
            IMAGE_FILE_MACHINE_R4000 = 0x166,
            IMAGE_FILE_MACHINE_SH3 = 0x1a2,
            IMAGE_FILE_MACHINE_SH3DSP = 0x1a3,
            IMAGE_FILE_MACHINE_SH4 = 0x1a6,
            IMAGE_FILE_MACHINE_SH5 = 0x1a8,
            IMAGE_FILE_MACHINE_THUMB = 0x1c2,
            IMAGE_FILE_MACHINE_WCEMIPSV2 = 0x169,
        }

        private static int Main(string[] args)
        {
            var returnValue = false;

            if (args.Length == 0 || args.Length > 2)
            {
                PrintUsage();
                return Convert.ToInt16(returnValue);
            }


            var fileName = args[0];
            var beVerbose = !(args.Length == 2 && args[1] == "-S");


            if (File.Exists(fileName))
            {
                fileName = Path.GetFullPath(fileName);
                if (beVerbose)
                {
                    Console.WriteLine("");
                    Console.WriteLine("File          : {0}", Path.GetFileName(fileName));
                    Console.WriteLine("Path          : {0}", Path.GetDirectoryName(fileName));

                    var ver = FileVersionInfo.GetVersionInfo(fileName);
                    Console.WriteLine("File Version  : {0}", ver.FileVersion);

                    var linkDateTime = GetLinkerTimeStamp(fileName);
                    Console.WriteLine("Built on      : {0:G}", linkDateTime);


                }


                if (!IsDotNet(fileName, beVerbose))
                {
                    using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                    {
                        using (BinaryReader br = new BinaryReader(fs))
                        {
                            fs.Seek(0x3c, SeekOrigin.Begin);
                            Int32 peOffset = br.ReadInt32();
                            fs.Seek(peOffset, SeekOrigin.Begin);
                            UInt32 peHead = br.ReadUInt32();
                            bool hasPEHeader = (peHead == 0x00004550);
                            Console.WriteLine($"Has PE Header : {hasPEHeader}");

                            if (hasPEHeader)
                            {
                                MachineType mt = (MachineType)(br.ReadUInt16());
                                string machineType = "Unknown";

                                switch (mt)
                                {
                                    case MachineType.IMAGE_FILE_MACHINE_AMD64:
                                        machineType = "AMD64";
                                        break;
                                    case MachineType.IMAGE_FILE_MACHINE_I386:
                                        machineType = "i386";
                                        break;
                                    case MachineType.IMAGE_FILE_MACHINE_IA64:
                                        machineType = "IA64";
                                        break;

                                    default:
                                        machineType = mt.ToString();
                                        break;
                                }
                                Console.WriteLine($"Machine Type  : {machineType}");




                            }
                        }
                    }
                }
                else
                {
                    if (beVerbose)
                    {


                        var assembly = Assembly.ReflectionOnlyLoadFrom(fileName);
                        Console.WriteLine($"CLR Version   : {assembly.ImageRuntimeVersion}");



                    }

                    var ass = Assembly.LoadFile(fileName);
                    foreach (var att in ass.GetCustomAttributes(false))
                        if (att.GetType() == Type.GetType("System.Diagnostics.DebuggableAttribute"))
                        {
                            var typedAttribute = (DebuggableAttribute)att;


                            var debugOuput =
                                (typedAttribute.DebuggingFlags & DebuggableAttribute.DebuggingModes.Default)
                                != DebuggableAttribute.DebuggingModes.None
                                    ? "Full"
                                    : "pdb-only";

                            //returnValue = typedAttribute.IsJITOptimizerDisabled;

                            if (beVerbose)
                            {

                                Console.WriteLine("Debuggable    : {0}",
                                    (typedAttribute.DebuggingFlags & DebuggableAttribute.DebuggingModes.Default) ==
                                    DebuggableAttribute.DebuggingModes.Default);
                                Console.WriteLine("JIT Optimized : {0}", !typedAttribute.IsJITOptimizerDisabled);
                                Console.WriteLine("Debug Output  : {0}", debugOuput);
                            }


                            returnValue = typedAttribute.IsJITOptimizerDisabled;
                        }
                    PortableExecutableKinds peKind;
                    ImageFileMachine imageFileMachine;
                    ass.ManifestModule.GetPEKind(out peKind, out imageFileMachine);


                    if (beVerbose)
                    {
                        Console.WriteLine("PE Type       : {0}", peKind);
                        Console.WriteLine("Machine       : {0}", imageFileMachine);
                        Console.WriteLine("");
                    }
                }
            }


            return Convert.ToInt16(returnValue);
        }


        private static void PrintUsage()
        {
            Console.WriteLine("");
            Console.WriteLine("IsDebug: .NET Debug version checker.");
            Console.WriteLine("USAGE  :  IsDebug fileName [-S]");
            Console.WriteLine("          filename = file to check");
            Console.WriteLine("          -S Silent (no messages, just returns true/false)");
            Console.WriteLine("Returns 1 if debug, otherwise 0");
            Console.WriteLine("");
        }

        private static bool IsDotNet(string fileName, bool doSpew)
        {
            var returnValue = false;

            try
            {
                Assembly.LoadFile(fileName);
                returnValue = true;
            }
            catch (BadImageFormatException bif)
            {

                bif = null;//make the compiler happy
                //if (doSpew)
                //    Console.WriteLine(
                //        $"BadImageFormatException, {fileName} has the wrong format or is not a .net assembly.");

                //It's not a .net assembly, or wrong format, so we'll just return false

            }
            catch (Exception e)
            {
                if (doSpew) Console.WriteLine($"Error loading {Path.GetFileName(fileName)}:{e.Message}" );
            }

            if (doSpew)
            {
                Console.WriteLine($".NET Assembly : {returnValue}");
            }
            return returnValue;
        }


        private static DateTime GetLinkerTimeStamp(string filePath)
        {
            const int peHeaderOffset = 60;
            const int linkerTimestampOffset = 8;
            var b = new byte[2048];
            FileStream s = null;
            try
            {
                s = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                s.Read(b, 0, 2048);
            }
            finally
            {
                s?.Close();
            }
            var dt =
                new DateTime(1970, 1, 1, 0, 0, 0).AddSeconds(BitConverter.ToInt32(b,
                    BitConverter.ToInt32(b, peHeaderOffset) + linkerTimestampOffset));
            return dt.AddHours(TimeZone.CurrentTimeZone.GetUtcOffset(dt).Hours);
        }
    }
}