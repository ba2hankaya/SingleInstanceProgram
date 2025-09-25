using System.Diagnostics;

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
         * KNOWN ISSUE: When running the tests, sometimes the test will hang. The issue is not consistent therefore I think it is about the process creation in the tests rather than the class itself. Still working on it.
         */

        [TestMethod]
        public void ListenProcessRespond2Instances()
        {
            ClearProcessByName("ListenAndRespond");
            Process firstInstance = StartProcess("ListenAndRespond", MultipleInstancesGenerationHelper.GenerateArgumentString(argName: "arg", argCount: 3, instanceNumber: 0));
            Process secondInstance = StartProcess("ListenAndRespond", MultipleInstancesGenerationHelper.GenerateArgumentString(argName: "arg", argCount:3, instanceNumber: 1));
            string expectedFirst = MultipleInstancesGenerationHelper.ConstructExpectedByFirstString(argName: "arg", firstInstanceArgCount: 3, secondaryInstanceCount:1 ,secondaryInstanceArgCount: 3);
            string expectedSecond = MultipleInstancesGenerationHelper.ConstructExpectedBySecondString(argName: "arg", argCount: 3, instanceNumber: 1);

            secondInstance.WaitForExit();

            firstInstance.StandardInput.WriteLine("exit");
            string receivedFirst = ReadOutput(firstInstance);
            firstInstance.Dispose();

            string receivedSecond = ReadOutput(secondInstance);
            secondInstance.Dispose();

            Assert.AreEqual(expectedFirst, receivedFirst);
            Assert.AreEqual(expectedSecond, receivedSecond);
        }

        [TestMethod]
        public void ListenProcessRespond10Instances()
        {
            ClearProcessByName("ListenAndRespond");
            Process firstInstance = StartProcess("ListenAndRespond", MultipleInstancesGenerationHelper.GenerateArgumentString(argName: "arg", argCount:3, instanceNumber: 0));
            List<string> secondaryOutputs = new();
            for (int i = 0; i < 10; i++)
            {
                Process secondaryInstance = StartProcess("ListenAndRespond", MultipleInstancesGenerationHelper.GenerateArgumentString(argName: "arg", argCount:5, instanceNumber: i+1));
                if (firstInstance.HasExited) { break; }
                secondaryInstance.WaitForExit(); //wait for process to complete because otherwise n+1 th process can run before the nth process because of process creation time.
                secondaryOutputs.Add(ReadOutput(secondaryInstance));
                secondaryInstance.Dispose();
            }

            firstInstance.StandardInput.WriteLine("exit");
            string receivedFirst = ReadOutput(firstInstance);
            firstInstance.Dispose();

            string expectedFirst = MultipleInstancesGenerationHelper.ConstructExpectedByFirstString(argName: "arg", firstInstanceArgCount: 3, secondaryInstanceCount: 10, secondaryInstanceArgCount: 5, firstInstanceId: 0);

            Assert.AreEqual(expectedFirst, receivedFirst);
            for(int i = 0; i < 10; i++)
            {
                string receivedSecondary = secondaryOutputs[i];
                Assert.AreEqual(MultipleInstancesGenerationHelper.ConstructExpectedBySecondString(argName: "arg", argCount: 5, instanceNumber: i+1), receivedSecondary);
            }
        }
    }
    
    public static class MultipleInstancesGenerationHelper
    {
        //Assumes the secondary instances will start with ID 1 and appear in order increasingly
        public static string ConstructExpectedByFirstString(string argName, int firstInstanceArgCount, int secondaryInstanceCount = -1, int secondaryInstanceArgCount = -1, int firstInstanceId = 0)
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

        public static string ConstructExpectedBySecondString(string argName, int argCount, int instanceNumber)
        {
            string result = "";

            for (int i = 0; i < argCount; i++)
            {
                result += $"I{instanceNumber}_{argName}{i.ToString()} processed by first processed by second\r\n";
            }

            return result;
        }

        public static string GenerateArgumentString(string argName, int argCount, int instanceNumber)
        {
            List<string> result = new();
            for (int i = 0; i < argCount; i++)
            {
                result.Add($"I{instanceNumber}_{argName}{i.ToString()}");
            }

            return String.Join(" ", result);
        }

        
    }
}
