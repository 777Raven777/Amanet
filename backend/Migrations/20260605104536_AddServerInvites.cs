using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddServerInvites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatorId",
                table: "Servers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                table: "Roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ServerInvites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviterId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitedUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServerInvites_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServerInvites_Users_InvitedUserId",
                        column: x => x.InvitedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServerInvites_Users_InviterId",
                        column: x => x.InviterId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Servers_CreatorId",
                table: "Servers",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_ServerInvites_InvitedUserId",
                table: "ServerInvites",
                column: "InvitedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ServerInvites_InviterId",
                table: "ServerInvites",
                column: "InviterId");

            migrationBuilder.CreateIndex(
                name: "IX_ServerInvites_ServerId",
                table: "ServerInvites",
                column: "ServerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Servers_Users_CreatorId",
                table: "Servers",
                column: "CreatorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Servers_Users_CreatorId",
                table: "Servers");

            migrationBuilder.DropTable(
                name: "ServerInvites");

            migrationBuilder.DropIndex(
                name: "IX_Servers_CreatorId",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "CreatorId",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "IsSystem",
                table: "Roles");
        }
    }
}
