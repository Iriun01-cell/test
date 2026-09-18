using Azure;
using Azure.Data.Tables;

namespace RMS.Api.Models;

public class Reservation : ITableEntity
{
    // PartitionKey: 施設名 (場所ごとにグループ化して、同じ施設内の予約を効率よく検索できるようにする)
    public string PartitionKey { get; set; } = string.Empty;

    // RowKey: 予約ID (GUID)
    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    /// <summary>予約者名</summary>
    public string ReserverName { get; set; } = string.Empty;

    /// <summary>予約開始日時</summary>
    public DateTime StartDateTime { get; set; }

    /// <summary>予約終了日時</summary>
    public DateTime EndDateTime { get; set; }

    /// <summary>場所・施設名 (PartitionKeyと同じ値)</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>利用人数</summary>
    public int NumberOfPeople { get; set; }

    /// <summary>利用目的</summary>
    public string Purpose { get; set; } = string.Empty;
}
