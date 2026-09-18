using System.Net;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RMS.Api.Models;

namespace RMS.Api.Functions;

public class ReservationsFunctions
{
    private readonly TableClient _tableClient;
    private readonly ILogger<ReservationsFunctions> _logger;

    public ReservationsFunctions(TableClient tableClient, ILogger<ReservationsFunctions> logger)
    {
        _tableClient = tableClient;
        _logger = logger;
    }

    /// <summary>予約一覧を取得する。locationを指定するとその施設の予約のみ返す。</summary>
    [Function("GetReservations")]
    public async Task<HttpResponseData> GetReservations(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reservations")] HttpRequestData req)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var location = query["location"];

        var results = new List<ReservationDto>();

        AsyncPageable<Reservation> pageable = string.IsNullOrEmpty(location)
            ? _tableClient.QueryAsync<Reservation>()
            : _tableClient.QueryAsync<Reservation>(r => r.PartitionKey == location);

        await foreach (var entity in pageable)
        {
            results.Add(ToDto(entity));
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(results.OrderBy(r => r.StartDateTime));
        return response;
    }

    /// <summary>新しい予約を登録する。同じ施設・時間帯に既存予約があれば409を返す。</summary>
    [Function("CreateReservation")]
    public async Task<HttpResponseData> CreateReservation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "reservations")] HttpRequestData req)
    {
        var dto = await req.ReadFromJsonAsync<ReservationDto>();
        if (dto is null || string.IsNullOrWhiteSpace(dto.Location) || string.IsNullOrWhiteSpace(dto.ReserverName))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "ReserverNameとLocationは必須です。" });
            return badRequest;
        }

        if (dto.EndDateTime <= dto.StartDateTime)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "終了日時は開始日時より後にしてください。" });
            return badRequest;
        }

        if (await HasOverlapAsync(dto.Location, dto.StartDateTime, dto.EndDateTime, excludeRowKey: null))
        {
            var conflict = req.CreateResponse(HttpStatusCode.Conflict);
            await conflict.WriteAsJsonAsync(new { error = "指定した時間帯は既に予約されています。" });
            return conflict;
        }

        var entity = new Reservation
        {
            PartitionKey = dto.Location,
            RowKey = Guid.NewGuid().ToString(),
            ReserverName = dto.ReserverName,
            StartDateTime = dto.StartDateTime,
            EndDateTime = dto.EndDateTime,
            Location = dto.Location,
            NumberOfPeople = dto.NumberOfPeople,
            Purpose = dto.Purpose
        };

        await _tableClient.AddEntityAsync(entity);

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(ToDto(entity));
        return response;
    }

    /// <summary>既存の予約を更新する。</summary>
    [Function("UpdateReservation")]
    public async Task<HttpResponseData> UpdateReservation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "reservations/{location}/{id}")] HttpRequestData req,
        string location,
        string id)
    {
        var dto = await req.ReadFromJsonAsync<ReservationDto>();
        if (dto is null)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "リクエスト内容が不正です。" });
            return badRequest;
        }

        if (dto.EndDateTime <= dto.StartDateTime)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "終了日時は開始日時より後にしてください。" });
            return badRequest;
        }

        // 場所を変更する場合、移動先の施設名で重複チェックする
        var targetLocation = string.IsNullOrWhiteSpace(dto.Location) ? location : dto.Location;

        if (await HasOverlapAsync(targetLocation, dto.StartDateTime, dto.EndDateTime, excludeRowKey: id))
        {
            var conflict = req.CreateResponse(HttpStatusCode.Conflict);
            await conflict.WriteAsJsonAsync(new { error = "指定した時間帯は既に予約されています。" });
            return conflict;
        }

        try
        {
            // 場所(PartitionKey)が変わる場合は、古いエンティティを削除して新しいキーで作り直す
            if (!string.Equals(location, targetLocation, StringComparison.Ordinal))
            {
                await _tableClient.DeleteEntityAsync(location, id);
            }

            var entity = new Reservation
            {
                PartitionKey = targetLocation,
                RowKey = id,
                ReserverName = dto.ReserverName,
                StartDateTime = dto.StartDateTime,
                EndDateTime = dto.EndDateTime,
                Location = targetLocation,
                NumberOfPeople = dto.NumberOfPeople,
                Purpose = dto.Purpose
            };

            await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ToDto(entity));
            return response;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }
    }

    /// <summary>予約を削除する。</summary>
    [Function("DeleteReservation")]
    public async Task<HttpResponseData> DeleteReservation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "reservations/{location}/{id}")] HttpRequestData req,
        string location,
        string id)
    {
        try
        {
            await _tableClient.DeleteEntityAsync(location, id);
            return req.CreateResponse(HttpStatusCode.NoContent);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }
    }

    /// <summary>同じ施設内で、指定した時間帯と重なる予約が既にあるかを調べる。</summary>
    private async Task<bool> HasOverlapAsync(string location, DateTime start, DateTime end, string? excludeRowKey)
    {
        await foreach (var existing in _tableClient.QueryAsync<Reservation>(r => r.PartitionKey == location))
        {
            if (excludeRowKey is not null && existing.RowKey == excludeRowKey)
            {
                continue;
            }

            // 時間帯が重なっているかどうか: 既存の開始 < 新規の終了 かつ 既存の終了 > 新規の開始
            if (existing.StartDateTime < end && existing.EndDateTime > start)
            {
                return true;
            }
        }

        return false;
    }

    private static ReservationDto ToDto(Reservation entity) => new()
    {
        Id = entity.RowKey,
        ReserverName = entity.ReserverName,
        StartDateTime = entity.StartDateTime,
        EndDateTime = entity.EndDateTime,
        Location = entity.Location,
        NumberOfPeople = entity.NumberOfPeople,
        Purpose = entity.Purpose
    };
}
