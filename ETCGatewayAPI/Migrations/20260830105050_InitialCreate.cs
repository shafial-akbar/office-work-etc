using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ETCGatewayAPI.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TBL_ApiTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: false),
                    Expiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TBL_ApiTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TBL_ApiUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TBL_ApiUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TBL_Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FatherName = table.Column<string>(type: "text", nullable: true),
                    MotherName = table.Column<string>(type: "text", nullable: true),
                    RegistrationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Active"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TBL_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TBL_RequestLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestMethod = table.Column<string>(type: "text", nullable: true),
                    RequestPath = table.Column<string>(type: "text", nullable: true),
                    RequestQuery = table.Column<string>(type: "text", nullable: true),
                    RequestHeaders = table.Column<string>(type: "text", nullable: true),
                    RequestPayload = table.Column<string>(type: "text", nullable: true),
                    ResponsePayload = table.Column<string>(type: "text", nullable: true),
                    StatusCode = table.Column<int>(type: "integer", nullable: true),
                    RequestTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResponseTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    ClientIp = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TBL_RequestLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TBL_Settlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BankTxnDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BatchProcessId = table.Column<string>(type: "text", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ReverseAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    NetSettlementAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalCount = table.Column<int>(type: "integer", nullable: false),
                    ReverseCount = table.Column<int>(type: "integer", nullable: false),
                    NetCount = table.Column<int>(type: "integer", nullable: false),
                    SettlementAccountNo = table.Column<string>(type: "text", nullable: false),
                    ParkingGL = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Pending"),
                    SettlementOperation = table.Column<string>(type: "text", nullable: false),
                    ProcessBrCode = table.Column<string>(type: "text", nullable: false),
                    SettleBrCode = table.Column<string>(type: "text", nullable: false),
                    ProcessedBy = table.Column<string>(type: "text", nullable: false),
                    SettledBy = table.Column<string>(type: "text", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SettledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CBSResponse = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TBL_Settlements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TBL_TransactionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartnerId = table.Column<string>(type: "text", nullable: false),
                    PartnerTxnId = table.Column<string>(type: "text", nullable: false),
                    RequestType = table.Column<string>(type: "text", nullable: false),
                    RequestData = table.Column<string>(type: "text", nullable: false),
                    ResponseData = table.Column<string>(type: "text", nullable: false),
                    ResponseCode = table.Column<string>(type: "text", nullable: false),
                    ResponseMessage = table.Column<string>(type: "text", nullable: false),
                    RequestTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResponseTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SblTxnId = table.Column<string>(type: "text", nullable: false),
                    AccountNo = table.Column<string>(type: "text", nullable: false),
                    TransactionAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    BalanceBefore = table.Column<decimal>(type: "numeric", nullable: true),
                    BalanceAfter = table.Column<decimal>(type: "numeric", nullable: true),
                    TranMode = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TBL_TransactionLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TBL_Wallets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletNo = table.Column<string>(type: "character(14)", fixedLength: true, maxLength: 14, nullable: false),
                    MobileNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0.00m),
                    Currency = table.Column<string>(type: "text", nullable: false, defaultValue: "BDT"),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Active"),
                    CompanyName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "SONALI BANK PLC"),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "BANK"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TBL_Wallets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TBL_Wallets_TBL_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "TBL_Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TBL_DoTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartnerId = table.Column<string>(type: "text", nullable: false),
                    PartnerTxnId = table.Column<string>(type: "text", nullable: false),
                    PartnerTransactionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SourceAccountNo = table.Column<string>(type: "text", nullable: false),
                    TransactionAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ResponseCode = table.Column<string>(type: "text", nullable: false),
                    ResponseMessage = table.Column<string>(type: "text", nullable: false),
                    RefNo1 = table.Column<string>(type: "text", nullable: true),
                    RefNo2 = table.Column<string>(type: "text", nullable: true),
                    RefNo3 = table.Column<string>(type: "text", nullable: true),
                    RefNo4 = table.Column<string>(type: "text", nullable: true),
                    RefNo5 = table.Column<string>(type: "text", nullable: true),
                    ChannelTransactionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BankTxnId = table.Column<string>(type: "text", nullable: false),
                    OriginalBankTxnId = table.Column<string>(type: "text", nullable: false),
                    BankTxnDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TranStatus = table.Column<string>(type: "text", nullable: false),
                    SettlStatus = table.Column<string>(type: "text", nullable: false),
                    SettlDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BatchProcessId = table.Column<string>(type: "text", nullable: true),
                    TranMode = table.Column<string>(type: "text", nullable: false),
                    SourceChannel = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TBL_DoTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TBL_DoTransactions_TBL_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "TBL_Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TBL_Vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleRegistrationNumber = table.Column<string>(type: "text", nullable: false),
                    ChassisNo = table.Column<string>(type: "text", nullable: false),
                    BrtaClass = table.Column<string>(type: "text", nullable: false),
                    RhdClass = table.Column<string>(type: "text", nullable: false),
                    VehicleCC = table.Column<string>(type: "text", nullable: false),
                    VehicleColour = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RegisterDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UnregisterDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TBL_Vehicles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TBL_Vehicles_TBL_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "TBL_Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TBL_Customers_CustomerId",
                table: "TBL_Customers",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TBL_DoTransactions_BankTxnId",
                table: "TBL_DoTransactions",
                column: "BankTxnId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TBL_DoTransactions_PartnerTxnId",
                table: "TBL_DoTransactions",
                column: "PartnerTxnId");

            migrationBuilder.CreateIndex(
                name: "IX_TBL_DoTransactions_WalletId",
                table: "TBL_DoTransactions",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_TBL_Settlements_BatchProcessId",
                table: "TBL_Settlements",
                column: "BatchProcessId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TBL_Vehicles_WalletId",
                table: "TBL_Vehicles",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_TBL_Wallets_CustomerId",
                table: "TBL_Wallets",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_TBL_Wallets_MobileNo",
                table: "TBL_Wallets",
                column: "MobileNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TBL_Wallets_WalletNo",
                table: "TBL_Wallets",
                column: "WalletNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TBL_ApiTokens");

            migrationBuilder.DropTable(
                name: "TBL_ApiUsers");

            migrationBuilder.DropTable(
                name: "TBL_DoTransactions");

            migrationBuilder.DropTable(
                name: "TBL_RequestLogs");

            migrationBuilder.DropTable(
                name: "TBL_Settlements");

            migrationBuilder.DropTable(
                name: "TBL_TransactionLogs");

            migrationBuilder.DropTable(
                name: "TBL_Vehicles");

            migrationBuilder.DropTable(
                name: "TBL_Wallets");

            migrationBuilder.DropTable(
                name: "TBL_Customers");
        }
    }
}
