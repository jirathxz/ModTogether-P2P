using System;
using System.Threading.Tasks;
using ModTogetherUniversal;

namespace ModTogether.Tests
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await RunAllTests.RunVerificationAsync();
            Console.WriteLine();
            TestMhWildsPakInstaller.RunTest();
            Console.WriteLine();
            TestMhwModInstaller.RunTest();
        }
    }
}
