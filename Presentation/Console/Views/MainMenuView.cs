using package_delivery_simulator.Presentation.Console.ViewsInterfaces;

namespace package_delivery_simulator.Presentation.Console.Views;

public class MainMenuView : IMainMenuView
{
    public MenuChoice ShowMenu()
    {
        System.Console.Clear();
        System.Console.WriteLine("╔═══════════════════════════════════════════╗");
        System.Console.WriteLine("║   Csomagkézbesítés Szimuláció             ║");
        System.Console.WriteLine("╚═══════════════════════════════════════════╝");
        System.Console.WriteLine();
        System.Console.WriteLine("1. Szimuláció indítása");
        System.Console.WriteLine("2. Kilépés");
        System.Console.WriteLine();
        System.Console.Write("Választás: ");

        var input = System.Console.ReadLine();
        return input == "1" ? MenuChoice.StartSimulation : MenuChoice.Exit;
    }
}
