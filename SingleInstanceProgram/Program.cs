using SingleInstanceProgramNS;

void s_MessageReceived(object? sender, MessageReceivedEventArgs e)
{
    if (e.Message != null)
    {
        PrintStringArray(e.Message);
    }
}

void PrintStringArray(string[] strings)
{
    foreach(string s in strings)
    {
        Console.WriteLine(s);
    }
}

SingleInstanceProgram s = SingleInstanceProgram.GetInstance("UniqueId", args);
PrintStringArray(args);
s.MessageReceived += s_MessageReceived;

Console.ReadLine();
s.MessageReceived -= s_MessageReceived;