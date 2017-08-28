using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace IsDebug
{
    internal class Program
    {
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


                if (IsDotNet(fileName, beVerbose))
                {
                    if (beVerbose)
                    {
                        

                        var assembly = Assembly.ReflectionOnlyLoadFrom(fileName);
                        Console.WriteLine("CLR Version   : {0}", assembly.ImageRuntimeVersion);


                    }

                    var ass = Assembly.LoadFile(fileName);
                    foreach (var att in ass.GetCustomAttributes(false))
                        if (att.GetType() == Type.GetType("System.Diagnostics.DebuggableAttribute"))
                        {
                            var typedAttribute = (DebuggableAttribute) att;


                            var debugOuput = (typedAttribute.DebuggingFlags & DebuggableAttribute.DebuggingModes.Default)
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
                //if (doSpew)
                //    Console.WriteLine(
                //        $"BadImageFormatException, {fileName} has the wrong format or is not a .net assembly.");
                     
            }
            catch (Exception e)
            {
                if (doSpew) Console.WriteLine("Error loading {0}:{1}", Path.GetFileName(fileName), e.Message);
            }

            if (doSpew)
            {
                Console.WriteLine($".NET Assembly: {returnValue}");
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