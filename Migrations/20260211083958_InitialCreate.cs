using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace package_delivery_simulator.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Couriers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    CurrentLocationX = table.Column<double>(type: "REAL", nullable: false),
                    CurrentLocationY = table.Column<double>(type: "REAL", nullable: false),
                    IsAvailable = table.Column<bool>(type: "INTEGER", nullable: false),
                    CompletedDeliveries = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalDistanceTraveled = table.Column<double>(type: "REAL", nullable: false),
                    TotalDelayMinutes = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Couriers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Zones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    CenterX = table.Column<double>(type: "REAL", nullable: false),
                    CenterY = table.Column<double>(type: "REAL", nullable: false),
                    CurrentLoad = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoutePlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CourierId = table.Column<int>(type: "INTEGER", nullable: false),
                    OptimizedOrderSequence = table.Column<string>(type: "TEXT", nullable: false),
                    EstimatedTotalMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutePlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoutePlans_Couriers_CourierId",
                        column: x => x.CourierId,
                        principalTable: "Couriers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DestinationAddress = table.Column<string>(type: "TEXT", nullable: false),
                    DestX = table.Column<double>(type: "REAL", nullable: false),
                    DestY = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Deadline = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeliveredAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ZoneId = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedCourierId = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    WasDelayNotificationSent = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryOrders_Couriers_AssignedCourierId",
                        column: x => x.AssignedCourierId,
                        principalTable: "Couriers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DeliveryOrders_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StatusHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeliveryOrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    NewStatus = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatusHistories_DeliveryOrders_DeliveryOrderId",
                        column: x => x.DeliveryOrderId,
                        principalTable: "DeliveryOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Couriers_IsAvailable",
                table: "Couriers",
                column: "IsAvailable");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryOrders_AssignedCourierId",
                table: "DeliveryOrders",
                column: "AssignedCourierId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryOrders_Deadline",
                table: "DeliveryOrders",
                column: "Deadline");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryOrders_Status",
                table: "DeliveryOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryOrders_ZoneId",
                table: "DeliveryOrders",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_RoutePlans_CourierId",
                table: "RoutePlans",
                column: "CourierId");

            migrationBuilder.CreateIndex(
                name: "IX_StatusHistories_DeliveryOrderId",
                table: "StatusHistories",
                column: "DeliveryOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_StatusHistories_Timestamp",
                table: "StatusHistories",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Zones_Name",
                table: "Zones",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoutePlans");

            migrationBuilder.DropTable(
                name: "StatusHistories");

            migrationBuilder.DropTable(
                name: "DeliveryOrders");

            migrationBuilder.DropTable(
                name: "Couriers");

            migrationBuilder.DropTable(
                name: "Zones");
        }
    }
}
