using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yurt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOtpDeliveryStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryStatus",
                table: "PhoneVerifications",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveryStatusAt",
                table: "PhoneVerifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderMessageId",
                table: "PhoneVerifications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhoneVerifications_ProviderMessageId",
                table: "PhoneVerifications",
                column: "ProviderMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_OtpEvents_ProviderMessageId",
                table: "OtpEvents",
                column: "ProviderMessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PhoneVerifications_ProviderMessageId",
                table: "PhoneVerifications");

            migrationBuilder.DropIndex(
                name: "IX_OtpEvents_ProviderMessageId",
                table: "OtpEvents");

            migrationBuilder.DropColumn(
                name: "DeliveryStatus",
                table: "PhoneVerifications");

            migrationBuilder.DropColumn(
                name: "DeliveryStatusAt",
                table: "PhoneVerifications");

            migrationBuilder.DropColumn(
                name: "ProviderMessageId",
                table: "PhoneVerifications");
        }
    }
}
