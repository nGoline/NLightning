namespace NLightning.Client.Printers;

public interface IPrinter<in T>
{
    void Print(T item);
}