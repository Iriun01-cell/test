namespace RMS.Web.Models;

/// <summary>APIとやり取りする予約データ</summary>
public class ReservationDto
{
    /// <summary>予約ID (新規登録時は空でよい)</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>予約者名</summary>
    public string ReserverName { get; set; } = string.Empty;

    /// <summary>予約開始日時</summary>
    public DateTime StartDateTime { get; set; }

    /// <summary>予約終了日時</summary>
    public DateTime EndDateTime { get; set; }

    /// <summary>場所・施設名</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>利用人数</summary>
    public int NumberOfPeople { get; set; } = 1;

    /// <summary>利用目的</summary>
    public string Purpose { get; set; } = string.Empty;
}
