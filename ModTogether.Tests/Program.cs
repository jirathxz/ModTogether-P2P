using System;
using System.Threading.Tasks;
using ModTogetherUniversal;

namespace ModTogether.Tests
{
    class Program
    {
        static async Task Main(string[] args)
        {
            bool success = true;
            success &= await RunAllTests.RunVerificationAsync();
            Console.WriteLine();
            success &= TestMhWildsPakInstaller.RunTest();
            Console.WriteLine();
            success &= TestMhwModInstaller.RunTest();
            Console.WriteLine();
            success &= TestFileHelperBug.RunTest();

            if (!success)
            {
                Environment.Exit(1);
            }
        }
    }
}
