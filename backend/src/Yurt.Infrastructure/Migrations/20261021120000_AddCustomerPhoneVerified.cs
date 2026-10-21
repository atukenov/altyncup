using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Yurt.Infrastructure.Persistence;

#nullable disable

namespace Yurt.Infrastructure.Migrations
{
    /// <summary>Adds the customer phone-verified flag and backfills it. Successful OTP rows are
    /// deleted from PhoneVerifications, so the only surviving evidence of a past verification is
    /// signup time: since AddPhoneVerification (20260910182352) an account is only created after
    /// its registration code is confirmed, so every customer created from then on proved their
    /// number. Older accounts registered without a code and stay unverified until they confirm
    /// one from the profile page.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261021120000_AddCustomerPhoneVerified")]
    public partial class AddCustomerPhoneVerified : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPhoneVerified",
                table: "CustomerUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PhoneVerifiedAt",
                table: "CustomerUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "CustomerUsers"
                SET "IsPhoneVerified" = TRUE, "PhoneVerifiedAt" = "CreatedAt"
                WHERE "CreatedAt" >= TIMESTAMPTZ '2026-09-10 18:23:52+00';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPhoneVerified",
                table: "CustomerUsers");

            migrationBuilder.DropColumn(
                name: "PhoneVerifiedAt",
                table: "CustomerUsers");
        }
    }
}
