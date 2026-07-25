using System;
using System.Threading.Tasks;

namespace ModTogetherUniversal
{
    public class TestRunnerConsole
    {
        public static async Task RunConsoleTestsAsync()
        {
            await RunAllTests.RunVerificationAsync();
        }
    }
}
