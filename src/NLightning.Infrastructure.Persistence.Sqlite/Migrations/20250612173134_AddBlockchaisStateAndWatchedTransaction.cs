using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NLightning.Infrastructure.Persistence.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddBlockchaisStateAndWatchedTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "PeerEntityNodeId",
                table: "Channels",
                type: "BLOB",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BlockchainStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LastProcessedHeight = table.Column<uint>(type: "INTEGER", nullable: false),
                    LastProcessedBlockHash = table.Column<byte[]>(type: "BLOB", nullable: false),
                    LastProcessedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockchainStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Peers",
                columns: table => new
                {
                    NodeId = table.Column<byte[]>(type: "BLOB", nullable: false),
                    Host = table.Column<string>(type: "TEXT", nullable: false),
                    Port = table.Column<uint>(type: "INTEGER", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Peers", x => x.NodeId);
                });

            migrationBuilder.CreateTable(
                name: "WatchedTransactions",
                columns: table => new
                {
                    TransactionId = table.Column<byte[]>(type: "BLOB", nullable: false),
                    ChannelId = table.Column<byte[]>(type: "BLOB", nullable: false),
                    RequiredDepth = table.Column<uint>(type: "INTEGER", nullable: false),
                    FirstSeenAtHeight = table.Column<uint>(type: "INTEGER", nullable: true),
                    TransactionIndex = table.Column<ushort>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchedTransactions", x => x.TransactionId);
                    table.ForeignKey(
                        name: "FK_WatchedTransactions_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "ChannelId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Channels_PeerEntityNodeId",
                table: "Channels",
                column: "PeerEntityNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchedTransactions_ChannelId",
                table: "WatchedTransactions",
                column: "ChannelId");

            migrationBuilder.Sql("PRAGMA foreign_keys = 0;", suppressTransaction: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Channels_Peers_PeerEntityNodeId",
                table: "Channels",
                column: "PeerEntityNodeId",
                principalTable: "Peers",
                principalColumn: "NodeId");

            migrationBuilder.Sql("PRAGMA foreign_keys = 1;", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Channels_Peers_PeerEntityNodeId",
                table: "Channels");

            migrationBuilder.DropTable(
                name: "BlockchainStates");

            migrationBuilder.DropTable(
                name: "Peers");

            migrationBuilder.DropTable(
                name: "WatchedTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Channels_PeerEntityNodeId",
                table: "Channels");

            migrationBuilder.DropColumn(
                name: "PeerEntityNodeId",
                table: "Channels");
        }
    }
}