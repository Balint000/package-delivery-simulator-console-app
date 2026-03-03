namespace package_delivery_simulator.Infrastructure.Configuration;

public class AppSettings
{
    public int TickDurationMs { get; set; } = 500;
    public int MaxCouriers { get; set; } = 10;
    public int MaxOrders { get; set; } = 50;
    public int SimulationDurationSeconds { get; set; } = 60;
}
