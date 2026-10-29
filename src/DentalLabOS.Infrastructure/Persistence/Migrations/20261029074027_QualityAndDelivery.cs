using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalLabOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QualityAndDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "QualityTemplateId",
                table: "WorkOrderStages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeliveryNoteId",
                table: "WorkOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "QualityTemplateId",
                table: "WorkflowStages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Adjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    QualityCheckId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AdjustmentWorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReasonCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Adjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Adjustments_ReasonCodes_ReasonCodeId",
                        column: x => x.ReasonCodeId,
                        principalTable: "ReasonCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Adjustments_WorkOrders_AdjustmentWorkOrderId",
                        column: x => x.AdjustmentWorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Adjustments_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CourierUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CourierName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QualityChecks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StageName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TemplateName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    InspectorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Result = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ReasonCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ReturnToStageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CheckedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualityChecks_ReasonCodes_ReasonCodeId",
                        column: x => x.ReasonCodeId,
                        principalTable: "ReasonCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QualityChecks_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QualityTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Remakes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalWorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RemakeWorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QualityCheckId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReasonCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ChargeTo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CostCharged = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Remakes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Remakes_ReasonCodes_ReasonCodeId",
                        column: x => x.ReasonCodeId,
                        principalTable: "ReasonCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Remakes_WorkOrders_OriginalWorkOrderId",
                        column: x => x.OriginalWorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Remakes_WorkOrders_RemakeWorkOrderId",
                        column: x => x.RemakeWorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DeliveryNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ClientOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RunSequence = table.Column<int>(type: "int", nullable: false),
                    DispatchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceivedByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DeliveredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureReasonCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FailureNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProofFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryNotes_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeliveryNotes_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeliveryNotes_DeliveryRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "DeliveryRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeliveryNotes_Files_ProofFileId",
                        column: x => x.ProofFileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeliveryNotes_ReasonCodes_FailureReasonCodeId",
                        column: x => x.FailureReasonCodeId,
                        principalTable: "ReasonCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QualityCheckItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QualityCheckId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsCritical = table.Column<bool>(type: "bit", nullable: false),
                    Passed = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityCheckItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualityCheckItems_QualityChecks_QualityCheckId",
                        column: x => x.QualityCheckId,
                        principalTable: "QualityChecks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QualityTemplateItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QualityTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsCritical = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityTemplateItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualityTemplateItems_QualityTemplates_QualityTemplateId",
                        column: x => x.QualityTemplateId,
                        principalTable: "QualityTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryNoteLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackagingChecked = table.Column<bool>(type: "bit", nullable: false),
                    CheckedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryNoteLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryNoteLines_DeliveryNotes_DeliveryNoteId",
                        column: x => x.DeliveryNoteId,
                        principalTable: "DeliveryNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeliveryNoteLines_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderStages_QualityTemplateId",
                table: "WorkOrderStages",
                column: "QualityTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_DeliveryNoteId",
                table: "WorkOrders",
                column: "DeliveryNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_TenantId_OriginalWorkOrderId",
                table: "WorkOrders",
                columns: new[] { "TenantId", "OriginalWorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStages_QualityTemplateId",
                table: "WorkflowStages",
                column: "QualityTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Adjustments_AdjustmentWorkOrderId",
                table: "Adjustments",
                column: "AdjustmentWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Adjustments_ReasonCodeId",
                table: "Adjustments",
                column: "ReasonCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Adjustments_TenantId",
                table: "Adjustments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Adjustments_TenantId_Status_RequestedAtUtc",
                table: "Adjustments",
                columns: new[] { "TenantId", "Status", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Adjustments_TenantId_WorkOrderId",
                table: "Adjustments",
                columns: new[] { "TenantId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_Adjustments_WorkOrderId",
                table: "Adjustments",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNoteLines_DeliveryNoteId",
                table: "DeliveryNoteLines",
                column: "DeliveryNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNoteLines_TenantId",
                table: "DeliveryNoteLines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNoteLines_TenantId_DeliveryNoteId_WorkOrderId",
                table: "DeliveryNoteLines",
                columns: new[] { "TenantId", "DeliveryNoteId", "WorkOrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNoteLines_TenantId_WorkOrderId",
                table: "DeliveryNoteLines",
                columns: new[] { "TenantId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNoteLines_WorkOrderId",
                table: "DeliveryNoteLines",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_BranchId",
                table: "DeliveryNotes",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_CustomerId",
                table: "DeliveryNotes",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_FailureReasonCodeId",
                table: "DeliveryNotes",
                column: "FailureReasonCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_ProofFileId",
                table: "DeliveryNotes",
                column: "ProofFileId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_RunId",
                table: "DeliveryNotes",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_TenantId",
                table: "DeliveryNotes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_TenantId_ClientOperationId",
                table: "DeliveryNotes",
                columns: new[] { "TenantId", "ClientOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_TenantId_Number",
                table: "DeliveryNotes",
                columns: new[] { "TenantId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_TenantId_RunId_RunSequence",
                table: "DeliveryNotes",
                columns: new[] { "TenantId", "RunId", "RunSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_TenantId_Status_DocumentDate",
                table: "DeliveryNotes",
                columns: new[] { "TenantId", "Status", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryRuns_TenantId",
                table: "DeliveryRuns",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryRuns_TenantId_RunDate",
                table: "DeliveryRuns",
                columns: new[] { "TenantId", "RunDate" });

            migrationBuilder.CreateIndex(
                name: "IX_QualityCheckItems_QualityCheckId",
                table: "QualityCheckItems",
                column: "QualityCheckId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityCheckItems_TenantId",
                table: "QualityCheckItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityChecks_ReasonCodeId",
                table: "QualityChecks",
                column: "ReasonCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityChecks_TenantId",
                table: "QualityChecks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityChecks_TenantId_Result_CheckedAtUtc",
                table: "QualityChecks",
                columns: new[] { "TenantId", "Result", "CheckedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_QualityChecks_TenantId_WorkOrderId_CheckedAtUtc",
                table: "QualityChecks",
                columns: new[] { "TenantId", "WorkOrderId", "CheckedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_QualityChecks_WorkOrderId",
                table: "QualityChecks",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityTemplateItems_QualityTemplateId",
                table: "QualityTemplateItems",
                column: "QualityTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityTemplateItems_TenantId",
                table: "QualityTemplateItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityTemplates_OneDefaultPerTenant",
                table: "QualityTemplates",
                column: "TenantId",
                unique: true,
                filter: "[IsDefault] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_QualityTemplates_TenantId_Name",
                table: "QualityTemplates",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Remakes_OriginalWorkOrderId",
                table: "Remakes",
                column: "OriginalWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Remakes_ReasonCodeId",
                table: "Remakes",
                column: "ReasonCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Remakes_RemakeWorkOrderId",
                table: "Remakes",
                column: "RemakeWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Remakes_TenantId",
                table: "Remakes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Remakes_TenantId_DecidedAtUtc",
                table: "Remakes",
                columns: new[] { "TenantId", "DecidedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Remakes_TenantId_OriginalWorkOrderId",
                table: "Remakes",
                columns: new[] { "TenantId", "OriginalWorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_Remakes_TenantId_RemakeWorkOrderId",
                table: "Remakes",
                columns: new[] { "TenantId", "RemakeWorkOrderId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowStages_QualityTemplates_QualityTemplateId",
                table: "WorkflowStages",
                column: "QualityTemplateId",
                principalTable: "QualityTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_DeliveryNotes_DeliveryNoteId",
                table: "WorkOrders",
                column: "DeliveryNoteId",
                principalTable: "DeliveryNotes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrderStages_QualityTemplates_QualityTemplateId",
                table: "WorkOrderStages",
                column: "QualityTemplateId",
                principalTable: "QualityTemplates",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowStages_QualityTemplates_QualityTemplateId",
                table: "WorkflowStages");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_DeliveryNotes_DeliveryNoteId",
                table: "WorkOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrderStages_QualityTemplates_QualityTemplateId",
                table: "WorkOrderStages");

            migrationBuilder.DropTable(
                name: "Adjustments");

            migrationBuilder.DropTable(
                name: "DeliveryNoteLines");

            migrationBuilder.DropTable(
                name: "QualityCheckItems");

            migrationBuilder.DropTable(
                name: "QualityTemplateItems");

            migrationBuilder.DropTable(
                name: "Remakes");

            migrationBuilder.DropTable(
                name: "DeliveryNotes");

            migrationBuilder.DropTable(
                name: "QualityChecks");

            migrationBuilder.DropTable(
                name: "QualityTemplates");

            migrationBuilder.DropTable(
                name: "DeliveryRuns");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrderStages_QualityTemplateId",
                table: "WorkOrderStages");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_DeliveryNoteId",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_TenantId_OriginalWorkOrderId",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowStages_QualityTemplateId",
                table: "WorkflowStages");

            migrationBuilder.DropColumn(
                name: "QualityTemplateId",
                table: "WorkOrderStages");

            migrationBuilder.DropColumn(
                name: "DeliveryNoteId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "QualityTemplateId",
                table: "WorkflowStages");
        }
    }
}
