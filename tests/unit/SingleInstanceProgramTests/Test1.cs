using System.Diagnostics;
using System.Threading.Tasks;

namespace SingleInstanceProgramTests
{
    [TestClass]
    public sealed class SingleInstanceProgramTests
    {
        const string AppNamePlaceholder = "{APPNAME}";
        const string Path = @$"..\..\..\..\AppSample_{AppNamePlaceholder}\bin\Debug\net8.0\{AppNamePlaceholder}.exe";

        
        static Process StartProcess(string sampleAppName, string arguments)
        {
            //* Create your Process
            Process process = new Process();
            string path = Path.Replace(AppNamePlaceholder, sampleAppName);
            process.StartInfo.FileName = path;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();
            return process;
        }

        static string ReadOutput(Process process)
        {
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return stdout + stderr;
        }
        static void ClearProcessByName(string processName)
        {
            foreach (var p in Process.GetProcessesByName(processName))
            {
                try { p.Kill(); p.WaitForExit(); } catch { throw; }
            }
        }

        /*
         * FIXED ISSUE: When running the tests, sometimes the test will hang. The issue is not consistent therefore I think it is about the process creation in the tests rather than the class itself.
         * What the issue was: StartProcess gives the command to start a process but doesn't wait until the process starts. So if two processes were started consecutively, sometimes the second process would
         * start before the first one, becoming the first instance. And since the secondary process doesn't receive the exit code, it would then run forever.
         * Fix: Added a small delay after creating the first process in the test cases to ensure the process starts before the next line of code is executed.
         * IMPORTANT: Same argname should be used for both GenerateXInstanceArgsAndExpected methods for correct results.
         */

        [TestMethod]
        public async Task ListenProcessRespond2Instances()
        {
            ClearProcessByName("ListenAndRespond");
            string argName = "arg";
            KeyValuePair<string, string> firstInstanceIO = MultipleInstancesGenerationHelper.GenerateFirstInstanceArgsAndExpected(argName, firstInstanceArgCount: 3, secondaryInstanceCount: 1, secondaryInstanceArgCount: 3);
            KeyValuePair<string, string> secondInstanceIO = MultipleInstancesGenerationHelper.GenerateSecondInstanceArgsAndExpected(argName, argCount: 3, instanceNumber: 1);
            Process firstInstance = StartProcess("ListenAndRespond", firstInstanceIO.Key);
            await Task.Delay(100);
            Process secondInstance = StartProcess("ListenAndRespond", secondInstanceIO.Key);

            secondInstance.WaitForExit();

            firstInstance.StandardInput.WriteLine("exit");
            string receivedFirst = ReadOutput(firstInstance);
            firstInstance.WaitForExit();
            firstInstance.Dispose();

            string receivedSecond = ReadOutput(secondInstance);
            secondInstance.Dispose();

            Assert.AreEqual(firstInstanceIO.Value, receivedFirst);
            Assert.AreEqual(secondInstanceIO.Value, receivedSecond);
        }

        [TestMethod]
        public async Task ListenProcessRespond10Instances()
        {
            ClearProcessByName("ListenAndRespond");

            string argName = "arg";

            KeyValuePair<string, string> firstInstanceIO = MultipleInstancesGenerationHelper.GenerateFirstInstanceArgsAndExpected(argName, firstInstanceArgCount: 3, secondaryInstanceCount: 10, secondaryInstanceArgCount: 5, firstInstanceId: 0);
            Process firstInstance = StartProcess("ListenAndRespond", firstInstanceIO.Key);
            await Task.Delay(100);
            for (int i = 0; i < 10; i++)
            {
                KeyValuePair<string, string> secondaryInstanceIO = MultipleInstancesGenerationHelper.GenerateSecondInstanceArgsAndExpected(argName, argCount: 5, instanceNumber: i + 1);
                Process secondaryInstance = StartProcess("ListenAndRespond", secondaryInstanceIO.Key);
                if (firstInstance.HasExited) { break; }
                secondaryInstance.WaitForExit(); //wait for process to complete because otherwise n+1 th process can run before the nth process because of process creation time.

                Assert.AreEqual(ReadOutput(secondaryInstance), secondaryInstanceIO.Value);
                secondaryInstance.Dispose();
            }

            firstInstance.StandardInput.WriteLine("exit");
            string receivedFirst = ReadOutput(firstInstance);
            firstInstance.WaitForExit();
            firstInstance.Dispose();

            string expectedFirst = firstInstanceIO.Value;

            Assert.AreEqual(expectedFirst, receivedFirst);
        }
    }
    
    public static class MultipleInstancesGenerationHelper
    {
        //Assumes the secondary instances will start with ID 1 and appear in order increasingly
        private static string ConstructExpectedByFirstString(string argName, int firstInstanceArgCount, int secondaryInstanceCount = -1, int secondaryInstanceArgCount = -1, int firstInstanceId = 0)
        {
            const string InstNoPH = "{instancenum}";
            const string ArgNamePH = "{argname}";
            const string ArgNoPH = "{argnum}";
            const string ExpectedFirstInstanceOutputStartTemplate = "This is the first instance of the application.\r\n";
            const string ExpectedFirstInstanceOutputBodyTemplate = "I{instancenum}_{argname}{argnum} processed by first\r\n";


            string result = "";
            result += ExpectedFirstInstanceOutputStartTemplate;

            for (int i = 0; i < firstInstanceArgCount; i++)
            {
                result += ExpectedFirstInstanceOutputBodyTemplate.Replace(InstNoPH, firstInstanceId.ToString()).Replace(ArgNamePH, argName).Replace(ArgNoPH, i.ToString());
            }

            if (secondaryInstanceArgCount > -1 && secondaryInstanceArgCount > 0)
            {
                string bodyTemplate = "";
                for (int i = 0; i < secondaryInstanceArgCount; i++)
                {
                    bodyTemplate += ExpectedFirstInstanceOutputBodyTemplate.Replace(ArgNamePH, argName).Replace(ArgNoPH, i.ToString());
                }

                for (int i = 0; i < secondaryInstanceCount; i++)
                {
                    result += bodyTemplate.Replace(InstNoPH, (i + 1).ToString());
                }
            }

            return result;
        }

        private static string ConstructExpectedBySecondString(string argName, int argCount, int instanceNumber)
        {
            string result = "";

            for (int i = 0; i < argCount; i++)
            {
                result += $"I{instanceNumber}_{argName}{i.ToString()} processed by first processed by second\r\n";
            }

            return result;
        }

        private static string GenerateArgumentString(string argName, int argCount, int instanceNumber)
        {
            List<string> result = new();
            for (int i = 0; i < argCount; i++)
            {
                result.Add($"I{instanceNumber}_{argName}{i.ToString()}");
            }

            return String.Join(" ", result);
        }

        public static KeyValuePair<string , string> GenerateFirstInstanceArgsAndExpected(string argName, int firstInstanceArgCount, int secondaryInstanceCount = -1, int secondaryInstanceArgCount = -1, int firstInstanceId = 0)
        {
            string args = GenerateArgumentString(argName, firstInstanceArgCount, firstInstanceId);
            string expected = ConstructExpectedByFirstString(argName, firstInstanceArgCount, secondaryInstanceCount, secondaryInstanceArgCount, firstInstanceId);
            return new KeyValuePair<string, string>(args, expected);
        }

        public static KeyValuePair<string, string> GenerateSecondInstanceArgsAndExpected(string argName, int argCount, int instanceNumber)
        {
            string args = GenerateArgumentString(argName, argCount, instanceNumber);
            string expected = ConstructExpectedBySecondString(argName, argCount, instanceNumber);
            return new KeyValuePair<string, string>(args,expected);
        }
        
    }
}
