using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace poker_web_api.Migrations
{
    /// <inheritdoc />
    public partial class AddBuyInAmountToGame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BuyInAmount",
                table: "Games",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuyInAmount",
                table: "Games");
        }
    }
}
