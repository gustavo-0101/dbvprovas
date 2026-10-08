namespace Dbvprovas.App;

// Endereço da API no Android (RNF-TEN-004). Em Debug, o emulador alcança a máquina de dev em 10.0.2.2.
internal static class ApiAddress
{
#if DEBUG
    public static Uri Current { get; } = new("http://10.0.2.2:5080/");
#else
    // Ainda não há ambiente hospedado (D-113); o endereço real chega no portão do piloto.
    public static Uri Current { get; } = new("https://api.invalid/");
#endif
}
