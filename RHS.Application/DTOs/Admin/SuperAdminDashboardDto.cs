using System;
using System.Collections.Generic;

namespace RHS.Application.DTOs.Admin;

/// <summary>
/// DTO tổng quan toàn sàn cho Super Admin
/// </summary>
public class PlatformOverviewStatDto
{
    public int TotalProjects { get; set; }
    public int TotalHousingDevelopers { get; set; }
    public int TotalSxdOfficers { get; set; }
    public int TotalApplicants { get; set; }
    public int TotalApplications { get; set; }
    public int TotalApartments { get; set; }
    public decimal TotalContractValueVnd { get; set; }
    public decimal TotalCollectedPaymentVnd { get; set; }
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// DTO thống kê mức độ hấp thụ căn hộ NOXH theo dự án
/// </summary>
public class PlatformAbsorptionStatDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public int TotalUnits { get; set; }
    public int DepositedOrSoldUnits { get; set; }
    public int AvailableUnits { get; set; }
    public double AbsorptionRatePercentage { get; set; }
}

/// <summary>
/// DTO thống kê tình hình thanh toán & giải ngân toàn sàn
/// </summary>
public class PlatformDisbursementStatDto
{
    public decimal TotalDueInstallmentAmountVnd { get; set; }
    public decimal TotalPaidInstallmentAmountVnd { get; set; }
    public decimal TotalOverdueAmountVnd { get; set; }
    public decimal TotalPenaltyInterestAccruedVnd { get; set; }
    public double PaymentCollectionRatePercentage { get; set; }
}

/// <summary>
/// DTO thống kê tỷ lệ hồ sơ hợp lệ / không hợp lệ cho cơ quan nhà nước
/// </summary>
public class PlatformApplicationRatioDto
{
    public int TotalApplications { get; set; }
    public int ApprovedCount { get; set; }
    public int ApprovedByTimeoutCount { get; set; }
    public int RejectedCount { get; set; }
    public int PendingCount { get; set; }
    public int ViolationCount { get; set; }
    public int CancelledCount { get; set; }
    public double ValidityPercentage { get; set; }
    public double RejectionPercentage { get; set; }
    public double ViolationPercentage { get; set; }
}
