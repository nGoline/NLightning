namespace NLightning.Daemon.Contracts.Utilities;

public static class ConsoleUtils
{
    public static string ReadPassword(string prompt = "Enter password: ")
    {
        Console.Write(prompt);
        var password = string.Empty;

        do
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
                break;
            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password = password[..^1];
                Console.Write("\b \b");
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password += key.KeyChar;
                Console.Write('*');
            }
        } while (true);

        Console.WriteLine();
        return password;
    }
}