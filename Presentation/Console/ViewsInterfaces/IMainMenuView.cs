namespace package_delivery_simulator.Presentation.Console.ViewsInterfaces;

public interface IMainMenuView
{
    MenuChoice ShowMenu();
}

public enum MenuChoice
{
    StartSimulation,
    Exit
}
