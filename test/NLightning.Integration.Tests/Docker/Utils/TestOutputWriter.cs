using System.Text;

namespace NLightning.Integration.Tests.Docker.Utils;

public class TestOutputWriter(ITestOutputHelper output) : TextWriter
{
    private readonly ITestOutputHelper _output = output;

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char[] buffer, int index, int count)
    {
        try
        {
            _output.WriteLine(new string(buffer, index, count));
        }
        catch
        {
            // Do nothing
        }
    }
}