namespace DentalLabOS.Domain.Common;

// Stored as strings (see ADR-0004). The full set is declared now so the schema design stays stable;
// tables that use most of them arrive in later phases (plan section D).
public enum DocumentStatus { Draft, PendingApproval, Confirmed, Posted, Voided, Reversed }
public enum TenantStatus { Trial, Active, Suspended, Closed }
public enum PartyType { Clinic, Dentist, Center, Individual }
public enum ItemType { Service, Material, Consumable, FinishedGood, OutsourcedService }
public enum WorkOrderStatus { Draft, Incomplete, Ready, InProduction, OnHold, InQuality, Rejected, Completed, ReadyForDelivery, Delivered, Cancelled }
public enum WorkOrderKind { Original, Remake, Adjustment }
public enum PriorityLevel { Normal, High, Urgent }
public enum StageStatus { Pending, InProgress, Paused, AwaitingQuality, Done, Skipped }
public enum QualityResult { Pending, Accepted, Rejected, AdjustmentRequested }
public enum AdjustmentSource { Quality, Customer }
public enum AdjustmentStatus { Open, Closed }
public enum RemakeChargeTo { Undecided, Lab, Customer, Supplier }
public enum DeliveryStatus { Draft, OutForDelivery, Delivered, Failed, Cancelled }
public enum DeliveryRunStatus { Planned, OnRoute, Completed }
public enum ReasonCategory { DentistData, Design, Manufacturing, Material, Transport, Unspecified }
public enum ReasonScope { Rejection, Adjustment, Remake, Hold, Waste, Cancellation, StockAdjustment, DeliveryFailure }
public enum MovementType { Receipt, Issue, IssueToWorkOrder, ReturnFromWorkOrder, TransferOut, TransferIn, AdjustmentIn, AdjustmentOut, PurchaseReturn, SalesReturn, Waste, OpeningBalance }
public enum ValuationMethod { MovingAverage, Fifo }
public enum PaymentDirection { In, Out }
public enum PaymentMethod { Cash, BankTransfer, Wallet, Cheque, Card }
public enum ChequeStatus { Received, Deposited, Cleared, Bounced, Cancelled }
public enum CashAccountType { Cashbox, Bank, Wallet }
public enum ShiftStatus { Open, Closed }
public enum AccountType { Asset, Liability, Equity, Revenue, Expense }
public enum NormalBalance { Debit, Credit }
public enum PeriodStatus { Open, SoftClosed, HardClosed }
public enum CostLineType { DirectMaterial, DirectLabor, OutsourcedService, Overhead, Remake, Waste }
public enum ApprovalStatus { Pending, Approved, Rejected, Cancelled }
public enum InvoiceType { Standard, CreditNote }
public enum NotificationChannel { InApp, Email, WebPush, Sms }
public enum SyncOperationStatus { Pending, Applied, Conflict, Rejected }

/// <summary>Platform staff roles (separate portal, see plan PLT).</summary>
public enum PlatformRole { None, Owner, Support }
